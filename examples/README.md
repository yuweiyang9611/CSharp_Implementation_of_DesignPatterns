# 设计模式组合实战

`src/DesignPatterns` 中的示例用于一次看清一个模式；这里的三个项目用于学习另一件事：当业务流程跨越创建、结构和行为三个维度时，怎样让多个模式协作而不互相污染。

## 项目与模式覆盖

| 项目 | 业务主线 | 重点模式 |
| --- | --- | --- |
| [OnlineStore](OnlineStore/README.md) | 从购物车到支付、履约和通知的完整结算流程 | Builder、Factory Method、Strategy、Chain of Responsibility、State、Observer、Facade |
| [SmartHome](SmartHome/README.md) | 多厂商设备接入、分组控制、联动、撤销和场景恢复 | Singleton、Adapter、Bridge、Composite、Proxy、Command、Mediator、Memento |
| [DocumentWorkflow](DocumentWorkflow/README.md) | 报表模板复制、条件筛选、合规检查和多渠道发布 | Abstract Factory、Prototype、Decorator、Flyweight、Interpreter、Iterator、Template Method、Visitor |

三个项目合计覆盖全部 GoF 23 种模式。某个模式只在最能说明其价值的业务中担任主角，避免为了凑数量重复制造相似的类。

## 一键运行

运行三个端到端业务故事：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1
```

运行三个项目的确定性自检：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1 -SelfTest
```

也可以单独运行：

```powershell
dotnet run --project examples/OnlineStore
dotnet run --project examples/SmartHome
dotnet run --project examples/DocumentWorkflow
```

## 建议学习方式

1. 先运行默认场景，只看业务输出，写下你认为系统中存在的变化点。
2. 阅读项目 README 的业务流程和模式协作图，再从 `Program.cs` 进入场景类或系统工厂找到组合根。
3. 按“入口用例 -> 领域对象 -> 模式角色”的顺序阅读，不要按文件名字母顺序阅读。
4. 运行 `--self-test`，再故意破坏一条业务规则，观察哪个测试最先暴露问题。
5. 完成 README 中的渐进练习；每次只引入一个新变化，不要一次重写整个项目。
6. 最后尝试删除一个模式。如果删除后代码仍更清晰，说明该模式在你的版本里可能属于过度设计。

## 与独立示例的关系

独立示例回答“这个模式的角色如何协作”，实战项目回答“业务为什么需要这些角色”。学习时应来回对照：先用独立示例建立清晰模型，再在实战项目中观察边界、取舍、错误处理和模式组合顺序。
