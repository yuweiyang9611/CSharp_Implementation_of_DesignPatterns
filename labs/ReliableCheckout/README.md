# ReliableCheckout：从“能下单”到“故障后仍然正确”

这是本仓库的生产化毕业项目。它不是一个模式陈列柜，而是一条完整的结账链路：HTTP 请求创建订单、SQLite 原子预留库存、同一事务写入 Outbox、后台投递支付请求、接收支付回调，并在重复、乱序、并发和进程崩溃窗口下保护业务不变量。

项目继承仓库根目录的 `.NET 10 + C# 14` 设置。API 和测试都可以独立于根解决方案运行。

## 你将学到什么

完成本项目后，你应该能回答这些比“这个类用了什么模式”更重要的问题：

- 客户端超时重试时，为什么不能再次扣库存？
- 两个买家同时争抢最后一件库存时，为什么不会超卖？
- 订单已经提交，但进程在发送支付请求前退出，支付怎样最终发出？
- 支付平台重复发送、乱序发送回调时，订单为什么不会从 `Paid` 倒退？
- 消费者已经完成工作、却来不及把 Outbox 标成完成时，为什么重放不会二次支付？
- 为什么“至少一次投递 + 幂等处理”通常比宣称“恰好一次”更诚实？

## 快速运行

前置条件：.NET 10 SDK。

从仓库根目录执行：

```powershell
dotnet restore labs/ReliableCheckout/ReliableCheckout.slnx
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release
dotnet run --project labs/ReliableCheckout/ReliableCheckout.Api --urls http://localhost:5188
```

应用会在 API 项目的 `data/reliable-checkout.db` 创建 SQLite 数据库，并种入 `DEMO-SKU`：库存 10、单价 1999 分。相对数据库路径始终按应用 Content Root 解析，因此从仓库根目录或项目目录启动都得到同一个位置。

另开一个 PowerShell 窗口，走完一次结账：

```powershell
$headers = @{ "Idempotency-Key" = "lesson-order-001" }
$order = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5188/orders `
  -Headers $headers `
  -ContentType application/json `
  -Body '{"sku":"DEMO-SKU","quantity":2}'

# BackgroundService 默认每 500 ms 投递一次 Outbox。
Start-Sleep -Seconds 1
$order = Invoke-RestMethod -Uri "http://localhost:5188/orders/$($order.id)"
$order

$callback = @{
  eventId = "gateway-event-001"
  orderId = $order.id
  externalPaymentId = $order.externalPaymentId
  outcome = "succeeded"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5188/payments/callback `
  -ContentType application/json `
  -Body $callback
```

再次使用完全相同的 `Idempotency-Key` 和请求体调用 `POST /orders`，会得到原订单且 `replayed=true`；同一个 Key 搭配不同数量会得到 `409 idempotency_conflict`。

## HTTP 边界

| 方法 | 路径 | 作用 | 关键约束 |
| --- | --- | --- | --- |
| `POST` | `/orders` | 创建订单并预留库存 | 必须提供 `Idempotency-Key` |
| `GET` | `/orders/{orderId}` | 查询订单和支付状态 | 不触发写操作 |
| `POST` | `/payments/callback` | 接收支付结果 | `EventId` 去重，State 校验顺序 |
| `GET` | `/inventory/{sku}` | 查询教学库存 | 用于观察原子预留结果 |
| `GET` | `/health` | 存活检查 | 返回 `200` |

主要状态码：

- `201 Created`：第一次成功提交订单。
- `200 OK`：查询成功，或幂等重放成功。
- `400 Bad Request`：缺少 Key、数量非法或回调结果非法。
- `404 Not Found`：订单或 SKU 不存在。
- `409 Conflict`：Key 与原请求冲突、库存不足、支付身份不匹配或状态转换非法。

## 架构全景

```mermaid
flowchart LR
    Client["客户端"] -->|"POST /orders + Idempotency-Key"| API["Minimal API"]
    Gateway["支付平台"] -->|"POST /payments/callback"| API
    API --> Checkout["CheckoutStore"]
    API --> Callback["PaymentCallbackService"]
    Checkout -->|"订单 + 库存 + Outbox 同事务"| DB[("SQLite")]
    Callback -->|"状态 + 回调收据 + Outbox 同事务"| DB
    Worker["OutboxWorker"] --> Dispatcher["OutboxDispatcher"]
    Dispatcher --> DB
    Dispatcher --> Consumer["PaymentRequestedHandler"]
    Consumer --> Adapter["CallbackPaymentGatewayAdapter"]
    Adapter --> Legacy["回调式 Legacy Payment SDK"]
    Consumer -->|"消费者收据 + 支付状态"| DB
    Dispatcher --> Projection["OrderProjectionHandler"]
    Projection -->|"幂等读模型"| DB
```

目录结构：

```text
labs/ReliableCheckout/
├─ ReliableCheckout.slnx
├─ README.md
├─ ReliableCheckout.Api/
│  ├─ Application/       # HTTP DTO、结账事务、支付回调用例
│  ├─ Domain/            # 订单/支付状态与合法转换
│  ├─ Infrastructure/    # SQLite、时钟、确定性故障注入
│  ├─ Messaging/         # Outbox、BackgroundService、幂等消费者
│  ├─ Payments/          # 旧 SDK 与 Task-based Adapter
│  └─ Program.cs         # Minimal API 组合根
└─ ReliableCheckout.Tests/
   ├─ CheckoutApiTests.cs
   └─ ReliableCheckoutApplicationFactory.cs
```

## 场景一：幂等结账与原子库存

`CheckoutStore.CreateOrderAsync` 使用 SQLite immediate transaction。事务取得写锁后，先按 `Idempotency-Key` 查询：

1. Key 不存在：执行条件更新 `available >= quantity`，然后写订单、支付初态和 Outbox。
2. Key 已存在且请求指纹相同：返回原订单，不再扣库存。
3. Key 已存在但请求指纹不同：返回冲突，避免“相同操作标识代表两个意图”。

库存不是“先查询再扣减”，而是一条原子 SQL：

```sql
UPDATE inventory
SET available = available - $quantity
WHERE sku = $sku AND available >= $quantity;
```

影响行数不是 1 就表示预留失败。因此即使两个请求并发争抢最后一件商品，也只会有一个请求成功。

```mermaid
sequenceDiagram
    participant C as 客户端
    participant A as POST /orders
    participant S as CheckoutStore
    participant D as SQLite

    C->>A: Idempotency-Key + sku + quantity
    A->>S: CreateOrderAsync
    S->>D: BEGIN IMMEDIATE
    S->>D: 查询 Key
    alt 幂等重放
        D-->>S: 原订单 + 原请求指纹
        S->>D: COMMIT
        S-->>A: 原订单 replayed=true
    else 新订单
        S->>D: 条件 UPDATE 库存
        S->>D: INSERT order + payment
        S->>D: INSERT PaymentRequested outbox
        S->>D: COMMIT
        S-->>A: 新订单
    end
    A-->>C: 201 或 200
```

这里没有额外套一个“库存锁模式”。真正保护不变量的是数据库事务和条件更新。

## 场景二：Transactional Outbox 与最终一致性

创建订单时不直接调用支付平台，因为“提交数据库”和“发送网络请求”无法由本项目中的同一个本地事务原子完成。订单和 `PaymentRequested` 事件写入同一 SQLite 事务：

- 事务回滚：订单和事件都不存在。
- 事务提交：订单和待投递事件必定同时存在。
- 进程随后退出：重启后的 `OutboxWorker` 仍能找到事件。

`OutboxWorker` 是 `BackgroundService`，轮询 `OutboxDispatcher`。失败时记录：

- `attempts`
- `next_attempt_at`
- `last_error`

退避时间为 `2^attempt` 秒，上限 64 秒。时钟来自 `IClock`，测试用 `ManualClock` 直接推进，不需要真的等待。

```mermaid
sequenceDiagram
    participant W as OutboxWorker
    participant D as OutboxDispatcher
    participant H as PaymentRequestedHandler
    participant P as Payment Adapter
    participant DB as SQLite

    W->>D: DispatchBatchAsync
    D->>DB: 读取到期且未完成事件
    D->>H: Handle(event)
    H->>P: StartAsync(event.Id 作为幂等键)
    alt 第一次失败
        P--xH: exception
        D->>DB: attempts + 1, 写 next_attempt_at
    else 支付平台接受
        P-->>H: externalPaymentId
        H->>DB: 支付状态 + consumer receipt 同事务
        D->>DB: 标记 outbox processed
    end
```

### 为什么需要幂等消费者

存在一个无法消除的崩溃窗口：消费者已完成工作，但 Dispatcher 还没有把 Outbox 标成完成。重启后同一事件会再次投递。

本项目使用两层防线：

1. `PaymentRequestedHandler` 在 `consumer_receipts` 保存事件收据；重复事件直接返回。
2. Adapter 把 Outbox 事件 ID 传给支付平台作为幂等键；即使进程恰好在保存本地收据前退出，平台也不应重复扣款。

这不是数学意义上的“恰好一次”。它是可解释、可测试的“至少一次投递 + 端到端幂等”。

## 场景三：支付 Adapter 与合法状态转换

示例中的 `ILegacyPaymentSdk` 用成功/失败回调返回结果；应用层只依赖 `IPaymentGateway.StartAsync`。`CallbackPaymentGatewayAdapter` 用 `TaskCompletionSource` 把旧式回调协议转换为可等待的 Task，并传递取消信号。

```mermaid
flowchart LR
    Handler["PaymentRequestedHandler"] -->|"Task&lt;PaymentStartResult&gt;"| Port["IPaymentGateway"]
    Port --> Adapter["CallbackPaymentGatewayAdapter"]
    Adapter -->|"accepted / rejected callbacks"| SDK["ILegacyPaymentSdk"]
```

Adapter 只解决协议不匹配，不负责订单规则。状态合法性由两个集中式 State Machine 控制：

```mermaid
stateDiagram-v2
    state "PendingRequest" as PR
    state "Requested" as R
    state "Succeeded" as S
    state "Failed" as F
    [*] --> PR
    PR --> R: RequestAccepted
    R --> S: Succeeded callback
    R --> F: Failed callback
```

```mermaid
stateDiagram-v2
    state "AwaitingPayment" as A
    state "Paid" as P
    state "PaymentFailed" as F
    [*] --> A
    A --> P: payment succeeded
    A --> F: payment failed
```

没有画出的边就是非法转换。例如：

- 支付请求尚未发出就收到成功回调：`409 invalid_state_transition`。
- 订单已经 `Paid`，随后收到一个新的失败事件：拒绝，不允许状态倒退。
- 完全相同的回调 `EventId` 重放：返回当前订单，`replayed=true`。
- 同一个回调 `EventId` 搭配不同内容：`409 idempotency_conflict`。

支付回调写入支付状态、订单状态、回调收据和结果 Outbox，也在同一个事务中完成。

## 故障矩阵

| 故障/竞争位置 | 可观察状态 | 恢复机制 | 业务不变量 |
| --- | --- | --- | --- |
| 写订单事务提交前进程退出 | 订单、扣库存、Outbox 一起回滚 | 客户端可用同一 Key 重试 | 不留下半张订单 |
| 事务提交后、投递前退出 | 订单和待处理 Outbox 都存在 | Worker 重启后继续投递 | 不丢支付请求 |
| 两个买家抢最后一件库存 | 一个条件更新影响 1 行，另一个 0 行 | 失败方收到 409 | 库存永不为负 |
| 客户端因超时重复提交 | 命中相同 Key 和指纹 | 返回原订单 | 只预留一次库存 |
| 相同 Key 携带不同请求体 | 指纹不一致 | 返回 409 | Key 不代表两个业务意图 |
| 第一次 Outbox 投递抛错 | `attempts=1`，记录错误与下次时间 | 指数退避后重试 | 事件仍未标完成 |
| 支付调用成功后、写消费收据前退出 | 本地可能没有收据 | 支付平台必须持久化事件 ID 并去重 | 不重复创建支付 |
| 消费完成后、Outbox 标记前退出 | 收据存在，Outbox 仍待处理 | 重放时消费者跳过 | 不重复执行副作用 |
| 支付平台重复发同一回调 | 回调收据已存在 | 返回当前结果 | 状态只改变一次 |
| 成功之后又到达失败回调 | State Machine 拒绝 | 返回 409 并记录日志 | `Paid` 不倒退 |
| 伪造其他支付单号的回调 | external payment id 不匹配 | 返回 409 | 回调不能串单 |

## 模式为什么出现在这里

| 技术/模式 | 变化压力 | 本项目中的职责 | 刻意不做的事 |
| --- | --- | --- | --- |
| Adapter | 第三方 SDK 是回调式，应用是 Task 异步模型 | 转换调用协议与返回值 | 不承载订单状态规则 |
| State | 回调可能重复、乱序，状态不能任意赋值 | 明确允许的支付/订单转换 | 不把每个状态做成无业务价值的大类层次 |
| Facade/应用服务 | HTTP 端点不应拼装事务细节 | `CheckoutStore`、`PaymentCallbackService` 表达用例 | 不隐藏错误语义 |
| Transactional Outbox | 数据库提交与网络发送无法原子完成 | 把“需要发送”与业务数据同事务保存 | 不宣称跨系统 ACID |
| Background Worker | 网络故障需要脱离请求线程重试 | 轮询、投递、退避、结构化日志 | 不阻塞 `POST /orders` 等支付完成 |
| Idempotent Consumer | 至少一次投递必然可能重复 | 收据表 + 外部幂等键 | 不依赖“消息永远只来一次” |
| Dependency Injection | 测试必须控制时间、故障和外部平台 | 替换 `IClock`、驱动 Dispatcher | 不为每个简单值制造接口 |

Transactional Outbox、幂等消费者和依赖注入不是 GoF 23 种模式，但它们是把经典模式放进真实故障模型时不可缺少的工程手段。

## 自动化测试

执行：

```powershell
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release
```

测试不是检查类名或控制台文本，而是通过真实 ASP.NET Core HTTP 管道和独立 SQLite 文件验证业务行为：

1. `Duplicate_submission...`：同 Key 重放返回同一订单，库存只扣一次，请求体漂移被拒绝。
2. `Concurrent_buyers...`：两个并发 HTTP 请求争抢最后一件商品，恰好一个成功。
3. `Duplicate_and_out_of_order_callbacks...`：早到、重复、成功后失败都得到正确处理。
4. `First_outbox_delivery_failure...`：第一次投递确定性失败，推进手动时钟后恢复。
5. `Consumer_replay_after_post_handler_crash...`：handler 已完成但标记失败，重放不产生第二次支付请求。

此外，`Null_callback_outcome...` 固定 HTTP 输入边界：缺失回调结果必须返回 `400 invalid_webhook`，不能泄漏为服务器 `500`。

测试宿主有两个重要设计：

- 移除后台 `IHostedService`，由测试精确决定何时投递，避免竞态掩盖断言。
- 用 `ManualClock` 取代系统时间，2 秒退避只需一次 `Advance`，测试在约一秒内完成。

`DeterministicFailureInjector` 使用命名故障点：

- `outbox:PaymentRequested`：handler 调用前失败。
- `outbox:after-handler:PaymentRequested`：handler 成功后、Outbox 标记前失败。

这些故障点只通过依赖注入暴露给测试，没有做成生产 HTTP 后门。

第 5 个测试覆盖“消费者事务已提交、Outbox 尚未标记”的崩溃窗口。更窄的“支付平台已接受、消费者收据尚未提交”窗口依赖支付平台跨进程持久化幂等键；本项目的内存 SDK 只模拟该契约，不构成重启证据。生产验收必须用支付沙箱或可持久化 fake 做进程重启测试。

## SQLite 数据模型

| 表 | 作用 |
| --- | --- |
| `inventory` | SKU 可用数量与单价 |
| `orders` | 订单、幂等 Key、请求指纹和订单状态 |
| `payments` | 外部支付标识与支付状态 |
| `outbox` | 待投递事件、尝试次数、下次时间、最后错误 |
| `consumer_receipts` | 每个消费者已处理的事件及内容指纹 |
| `order_projection` | 由订单结果事件构建的幂等读模型示例 |

项目显式引用 `SQLitePCLRaw.bundle_e_sqlite3 2.1.12`，覆盖 `Microsoft.Data.Sqlite 10.0.10` 原本解析到的 2.1.11。原因是 2.1.11 自 2026 年 7 月起被 NuGet 标记为包含高危原生 SQLite 漏洞；这里选择兼容的修复版本，而不是关闭 `NU1903`。参见 [SQLitePCLRaw.lib.e_sqlite3 的 NuGet 版本与安全提示](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3)。

## 结构化日志

日志模板保留字段，而不是提前拼接字符串。例如：

```text
Created order {OrderId}; reserved {Quantity} of {Sku}; outbox event {OutboxId}
Outbox event {EventId} of type {MessageType} failed on attempt {Attempt}
Applied payment webhook {WebhookEventId}; order {OrderId} moved to {OrderStatus}
```

接入 OpenTelemetry、Serilog 或云日志平台时，这些字段可以直接检索和聚合。

## 本项目没有假装已经解决的生产问题

这是“最小生产式”教学项目，不是可直接收款的商业系统。真正上线前至少还需要：

- 验证支付平台签名、时间戳和防重放窗口；API 身份认证与限流。
- 用正式支付沙箱替换内存 SDK，保存并轮换密钥。
- 使用 EF Core migrations、DbUp 或专用迁移流程替代启动时建表。
- 多实例 Worker 需要事件 claim/lease；当前消费者是幂等的，但会产生额外重复调用。
- SQLite 适合单节点教学；高写入量下应评估 PostgreSQL/SQL Server 的锁与隔离级别。
- 支付失败/超时后的库存释放、订单取消和补偿流程。
- Outbox 死信、最大重试次数、人工重放、指标和告警。
- Trace/Metric、敏感字段脱敏、备份恢复和容量压测。
- 请求指纹可升级为规范化 JSON 哈希，并结合租户/用户范围。

## 推荐练习顺序

1. 先只读 `CheckoutApiTests`，写下每条测试保护的不变量。
2. 在 `CreateOrderAsync` 中把原子 UPDATE 暂时改成“先读再写”，观察并发测试为什么可能失败，然后恢复。
3. 暂时删除回调收据检查，观察重复回调对系统意味着什么。
4. 增加 `Cancelled` 和 `Refunded`，先写非法转换测试，再扩展 State Machine。
5. 为 Outbox 增加最大重试和 dead-letter 表，保持失败原因可审计。
6. 把 `ILegacyPaymentSdk` 替换成一个本地假 HTTP 服务，保持应用层和测试用例基本不变，验证 Adapter 边界是否稳定。

毕业标准不是“能指出用了 Adapter、State、Outbox”，而是能从一个具体故障出发，解释哪个不变量会破坏、哪个边界负责恢复，以及测试如何证明它。
