# 从这里开始：C# 设计模式学习路线

这是仓库的课程入口。不要一开始通读所有文件：先选一条路线，运行代码，再带着输出阅读对应章节。

## 先做 2 分钟环境检查

没有本地 .NET 10 时，可先 [在 GitHub Codespaces 中打开本项目](https://codespaces.new/yuweiyang9611/CSharp_Implementation_of_DesignPatterns?quickstart=1)。预配置容器会自动还原、构建并生成学习站；Codespaces 可能消耗 GitHub 使用额度。

在仓库根目录执行：

```powershell
dotnet --version
dotnet build DesignPatterns.sln --configuration Release
```

项目目标框架与 CI 使用 .NET 10，语言版本固定为 C# 14。`global.json` 允许使用已安装的 .NET 10 最新 feature band；`dotnet --version` 应以 `10.` 开头。构建结尾应出现 `Build succeeded.`（中文环境可能显示“生成成功”）并且没有错误。

| 你现在有多少时间 | 选择路线 | 完成后能做到什么 |
| --- | --- | --- |
| 约 30 分钟 | [路线 A：快速体验](#路线-a30-分钟快速体验) | 看懂 Adapter 如何从独立角色进入真实业务 |
| 约 6 小时 | [路线 B：入门闭环](#路线-b6-小时入门闭环) | 区分 5 个常用模式，并完成一次小型业务扩展 |
| 10 周，每周 4～6 小时 | [路线 C：核心课程](#路线-c10-周完整课程) | 跑完 23 种模式和三个组合项目，建立模式取舍能力 |
| 再用 3～4 周 | [路线 D：高级实验](#路线-d34-周高级实验) | 完成安全重构工坊和生产化结账毕业项目 |

完整查询入口：[23 种模式索引](docs/模式索引.md)｜[基础学习指导](docs/CSharp设计模式学习指南.md)｜[实战学习指导](docs/设计模式实战项目学习指南.md)｜[高级实验索引](labs/README.md)

## 路线 A：30 分钟快速体验

目标：用同一个 Adapter 模式完成“看角色 → 看业务 → 用自检证明行为”的最短闭环。

### 1. 运行独立 Demo（约 5 分钟）

```powershell
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- adapter
```

关键成功输出：

```text
=== Adapter / 适配器模式 [Structural] ===
旧设备原始读数: 41.0 °F
适配后的读数: 5.0 °C
冷链检查结果: 温度正常
```

### 2. 带着输出读代码（约 10 分钟）

依次阅读：

1. [Adapter 教程章节](docs/CSharp设计模式学习指南.md#32-第-2-章-adapter适配器填平接口与单位差异)；
2. [独立 AdapterDemo 源码](src/DesignPatterns/Structural/AdapterDemo.cs)；
3. [SmartHome 中的旧空调 Adapter](examples/SmartHome/Patterns/Structural/LegacyAirConditionerAdapter.cs)；
4. [SmartHome 的 Adapter 讲解](examples/SmartHome/README.md#2-adapter隔离旧接口差异)。

### 3. 做一个追踪练习（约 10 分钟）

不用改代码，沿调用链回答：

- Target 是哪个接口？Adaptee 是哪个旧类？Adapter 是哪个类？
- `SetSetting(24)` 为什么会变成旧接口的 `75°F`？
- 为什么 Command、Composite 和 Mediator 不需要知道空调使用华氏温度？

然后让项目自检回答你的判断：

```powershell
dotnet run --project examples/SmartHome/DesignPatterns.TeachingProjects.SmartHome.csproj --configuration Release --no-build -- --self-test
```

关键成功输出是 `[PASS] Adapter 把 24°C 转成旧接口的 75°F`，结尾为 `结果：13/13 通过`。

### 结束标准

- 能用一句话说清 Adapter 隔离的是“接口/协议差异”，而不是业务流程；
- 能指出独立 Demo 与 SmartHome 中四个角色的实际类型；
- 两条运行命令都返回退出码 `0`。

## 路线 B：6 小时入门闭环

目标：先掌握 Iterator、Adapter、Strategy、Decorator、State，再观察它们怎样进入 OnlineStore 的完整业务流程。

### 1. 建立五个最小模型（约 2 小时）

```powershell
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- iterator
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- adapter
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- strategy
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- decorator
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- state
```

每运行一个模式，就在 [模式索引](docs/模式索引.md) 中打开它的“独立源码”和“教程”链接。重点阅读：

- [Iterator](docs/CSharp设计模式学习指南.md#31-第-1-章-iterator迭代器一个一个遍历)：遍历规则由谁拥有；
- [Adapter](docs/CSharp设计模式学习指南.md#32-第-2-章-adapter适配器填平接口与单位差异)：差异在哪里被翻译；
- [Strategy](docs/CSharp设计模式学习指南.md#62-第-10-章-strategy策略整体替换算法)：算法何时被选择；
- [Decorator](docs/CSharp设计模式学习指南.md#72-第-12-章-decorator装饰器按顺序叠加职责)：包装顺序为何改变结果；
- [State](docs/CSharp设计模式学习指南.md#103-第-19-章-state状态让当前状态对象决定行为)：当前状态如何限制命令。

### 2. 进入 OnlineStore（约 2 小时）

先读 [OnlineStore 业务故事和阅读顺序](examples/OnlineStore/README.md)，再运行默认场景与失败路径自检：

```powershell
dotnet run --project examples/OnlineStore/DesignPatterns.TeachingProjects.OnlineStore.csproj --configuration Release --no-build
dotnet run --project examples/OnlineStore/DesignPatterns.TeachingProjects.OnlineStore.csproj --configuration Release --no-build -- --self-test
```

阅读顺序固定为：

1. [DemoScenario.cs](examples/OnlineStore/Application/DemoScenario.cs) 看业务输入；
2. [CheckoutFacade.cs](examples/OnlineStore/Application/CheckoutFacade.cs) 看总流程；
3. [PricingStrategies.cs](examples/OnlineStore/Pricing/PricingStrategies.cs) 看 Strategy；
4. [OrderStates.cs](examples/OnlineStore/States/OrderStates.cs) 看 State；
5. [SelfTestRunner.cs](examples/OnlineStore/Application/SelfTestRunner.cs) 看行为证据。

自检成功时结尾为 `SELF-TEST PASS: 5/5`。

### 3. 完成一个有验收条件的练习（约 2 小时）

给 OnlineStore 增加“商品小计满 300 减 40”的定价策略：

- 只把价格算法放进新的 Strategy，不在 `CheckoutFacade` 中复制计价公式；
- 明确它是否与 VIP 九折叠加，并用代码固定这个决定；
- 为小计 `299`、`300`、`500` 各增加一条确定性自检；
- 保证原有五条 OnlineStore 自检继续通过。

完成后执行：

```powershell
dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release --no-build
dotnet run --project examples/OnlineStore/DesignPatterns.TeachingProjects.OnlineStore.csproj --configuration Release -- --self-test
```

第一条应输出 `烟雾测试通过：23 个模式均可重复运行`；第二条应包含原有五个 `PASS`，并包含你新增的三条边界检查。

### 结束标准

- 能用“变化轴”解释 Iterator、Adapter、Strategy、Decorator、State，而不是只背类图；
- 能从 `DemoScenario` 追踪到定价、订单状态和自检；
- 新策略的三个边界用例通过，原有 23 个 Demo 与五条 OnlineStore 自检没有回归；
- 能解释为什么价格算法属于 Strategy，而订单生命周期属于 State。

## 路线 C：10 周完整课程

目标：六周掌握 23 个独立模式，四周训练模式组合、失败路径与取舍。

### 第 1～6 周：独立模式

按 [六周学习路线](docs/CSharp设计模式学习指南.md#17-六周学习路线与阶段项目) 推进。先列目录，再按索引中的 Runner key 运行当天模式：

```powershell
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --list
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- <runner-key>
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --category Creational
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --category Structural
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- --category Behavioral
```

`<runner-key>` 必须替换为 [模式索引](docs/模式索引.md) 中的真实键，例如 `factory-method`；不要原样输入尖括号。

每周练习与交付物：

| 周次 | 模式范围 | 必做练习/交付物 |
| --- | --- | --- |
| 1 | Iterator～Factory Method | 给一个第三方 API 画 Adapter 边界，解释接口、抽象类与组合的选择 |
| 2 | Singleton～Abstract Factory | 做报表配置器，明确 Prototype 深复制边界，避免全局可变状态 |
| 3 | Bridge～Decorator | 做“告警级别 × 渠道”通知系统，并用数字证明 Decorator 顺序 |
| 4 | Visitor～Mediator | 增加统计 Visitor、验证链和 Facade，提交一次请求时序图 |
| 5 | Observer～State | 写状态转换表、退订测试、非法转换测试和恢复方案 |
| 6 | Flyweight～Interpreter | 证明缓存收益，限制 DSL 输入；删除一个没有真实变化压力的模式 |

每周从 [练习提示](docs/CSharp设计模式学习指南.md#18-练习提示与参考方向) 选至少一题，并执行：

```powershell
dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release
```

### 第 7～10 周：三个实战项目

按 [四周实战计划](docs/设计模式实战项目学习指南.md#8-四周学习计划) 推进；项目入口和覆盖关系见 [实战项目索引](examples/README.md)。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1 -SelfTest
```

练习顺序：

1. 第 7 周：每个项目画一条业务时序，完成一个最小练习；
2. 第 8 周：分别移除 Strategy、Mediator、Decorator 做对照，记录修改文件数；
3. 第 9 周：补支付失败、权限失败、解析失败，以及撤销/恢复/非法转换测试；
4. 第 10 周：从[三个可选扩展方向](docs/设计模式实战项目学习指南.md#7-三个可选扩展方向)选择一个，只在已有变化压力处引入模式。

### 最终验证与结束标准

```powershell
pwsh -File scripts/verify.ps1 -SkipPdf
powershell -ExecutionPolicy Bypass -File scripts/export-all-guides.ps1
```

五份课程包位于：

```text
output/pdf/CSharp设计模式学习指南.pdf
output/pdf/设计模式实战项目学习指南.pdf
output/pdf/CSharp-Design-Patterns-Learning-Path.pdf
output/pdf/Checkout-Refactoring-Workshop.pdf
output/pdf/Reliable-Checkout-Graduation-Project.pdf
```

完成课程意味着：

- `--list` 恰好列出 23 种模式，独立烟雾测试全部通过；
- 三个项目默认故事和自检全部通过（基线共 33 项）；
- 至少一个教学项目扩展有设计说明、失败路径和可重复自检；
- 能回答基础指南的[毕业验收问题](docs/CSharp设计模式学习指南.md#毕业验收问题)；
- 能指出自己删除或拒绝使用的一个模式，并说明更简单的替代方案；
- 五份课程包都能成功生成 PDF。

## 路线 D：3～4 周高级实验

这条路线建立在路线 C 之上。它不再增加要背的模式，而是训练两个更接近真实工作的能力：安全改变遗留代码，以及在故障和并发下保护业务不变量。

### 第 1 周：坏代码到设计模式重构工坊

打开 [CheckoutRefactoringKata 工坊讲义](labs/CheckoutRefactoringKata/README.md)，严格按以下顺序推进：

```text
坏代码
  -> 特征测试锁定当前行为
  -> Strategy 隔离计价
  -> Chain of Responsibility 组织校验和短路
  -> State 保护订单生命周期
  -> Facade 收拢用例入口
  -> 等价性测试证明重构前后行为一致
```

先运行基线，不要先照抄 Reference：

```powershell
dotnet test labs/CheckoutRefactoringKata/Tests/CheckoutRefactoringKata.Tests.csproj -c Release
dotnet run --project labs/CheckoutRefactoringKata/Starter/CheckoutRefactoringKata.Starter.csproj -c Release -- success
dotnet run --project labs/CheckoutRefactoringKata/Reference/CheckoutRefactoringKata.Reference.csproj -c Release -- success
```

每提取一个职责，都重新运行特征测试和等价性测试。结束时应能证明：校验失败不会调用支付、支付失败不会保存收据、成功和失败的收据/错误/轨迹均与 Starter 一致。

### 第 2～4 周：ReliableCheckout 生产化毕业项目

打开 [ReliableCheckout 完整讲义](labs/ReliableCheckout/README.md)。建议按 2～3 周推进：

1. **第 1 周：HTTP、幂等与库存。** 追踪 `Idempotency-Key`、请求指纹、SQLite immediate transaction 和条件更新；证明重试不重复扣库存、并发不超卖。
2. **第 2 周：Outbox、重试与恢复。** 追踪同事务写入、后台投递、指数退避、手动时钟和幂等消费者；注入投递前后故障。
3. **第 3 周（推荐）：回调与状态机。** 验证支付 Adapter、重复/乱序回调、非法状态转换，并完成“离真正生产还有什么”的差距评审。

运行与测试：

```powershell
dotnet restore labs/ReliableCheckout/ReliableCheckout.slnx
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release
dotnet run --project labs/ReliableCheckout/ReliableCheckout.Api --urls http://localhost:5188
```

五个毕业验收场景必须全部通过：

1. 相同幂等 Key 重放只生成一张订单、只扣一次库存；同 Key 不同请求发生冲突；
2. 两个买家并发抢最后一件商品时恰好一个成功，库存不为负；
3. 早到、重复和成功后到达的失败回调不会让状态非法前进或倒退；
4. Outbox 首次投递失败后，推进手动时钟可以重试成功；
5. handler 完成但 Outbox 标记前崩溃，重放不会产生第二次支付请求。

完成高级路线后，你应能明确区分：Strategy、Chain、State、Facade 负责组织变化；事务、幂等、Outbox 和重试负责在故障下保护结果。详细阶段表见 [高级实验索引](labs/README.md)。

## 独立 Demo 与实战项目怎样切换

| 学习状态 | 去哪里 | 运行方式 |
| --- | --- | --- |
| 第一次接触模式、角色混乱 | `src/DesignPatterns/*/*Demo.cs` | 用一个 Runner key 运行，例如 `... -- adapter` |
| 已看懂角色，想知道业务价值 | `examples/OnlineStore`、`SmartHome`、`DocumentWorkflow` | 单独运行项目或用教学项目脚本 |
| 想练习安全重构，而不是照着类图新建代码 | `labs/CheckoutRefactoringKata` | 运行特征测试、逐步重构、最后做等价性验证 |
| 想验证并发、重试和崩溃后的业务不变量 | `labs/ReliableCheckout` | 运行真实 HTTP + SQLite 集成测试 |
| 实战中分不清两个模式 | 回到两个独立 Demo，对比输入、输出和变化轴 | 分别用两个 Runner key 运行 |
| 修改了业务规则，想防回归 | 项目的 `SelfTestRunner`，再跑全部 SmokeTests | 使用 `-- --self-test` 与烟雾测试 |

运行单个独立 Demo 的正确模板：

```powershell
dotnet run --project src/DesignPatterns.Runner --configuration Release --no-build -- <runner-key>
```

运行单个实战项目的正确模板：

```powershell
dotnet run --project examples/OnlineStore/DesignPatterns.TeachingProjects.OnlineStore.csproj --configuration Release
dotnet run --project examples/OnlineStore/DesignPatterns.TeachingProjects.OnlineStore.csproj --configuration Release -- --self-test
```

核心区别：独立 Demo 回答“角色怎样协作”，实战项目回答“业务变化为什么值得这些角色”。推荐循环是：**独立 Demo → 实战调用链 → 自检 → 回到 Demo 复盘取舍**。

## 预期成功输出速查

| 命令 | 稳定成功标记 |
| --- | --- |
| `dotnet build DesignPatterns.sln -c Release` | `Build succeeded.` / “生成成功”，0 个错误 |
| Runner `-- --list` | 编号 1～23，最后一项 `interpreter` |
| `tests/DesignPatterns.SmokeTests` | `烟雾测试通过：23 个模式均可重复运行` |
| OnlineStore `-- --self-test` | `SELF-TEST PASS: 5/5` |
| SmartHome `-- --self-test` | `结果：13/13 通过` |
| DocumentWorkflow `-- --self-test` | `SELF-TEST PASSED: 15/15` |
| CheckoutRefactoringKata tests | `Passed: 22` / “通过: 22” |
| ReliableCheckout tests | 五个 HTTP + SQLite 验收场景全部通过 |
| `export-all-guides.ps1` | 生成 5 份 PDF 及对应 HTML 预览 |

## 常见故障

### PowerShell 找不到命令或脚本被阻止

- `dotnet` 无法识别：执行 `where.exe dotnet` 与 `dotnet --list-sdks`；安装 .NET 10 SDK 后关闭并重新打开终端。
- 当前目录错误：先 `cd` 到包含 `DesignPatterns.sln` 的仓库根目录。
- 脚本执行策略阻止运行：使用文档中的 `powershell -ExecutionPolicy Bypass -File ...`，它只对该进程生效。
- 在 PowerShell 7 中也可把命令开头的 `powershell` 换成 `pwsh`。

### SDK 或编译失败

- `NETSDK1045` 通常表示当前 SDK 不支持 `net10.0`；请确认 `dotnet --list-sdks` 中存在 .NET 10 SDK。
- 使用 `--no-build` 报缺少产物时，先执行 `dotnet build DesignPatterns.sln -c Release`，或暂时去掉 `--no-build`。
- 传给示例程序的参数必须放在独立的 `--` 后面，例如 `... -- --self-test`；Runner key 则是 `... -- adapter`。
- 修改练习后先看第一条编译错误，不要用关闭 Nullable 或“警告视为错误”掩盖问题。

### PDF 生成失败

```powershell
powershell -ExecutionPolicy Bypass -File scripts/export-all-guides.ps1
```

- 需要本机安装 Microsoft Edge、Google Chrome 或 Chromium；找不到浏览器时 HTML 仍会生成，终端会显示其路径。
- 如果 PDF 正被阅读器占用，先关闭文件再重试。
- 生成结果在 `output/pdf/`；它是可再生输出，不要求提交到 Git。
- 只想验证代码时使用 `scripts/verify.ps1 -SkipPdf`，不要让浏览器问题阻断代码练习。

下一步：[打开 23 种模式索引](docs/模式索引.md)，选择第一个 Runner key。
