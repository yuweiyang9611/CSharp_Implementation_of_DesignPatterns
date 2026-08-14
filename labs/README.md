# 高级实验：从模式组合到生产可靠性

`labs` 是基础课程之后的高级实践层。这里不再按模式名称组织代码，而是从两类真实压力出发：**怎样安全重构已有代码**，以及**怎样让系统在重试、并发、乱序和崩溃后仍保持正确**。

> 推荐先完成三个 [`examples`](../examples/README.md) 教学项目，再进入本目录。独立 Demo 回答“模式角色怎样协作”，教学项目回答“多个模式怎样组合”，高级实验回答“如何证明重构没有改坏行为，以及模式之外还需要哪些工程机制”。

## 实验地图

| 实验 | 建议时间 | 核心问题 | 主要验收证据 |
| --- | ---: | --- | --- |
| [CheckoutRefactoringKata](CheckoutRefactoringKata/README.md) | 4～7 天 | 怎样从职责混乱但行为正确的结账代码，小步重构出 Strategy、Chain、State、Facade | 特征测试与 Starter/Reference 等价性测试 |
| [ReliableCheckout](ReliableCheckout/README.md) | 2～3 周 | 怎样用事务、幂等、Outbox、重试、状态机和故障注入保护结账不变量 | 真实 HTTP + SQLite 集成测试中的五个验收场景 |

两个实验均继承仓库根目录的 .NET 10 / C# 14 配置。

## 实验一：坏代码到设计模式

不要先打开 `Reference` 照抄。按下面顺序推进：

```text
坏代码
  -> 用特征测试锁定已有行为
  -> Strategy 隔离价格变化
  -> Chain of Responsibility 固定校验顺序与短路
  -> State 保护 Draft -> Validated -> Paid -> Completed
  -> Facade 收拢调用入口
  -> 用差分/等价性测试比较重构前后结果与轨迹
```

先运行基线：

```powershell
dotnet test labs/CheckoutRefactoringKata/Tests/CheckoutRefactoringKata.Tests.csproj -c Release
dotnet run --project labs/CheckoutRefactoringKata/Starter/CheckoutRefactoringKata.Starter.csproj -c Release -- success
dotnet run --project labs/CheckoutRefactoringKata/Reference/CheckoutRefactoringKata.Reference.csproj -c Release -- success
```

阶段结束时必须满足：

- Starter 的特征测试持续通过；
- 相同请求和相同支付响应在两个实现中产生相同收据、失败信息和完整轨迹；
- 校验失败不调用支付，支付失败不保存收据；
- 能说明四个模式各自隔离的变化，以及至少一个不值得使用模式的简单场景。

完整红—绿—重构步骤、每阶段验收和反思问题见 [工坊讲义](CheckoutRefactoringKata/README.md)。

## 实验二：2～3 周生产化毕业路线

### 第 1 周：HTTP、幂等与原子库存

1. 运行 API 和测试，画出 `POST /orders` 到 SQLite 的时序图；
2. 追踪 `Idempotency-Key`、请求指纹和原订单重放；
3. 对比“先查再扣”与条件 `UPDATE`，解释两个买家为什么不能同时买走最后一件库存；
4. 验收重复提交与并发抢购场景。

### 第 2 周：Transactional Outbox、重试与恢复

1. 追踪订单、支付初态和 `PaymentRequested` 如何在同一事务中提交；
2. 阅读 `OutboxDispatcher`、指数退避和 `ManualClock`；
3. 注入首次投递失败，推进手动时钟后恢复；
4. 注入 handler 完成后、Outbox 标记前崩溃，验证幂等消费者阻止二次支付。

### 第 3 周（推荐）：回调、状态机与生产差距

1. 追踪回调式旧支付 SDK 如何经 Adapter 转为 Task；
2. 验证支付回调的重复、乱序、身份不匹配和非法状态转换；
3. 补一个故障或恢复测试，再完成上线前差距评审；
4. 说明 State、Facade、Adapter 保护的边界，以及事务、幂等、Outbox 不能被 GoF 模式替代的原因。

运行毕业项目：

```powershell
dotnet restore labs/ReliableCheckout/ReliableCheckout.slnx
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release
dotnet run --project labs/ReliableCheckout/ReliableCheckout.Api --urls http://localhost:5188
```

### 五个必过验收场景

1. **重复提交：** 相同 Key 与相同请求返回原订单，只扣一次库存；相同 Key 携带不同请求返回冲突。
2. **并发抢购：** 两个 HTTP 请求争抢最后一件商品，恰好一个成功，库存永不为负。
3. **重复与乱序回调：** 早到、重复、成功后又到达的失败回调都不会让订单非法前进或倒退。
4. **Outbox 首次失败后恢复：** 第一次投递确定性失败，记录尝试次数；推进手动时钟后重试成功。
5. **处理后崩溃重放：** handler 已完成但 Outbox 尚未标记时发生故障，重放不会创建第二次支付请求。

这些场景对应 [ReliableCheckout 自动化测试](ReliableCheckout/README.md#自动化测试)，它们测试的是业务不变量，不是类名。

## 完成标准

- 两个实验的全部测试通过；
- 能从一次失败现象追踪到负责恢复的边界；
- 能区分“模式使职责可替换”和“事务/幂等使结果可靠”；
- 能指出 ReliableCheckout 仍未解决的生产问题，例如签名验证、认证、限流、迁移、多实例租约、死信和可观测性；
- 能用自己的话说明为什么“至少一次投递 + 端到端幂等”比空泛承诺“恰好一次”更可信。
