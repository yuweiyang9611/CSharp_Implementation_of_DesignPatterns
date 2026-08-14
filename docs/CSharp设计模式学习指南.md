# C# 设计模式学习指导：从“读懂角色”到“写出可演进代码”

> 这是一份面向 .NET 10 / C# 14 的 GoF 23 种设计模式实战教程。章节次序沿用《图解设计模式》的学习路径，但所有讲解、业务场景和代码均针对现代 C# 重新设计。完整示例位于本仓库，可编译、可逐个运行，并由烟雾测试与正式 xUnit 测试统一验证。

**适合读者：** 已掌握 C# 类、接口、继承、泛型和委托，希望系统学习面向对象设计的人。
**建议节奏：** 每章 45-90 分钟；先预测输出，再运行示例，最后完成一个改造练习。
**目标框架：** .NET 10；**语言版本：** C# 14。独立模式 Demo 不依赖第三方 NuGet 包，正式测试和生产化实验使用明确锁定的教学依赖。
**本指南版本：** 1.1（2026-07-17）。

## 0. 先把项目跑起来

### 0.1 环境与仓库结构

安装 .NET 10 SDK。在仓库根目录执行：

```powershell
dotnet --version
dotnet build DesignPatterns.sln --configuration Release
dotnet run --project src/DesignPatterns.Runner -- --list
dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release
```

仓库按“领域代码、运行入口、验证、文档工具”分开：

```text
src/DesignPatterns/                 23 种模式的完整实现
  Creational/                       创建型：5 种
  Structural/                       结构型：7 种
  Behavioral/                       行为型：11 种
src/DesignPatterns.Runner/          可选择模式的命令行入口
tests/DesignPatterns.SmokeTests/     无第三方依赖的可执行烟雾测试
tests/TeachingProjects.Tests/        三个教学项目的 xUnit 行为测试
examples/                            OnlineStore、SmartHome、DocumentWorkflow
labs/CheckoutRefactoringKata/        坏代码到设计模式重构工坊
labs/ReliableCheckout/               生产化结账毕业项目
tools/GuideExporter/                Markdown -> HTML -> PDF 工具
scripts/export-guide.ps1            一键导出 PDF
scripts/export-all-guides.ps1       一键导出五份课程包
docs/CSharp设计模式学习指南.md       本指南源文件
output/pdf/                          生成的 HTML 与 PDF
```

### 0.2 三种运行方式

列出全部模式：

```powershell
dotnet run --project src/DesignPatterns.Runner -- --list
```

只运行一个模式；参数是列表中的 `Key`：

```powershell
dotnet run --project src/DesignPatterns.Runner -- iterator
dotnet run --project src/DesignPatterns.Runner -- factory-method
dotnet run --project src/DesignPatterns.Runner -- chain-of-responsibility
```

运行全部模式，观察它们各自解决的问题：

```powershell
dotnet run --project src/DesignPatterns.Runner -- --all
```

### 0.3 一键生成 PDF

Windows 下执行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/export-guide.ps1
```

或双击、直接调用：

```bat
scripts\export-guide.cmd
```

导出器本身也是一个零 NuGet 依赖的 C# 项目。它先把 Markdown 转为带打印样式的 HTML，再调用本机 Microsoft Edge、Chrome 或 Chromium 的无头打印功能。默认输出：

```text
output/pdf/CSharp设计模式学习指南.html
output/pdf/CSharp设计模式学习指南.pdf
```

若只想生成 HTML 预览：

```powershell
dotnet run --project tools/GuideExporter -- --html-only
```

生成完整五份课程包：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/export-all-guides.ps1
```

```text
output/pdf/CSharp设计模式学习指南.pdf
output/pdf/设计模式实战项目学习指南.pdf
output/pdf/CSharp-Design-Patterns-Learning-Path.pdf
output/pdf/Checkout-Refactoring-Workshop.pdf
output/pdf/Reliable-Checkout-Graduation-Project.pdf
```

### 0.4 每一章应该怎样学

不要把模式背成一句定义。对每一章按下面六步操作：

1. **先找变化轴。** 哪部分经常变，哪部分应该稳定？模式通常是在二者之间放置一个边界。
2. **再认角色。** 不先背类名，先问每个对象“负责什么、不负责什么”。
3. **预测输出。** 打开对应 `.cs` 文件，只读 `Run()` 和客户端代码，先写下你预计发生的调用顺序。
4. **运行与断点。** 用 Runner 执行该模式，在创建、委托和状态变化的位置打断点。
5. **故意破坏。** 去掉一个接口或中间对象，看看客户端为什么重新与具体实现耦合。
6. **完成练习。** 至少做一道“增加新变化而不修改旧代码”的练习；这才是在验证设计，而不是验证语法。

> 判断是否真正理解：你能否不用模式名称，先说明问题、约束、变化方向和代价，然后自然推导出相似结构？如果只能画类图，还不能解释为什么值得多出这些类型，就还没有学会。

## 1. 阅读地图：23 种模式不是 23 个孤岛

原书按认知难度和“变化发生在哪里”组织章节。本指南保留这条路线，而不是按创建型、结构型、行为型简单排序。

| 原学习单元 | 章节 | 模式 Key | 首要问题 | 现代 C# 连接点 |
| --- | --- | --- | --- | --- |
| 适应设计模式 | 1 Iterator | `iterator` | 不暴露集合结构如何遍历 | `IEnumerable<T>`、`yield return` |
| 适应设计模式 | 2 Adapter | `adapter` | 让旧接口适配新客户端 | 组合、接口、扩展方法边界 |
| 交给子类 | 3 Template Method | `template-method` | 固定流程、开放步骤 | 抽象类、受保护成员、委托 |
| 交给子类 | 4 Factory Method | `factory-method` | 把创建的决定延迟出去 | 虚工厂方法、泛型约束、DI |
| 生成实例 | 5 Singleton | `singleton` | 控制实例数量与生命周期 | 静态初始化、`Lazy<T>`、DI 生命周期 |
| 生成实例 | 6 Prototype | `prototype` | 从样板复制复杂对象 | `record`、`with`、深浅复制 |
| 生成实例 | 7 Builder | `builder` | 分步骤构造并保持合法性 | Fluent API、`required`、不可变对象 |
| 生成实例 | 8 Abstract Factory | `abstract-factory` | 创建相互匹配的一族对象 | 接口族、配置、DI 容器 |
| 分开考虑 | 9 Bridge | `bridge` | 两个变化维度独立扩展 | 组合优于继承、泛型或接口桥 |
| 分开考虑 | 10 Strategy | `strategy` | 整体替换算法 | 接口、委托、lambda |
| 一致处理 | 11 Composite | `composite` | 单个与组合对象统一处理 | 树、递归、`IReadOnlyList<T>` |
| 一致处理 | 12 Decorator | `decorator` | 运行时叠加职责 | 流、ASP.NET Core 中间件、包装器 |
| 访问数据结构 | 13 Visitor | `visitor` | 给稳定结构增加新操作 | 双分派、模式匹配替代方案 |
| 访问数据结构 | 14 Chain of Responsibility | `chain-of-responsibility` | 让多个处理者依次尝试 | 中间件、验证管线 |
| 简单化 | 15 Facade | `facade` | 为复杂子系统提供窄入口 | 应用服务、模块边界 |
| 简单化 | 16 Mediator | `mediator` | 集中协调多对象交互 | 消息总线、对话框协调器 |
| 管理状态 | 17 Observer | `observer` | 状态变化后通知订阅者 | 事件、`IObservable<T>`、解绑 |
| 管理状态 | 18 Memento | `memento` | 在不泄露内部结构时保存状态 | 不可变快照、撤销栈 |
| 管理状态 | 19 State | `state` | 让行为随状态改变 | 状态对象、有限状态机 |
| 避免浪费 | 20 Flyweight | `flyweight` | 共享大量重复的内在状态 | 缓存、值对象、对象池辨析 |
| 避免浪费 | 21 Proxy | `proxy` | 在访问真实对象前后加控制 | 延迟加载、缓存、权限、远程代理 |
| 用类表示 | 22 Command | `command` | 把请求变成可排队、撤销的对象 | 一等函数、队列、撤销/重做 |
| 用类表示 | 23 Interpreter | `interpreter` | 表示并解释小型语言的语法 | AST、组合、解析器边界 |

### 1.1 三类模式的底层问题

**创建型**关心“对象由谁创建、何时创建、创建哪一种”；**结构型**关心“已有对象怎样连接”；**行为型**关心“运行时职责怎样流动”。分类有助于检索，但真实系统经常组合使用。例如一个报表系统可能用 Abstract Factory 创建同一主题的组件，用 Composite 表示报表树，用 Visitor 导出格式，再用 Strategy 选择压缩算法。

### 1.2 模式的共同语言

每章都会反复出现四个词：

- **Client（客户端）**：使用结构的一方。它应依赖稳定抽象，而不是知道全部细节。
- **Abstraction（抽象）**：客户端看到的协议，通常是接口、抽象类或委托签名。
- **Concrete（具体实现）**：真正完成工作的类型。模式不是为了消灭具体类型，而是限制知道它们的人。
- **Context（上下文）**：持有策略、状态、命令或责任链并驱动协作的对象。

“接口”在不同语境有两层意思：一是公开 API 的总体边界，二是 C# 的 `interface` 类型。API 不一定由 `interface` 实现；一个设计良好的具体类同样可以提供稳定 API。

## 2. C# 版必须先补齐的语言工具

### 2.1 接口、抽象类和委托怎样选

接口最适合表达“能力或协议”，允许多个不相关类型实现；抽象类适合共享不变量、模板流程和受保护的扩展点；委托适合只有一个行为、无需保存额外对象状态的策略。

```csharp
public interface IPricePolicy
{
    decimal Calculate(decimal subtotal);
}

public abstract class ImportWorkflow
{
    public void Execute(Stream source)
    {
        Validate(source);
        Import(source);
        WriteAuditLog();
    }

    protected abstract void Validate(Stream source);
    protected abstract void Import(Stream source);
    protected virtual void WriteAuditLog() { }
}

Func<decimal, decimal> discount = subtotal => subtotal * 0.9m;
```

选择标准不是“哪个更高级”，而是变化是否需要一个有名称、有状态、可被多处依赖的角色。只有一行计算时优先委托；需要多个方法形成协议时用接口；流程骨架必须被基类守住时用抽象类。

### 2.2 组合优于继承，但不是禁止继承

继承把类型关系和代码复用绑定在一起，编译期就固定；组合让对象在运行时协作，更容易替换和测试。Adapter、Bridge、Decorator、Strategy 都主要利用组合。Template Method 和 Factory Method 则有意利用继承，因为它们的变化点就是子类扩展。

```csharp
public sealed class CheckoutService(IPricingStrategy pricing)
{
    public decimal Checkout(decimal subtotal) => pricing.Calculate(subtotal);
}
```

这个类不继承任何“折扣基类”，只持有协议。测试时传入一个假实现即可。

### 2.3 不可变性会让很多模式更安全

C# 的 `record`、`init` 和 `required` 能减少 Builder、Prototype、Memento 中的意外共享：

```csharp
public sealed record ReportOptions
{
    public required string Title { get; init; }
    public string Theme { get; init; } = "Light";
    public IReadOnlyList<string> Columns { get; init; } = [];
}

var draft = new ReportOptions { Title = "Weekly" };
var dark = draft with { Theme = "Dark" };
```

`with` 默认仍是浅复制。如果属性里放可变 `List<T>`，两个记录可能共享同一个列表。Prototype 和 Memento 章节会专门处理这个陷阱。

### 2.4 依赖注入与设计模式是什么关系

依赖注入容器负责组装对象图，不替代模式本身。容器可以选择 Strategy、配置 Abstract Factory、管理 Singleton 生命周期，但业务类仍需有清晰职责。不要在领域代码里到处调用全局 `ServiceProvider`；那会把依赖重新藏起来，变成 Service Locator。

### 2.5 异步、并发和资源释放

经典模式早于 `async/await`。迁移到现代 C# 时要主动回答：

- Strategy 或 Command 是否需要返回 `Task` 并接收 `CancellationToken`？
- Observer 的通知是顺序、并发还是“至少一次”？失败是否阻止其他订阅者？
- Singleton 保存的状态是否线程安全？
- Decorator 是否正确传递取消、异常和 `IAsyncDisposable`？
- Memento 捕获状态期间，其他线程能否修改原对象？

模式只提供结构，不自动解决这些运行时语义。

### 2.6 类图和时序图应该看什么

类图回答“谁依赖谁、谁实现谁”；时序图回答“一次请求中谁先调用谁”。学习时不必追求工具格式，先能画出下面两种最小图即可：

```text
Client --> Abstraction <-- ConcreteImplementation

Client        Context        Strategy
  | Execute()    |              |
  |------------->| Calculate()  |
  |              |------------->|
  |              |<-------------|
  |<-------------|              |
```

箭头不是装饰。每画一条依赖，都问：如果右侧类型发生变化，左侧是否必须修改？模式的价值通常就藏在这条传播路径里。

### 2.7 .NET 10 与 C# 14 怎样进入本课程

仓库只在根目录的 `Directory.Build.props` 声明 `net10.0` 和 C# 14，各项目不得再写 `LangVersion=latest`。`global.json` 固定 .NET 10 SDK 主版本并允许滚动到同一主版本的新 feature band，使本机和 CI 使用同一代编译器。

C# 14 语法只在能让边界更清楚时使用。例如 `field` 可以在不手写后备字段的情况下守住配置不变量，空条件赋值可以避免为了一个可选观察者展开无关分支：

```csharp
public sealed class RetryOptions
{
    public int MaxAttempts
    {
        get => field;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

observer?.LastError = null;
```

新语法不会自动产生好设计。Strategy 仍要解释算法为何变化，State 仍要守住合法转换，Observer 仍要定义失败和投递语义。重构工坊会刻意让模式选择先于语法选择，生产化毕业项目再把异步、持久化和故障恢复加入同一条业务链。

## 3. 适应设计模式：先学会隔离“怎么取”和“怎么接”

### 3.1 第 1 章 Iterator（迭代器）——一个一个遍历

**问题场景。** 播放列表内部以后可能从数组改为数据库分页结果，但播放器只想按顺序读取曲目。若客户端直接依赖索引、数组长度或链表节点，容器一改，所有遍历代码都跟着改。

**角色对应。** `Playlist` 是 Aggregate，负责产生迭代器；C# 已用 `IEnumerable<Track>` / `IEnumerator<Track>` 标准化 Iterator；`Track` 是 Element；`foreach` 是 Client。示例还演示了 LINQ 如何继续消费同一遍历协议。

```csharp
private sealed class Playlist : IEnumerable<Track>
{
    private readonly IReadOnlyList<Track> _tracks;

    public IEnumerator<Track> GetEnumerator()
    {
        foreach (var track in _tracks.OrderBy(track => track.Position))
        {
            yield return track;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

`yield return` 让编译器生成状态机；集合无需一次性复制，客户端也看不到内部存储。延迟执行意味着异常通常在枚举时而不是查询创建时发生，而且同一序列可能被重复枚举。对数据库查询尤其要警惕隐含的多次 I/O。

**何时使用。** 自定义容器需要多种遍历方式、惰性序列或隐藏内部结构时使用。普通 `List<T>` 已经有成熟迭代器，不要为了“套模式”再包装一层。异步数据流使用 `IAsyncEnumerable<T>` 和 `await foreach`。

**完整运行。** [IteratorDemo.cs](../src/DesignPatterns/Behavioral/IteratorDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- iterator
```

**练习。** 增加一个只返回收藏曲目的惰性迭代器；再写一个 `IAsyncEnumerable<Track>`，每首曲目之间模拟 50ms 延迟，并正确传递 `CancellationToken`。比较“容器提供多个枚举入口”和“客户端用 LINQ 过滤”的职责差异。

**相关模式。** Composite 常用 Iterator 遍历树；Memento 可以保存当前游标；Visitor 在遍历过程中执行与元素类型有关的操作。

### 3.2 第 2 章 Adapter（适配器）——填平接口与单位差异

**问题场景。** 新监控系统只接受摄氏温度的 `ITemperatureSensor`，遗留网关却暴露华氏度和另一套方法名。直接修改遗留组件风险高，在每个客户端里散落换算又会重复。

```csharp
private interface ITemperatureSensor
{
    decimal ReadCelsius();
}

private sealed class FahrenheitSensorAdapter(LegacyFahrenheitGateway adaptee)
    : ITemperatureSensor
{
    public decimal ReadCelsius()
        => (adaptee.FetchCurrentFahrenheit() - 32m) * 5m / 9m;
}
```

Client 依赖 Target（`ITemperatureSensor`）；Adapter 持有 Adaptee（遗留网关）并转换名称、调用方式和数据单位。这里使用**对象适配器**，因为 C# 只支持单基类继承，而且组合更容易替换和测试。若 Adapter 只是把方法转发出去，它仍然有价值：边界明确地吸收了第三方 API 的变化。

**不要混淆。** Adapter 让两个既有接口协同；Decorator 保持同一接口并增加职责；Facade 提供更高层、更简单的入口；Proxy 保持同一接口并控制访问。

**完整运行。** [AdapterDemo.cs](../src/DesignPatterns/Structural/AdapterDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- adapter
```

**练习。** 让遗留网关偶尔返回无效读数，决定异常转换应该在 Adapter 还是应用服务中完成。再增加一个批量网关，尝试把同步 Target 升级为 `Task<decimal>`；记录这个变化为何会影响整个调用协议，而不只是一个换算公式。

## 4. 交给子类：固定骨架，把一个决定留出去

### 4.1 第 3 章 Template Method（模板方法）——父类守住流程

**问题场景。** CSV 和 JSON 订单导出都要经过“校验、序列化、可选压缩、投递”，只有少数步骤不同。复制两套流程很容易让校验或审计顺序漂移。

```csharp
private abstract class OrderExportTemplate
{
    public IReadOnlyList<string> Export(IReadOnlyList<OrderRow> orders)
    {
        Validate(orders);                    // 固定步骤
        var payload = Serialize(orders);     // 必须扩展的步骤
        if (ShouldCompress)                  // Hook
        {
            payload = Compress(payload);
        }

        return Deliver(payload);
    }

    protected abstract string Serialize(IReadOnlyList<OrderRow> orders);
    protected virtual bool ShouldCompress => false;
}
```

模板方法应由基类控制，通常不要设为 `virtual`，否则子类可以绕过不变量。抽象步骤表示子类必须回答的问题；虚 Hook 提供可选扩展。扩展点越多，基类与子类之间的隐式协议越复杂，这就是“脆弱基类”风险。

**C# 取舍。** 如果只有一两个可变步骤，组合 Strategy 或直接传入 `Func<T>` 往往更清楚；如果流程顺序和前后条件必须被同一个类型守住，Template Method 更合适。

**完整运行。** [TemplateMethodDemo.cs](../src/DesignPatterns/Behavioral/TemplateMethodDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- template-method
```

**练习。** 增加 XML 导出但不要复制 `Export`；让投递步骤异步化并支持取消；最后故意让子类重写整个流程，列出它能破坏的三个不变量。

### 4.2 第 4 章 Factory Method（工厂方法）——把“创建哪一种”留给子类

**问题场景。** 订单发运工作流固定执行“创建承运商、生成标签、安排取件”，普通订单用公路承运商，加急订单用航空承运商。工作流不应出现不断增长的 `if (order.Kind == ...)`。

```csharp
private abstract class ShippingWorkflow
{
    public string Ship(Order order)
    {
        var carrier = CreateCarrier();
        return carrier.Schedule(order);
    }

    protected abstract ICarrier CreateCarrier();
}

private sealed class ExpressShippingWorkflow : ShippingWorkflow
{
    protected override ICarrier CreateCarrier() => new AirCarrier();
}
```

`ShippingWorkflow` 是 Creator，`CreateCarrier` 是 Factory Method，`ICarrier` 是 Product。Creator 不知道具体产品，却能通过产品协议完成稳定算法。增加一种运输方案通常增加一对 ConcreteCreator / ConcreteProduct；若产品选择完全由运行时配置决定，注册表、委托工厂或 DI 容器可能比子类更轻。

Factory Method 不是任何返回对象的方法。模式的关键是：创建方法处在一个更大算法中，子类通过它影响算法使用的产品。

**完整运行。** [FactoryMethodDemo.cs](../src/DesignPatterns/Creational/FactoryMethodDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- factory-method
```

**练习。** 增加冷链工作流，并保证原有工作流零修改；再改用 `Func<ICarrier>` 注入，比较继承方案和委托方案在“类型数量、运行时选择、共享流程”上的差异。

## 5. 生成实例：生命周期、复制、分步构建和产品族

### 5.1 第 5 章 Singleton（单例）——先问生命周期，再写全局入口

**问题场景。** 示例把只读应用配置延迟创建一次，并让并发访问得到同一实例。

```csharp
private sealed class AppConfiguration
{
    private static readonly Lazy<AppConfiguration> InstanceHolder =
        new(() => new AppConfiguration(), LazyThreadSafetyMode.ExecutionAndPublication);

    private AppConfiguration() { }

    public static AppConfiguration Instance => InstanceHolder.Value;
}
```

私有构造器限制外部创建，静态字段保存唯一入口，`Lazy<T>` 明确延迟初始化和线程安全语义。不要自己写没有内存屏障的“双重检查锁”。更重要的是：Singleton 同时承担“只创建一个”和“全局可访问”两件事，后者会隐藏依赖、污染测试并让可变状态跨请求共享。

在 ASP.NET Core 等应用中，优先让 DI 容器管理 singleton lifetime，并通过构造函数显式注入。进程内单例也不等于分布式唯一；多实例部署需要数据库约束、租约或分布式锁。

**完整运行。** [SingletonDemo.cs](../src/DesignPatterns/Creational/SingletonDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- singleton
```

**练习。** 为配置增加可变字典并并发写入，观察问题；然后改回不可变快照。写一个测试说明静态单例为何会让测试相互影响，再改成由容器或组合根持有的单实例。

### 5.2 第 6 章 Prototype（原型）——复制的是值，还是共享引用

**问题场景。** 营销活动有昂贵的初始模板，需要为不同地区复制并本地化。重新从零组装容易遗漏规则，复制现有原型更自然。

```csharp
private sealed record CampaignTemplate(
    string Name,
    string Locale,
    List<string> Channels)
{
    public CampaignTemplate DeepClone(string locale) =>
        this with
        {
            Locale = locale,
            Channels = [.. Channels]
        };
}
```

Prototype 把创建知识放进原型自身或一个原型注册表。C# 的 `MemberwiseClone()`、record `with`、数组复制默认都是**浅复制**：引用类型字段仍可能共享。深复制必须根据领域语义逐层决定，不建议不加思考地用 JSON 序列化“万能克隆”，因为它慢、会丢失运行时语义，也可能绕过不变量。

**完整运行。** [PrototypeDemo.cs](../src/DesignPatterns/Creational/PrototypeDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- prototype
```

**练习。** 在活动中增加可变的 `Dictionary<string,string>` 元数据，先制造浅复制污染，再完成深复制。思考数据库实体、文件句柄、事件订阅者是否应该被克隆。

### 5.3 第 7 章 Builder（建造者）——让复杂对象只能以合法方式完成

**问题场景。** 发布计划包含环境、版本、审批人、步骤和回滚开关；预览环境与生产环境又有不同配方。一个十几个参数的构造器难读，随意对象初始化则可能产生非法半成品。

```csharp
var plan = new ReleasePlanBuilder()
    .ForEnvironment("Production")
    .WithVersion("2.4.0")
    .RequireApprovalBy("Release manager")
    .AddStep("Deploy canary")
    .AddStep("Promote rollout")
    .EnableRollback()
    .Build();
```

Builder 持有构建中的可变状态，`Build()` 集中验证并返回不可变 Product。Director 是可选角色，用来复用“生产发布配方”等构建顺序。最终对象要防御性复制集合，否则客户端仍可从 Builder 的列表侧面修改 Product。

简单对象优先构造器、对象初始化器和 `required` 属性。只有当构建有顺序、条件分支、跨字段验证或多种表示时，Builder 的额外类型才值得。

**完整运行。** [BuilderDemo.cs](../src/DesignPatterns/Creational/BuilderDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- builder
```

**练习。** 让 Production 必须有审批人且至少两个步骤；为 `Build()` 写失败用例；实现一个 Staging Director，但不在 Director 中泄露 Product 的内部集合。

### 5.4 第 8 章 Abstract Factory（抽象工厂）——一次选择一整套兼容产品

**问题场景。** Windows Fluent 与移动 Touch UI 都需要按钮和菜单，而且同一界面里的控件必须来自同一产品族。客户端不应分别判断平台并手工配对具体类型。

```csharp
private interface IUiFactory
{
    IButton CreateButton();
    IMenu CreateMenu();
}

private static string RenderScreen(IUiFactory factory)
{
    var button = factory.CreateButton();
    var menu = factory.CreateMenu();
    return $"{menu.Render()} + {button.Render()}";
}
```

AbstractFactory 声明创建一族产品的方法；ConcreteFactory 保证产品来自同一风格；AbstractProduct 让客户端只依赖协议。新增**产品族**很容易：添加新工厂和一套实现；新增**产品种类**较贵：给接口增加 `CreateDialog()` 会迫使所有工厂更新。这是它有意优化的变化方向。

**完整运行。** [AbstractFactoryDemo.cs](../src/DesignPatterns/Creational/AbstractFactoryDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- abstract-factory
```

**练习。** 增加 Web 产品族；再增加 `ITextBox` 产品种类并记录改动面。用配置在组合根选择具体工厂，确保业务页面中没有平台判断。

**本单元回顾。** Factory Method 通常创建一个产品，并借助继承把决定留给子类；Abstract Factory 通过组合创建一族产品；Builder 分步骤完成一个复杂产品；Prototype 复制已有样板；Singleton 控制生命周期。遇到“怎么 new”时，先明确真正变化的是产品类型、产品族、构建步骤、样板还是生命周期。

## 6. 分开考虑：识别两个独立变化维度

### 6.1 第 9 章 Bridge（桥接）——不要为每种组合建立子类

**问题场景。** 告警有“运维告警、安保告警”等语义，发送渠道又有 Email、SMS。若用继承表达全部组合，很快出现 `OperationalEmailAlert`、`OperationalSmsAlert`、`SecurityEmailAlert`；两个维度各增加一种，组合数就从 4 变 9。

```csharp
private interface IMessageChannel
{
    string Deliver(string recipient, string subject, string body);
}

private abstract class Alert(IMessageChannel channel)
{
    protected IMessageChannel Channel { get; } = channel;
    public abstract string Send(string recipient, string message);
}

private sealed class SecurityAlert(IMessageChannel channel) : Alert(channel)
{
    public override string Send(string recipient, string message) =>
        Channel.Deliver(recipient, "Security incident", message);
}
```

`Alert` 是 Abstraction，`OperationalAlert` / `SecurityAlert` 是 RefinedAbstraction；`IMessageChannel` 是 Implementor；Email/SMS 是 ConcreteImplementor。“桥”就是 Abstraction 持有 Implementor 的那条对象引用，不是额外叫 Bridge 的类。

Bridge 适合**两个维度都要独立扩展**的情况。若只有一个变化点，普通 Strategy 或简单组合即可。它与 Adapter 都连接两端，但 Bridge 是预先设计的稳定分层，Adapter 常用于事后兼容已有接口。

**完整运行。** [BridgeDemo.cs](../src/DesignPatterns/Structural/BridgeDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- bridge
```

**练习。** 新增合规告警和 Slack 渠道，验证只增加两个类型就得到所有新组合。再增加“重试”需求，判断它属于渠道实现、Decorator，还是告警抽象。

### 6.2 第 10 章 Strategy（策略）——整体替换算法

**问题场景。** 配送报价可选标准快递、加急快递或自提柜，算法使用相同输入并返回相同形状的报价。客户端需要在运行时切换，而不是在一个巨型方法中维护分支。

```csharp
private interface IDeliveryStrategy
{
    string Name { get; }
    DeliveryQuote Calculate(Shipment shipment);
}

private sealed class DeliveryPlanner
{
    private IDeliveryStrategy _strategy;

    public void Use(IDeliveryStrategy strategy) => _strategy = strategy;
    public DeliveryQuote Calculate(Shipment shipment) =>
        _strategy.Calculate(shipment);
}
```

Context 只负责委托和策略生命周期；ConcreteStrategy 封装不同公式。算法有自身配置、多个方法或需要被独立测试时，接口策略清晰；只有一个纯函数时，可以直接注入：

```csharp
public sealed class PriceCalculator(Func<Shipment, DeliveryQuote> quote)
{
    public DeliveryQuote Calculate(Shipment shipment) => quote(shipment);
}
```

不要把所有 `if` 都机械改成 Strategy。两个稳定分支可能比五个类型更易读；当算法会独立增加、需要运行时选择、测试或复用时再抽取。

**完整运行。** [StrategyDemo.cs](../src/DesignPatterns/Behavioral/StrategyDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- strategy
```

**练习。** 增加“超重货物不可使用自提柜”的业务规则。分别把规则放入 Context 和策略，比较谁拥有这条不变量更合理。把策略改成异步外部报价，正确传递取消和超时。

**Bridge 与 Strategy。** 二者都把对象放进另一个对象。Strategy 通常替换一个算法；Bridge 明确维护两个可独立扩展的类型层次。先说清变化维度，再决定用哪个名称。

## 7. 一致性：把单个对象与包装后的对象当成同一种东西

### 7.1 第 11 章 Composite（组合）——让叶子和容器共享协议

**问题场景。** 发布计划由单个任务和嵌套任务组组成。客户端既要渲染树，又要汇总总工时；如果到处判断 `is TaskItem` / `is WorkGroup`，递归逻辑会扩散。

```csharp
private interface IWorkItem
{
    string Name { get; }
    int EstimateHours { get; }
    IEnumerable<string> Render(int depth);
}

private sealed class WorkGroup : IWorkItem
{
    private readonly List<IWorkItem> _children = [];
    public int EstimateHours => _children.Sum(child => child.EstimateHours);
    public WorkGroup Add(IWorkItem child) { _children.Add(child); return this; }
}
```

Component 定义共同操作；Leaf 完成基本行为；Composite 保存 `IWorkItem` 子节点并递归组合结果。客户端只面对 Component，就能透明处理整棵树。

“透明性”和“安全性”存在取舍：若把 `Add/Remove` 放进 Component，叶子也会暴露无意义操作；若只放在 Composite，客户端创建树时需要知道容器类型。本例选择后者，让非法操作在编译期不可见。还要防止循环引用，否则递归会无限进行。

**完整运行。** [CompositeDemo.cs](../src/DesignPatterns/Structural/CompositeDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- composite
```

**练习。** 增加里程碑节点（工时为 0）；增加按名称查找的迭代器；阻止把祖先节点加入自己的后代。思考父引用应该由谁维护。

### 7.2 第 12 章 Decorator（装饰器）——按顺序叠加职责

**问题场景。** 酒店房价依次应用会员折扣、固定服务费和税。若用继承表示所有组合，会产生类爆炸；若把全部开关塞进 `RoomPrice`，核心类会知道每种附加规则。

```csharp
IPrice price = new RoomRate(400m);
price = new MemberDiscountDecorator(price, 0.10m);
price = new ServiceFeeDecorator(price, 25m);
price = new TaxDecorator(price, 0.08m);

private abstract class PriceDecorator(IPrice inner) : IPrice
{
    protected IPrice Inner { get; } = inner;
    public abstract decimal Total { get; }
    public abstract string Description { get; }
}
```

Component 与 Decorator 共享 `IPrice`；Decorator 也持有一个 Component，所以可以无限嵌套。每个 ConcreteDecorator 只增加一个职责。**顺序是语义的一部分**：先打折后收固定费，与先收费后对总额打折结果不同。组装代码必须明确，最好由组合根或工厂集中完成。

.NET 中 `Stream` 包装、ASP.NET Core middleware、日志作用域常体现相似结构。Decorator 与 Proxy 结构几乎一样；Decorator 关注叠加功能，Proxy 关注控制对某个真实对象的访问。

**完整运行。** [DecoratorDemo.cs](../src/DesignPatterns/Structural/DecoratorDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- decorator
```

**练习。** 增加封顶优惠；用两个顺序计算并写下差额；为装饰器加入明细集合，让客户端不必解析 `Description` 字符串。

## 8. 访问数据结构：操作扩展与请求传递

### 8.1 第 13 章 Visitor（访问者）——数据类型稳定、操作经常增加

**问题场景。** 购物车含图书和电子设备。定价、运费、税务、导出都依赖具体商品类型。如果把每种新操作不断加到商品接口，元素类会越来越杂；如果在服务中反复 `switch` 类型，类型知识又会散落。

```csharp
private interface ICartItem
{
    void Accept(ICartVisitor visitor);
}

private interface ICartVisitor
{
    void Visit(Book book);
    void Visit(ElectronicDevice device);
}

private sealed record Book(...) : ICartItem
{
    public void Accept(ICartVisitor visitor) => visitor.Visit(this);
}
```

第一次分派根据运行时元素调用 `Accept`；第二次分派由具体元素选择正确的 `Visit(Book)` 或 `Visit(ElectronicDevice)`，这就是双分派。新增 Visitor 不改元素；但新增元素类型会修改 Visitor 接口和所有 Visitor。它优化的是“元素种类稳定、操作频繁增加”的方向。

在封闭类型集合中，C# 模式匹配也很有竞争力：一个 `switch` 表达式可能更短、更容易导航。Visitor 的价值在于操作需要独立对象状态、双分派协议和可扩展操作集合时，而不是为了回避任何类型判断。

**完整运行。** [VisitorDemo.cs](../src/DesignPatterns/Behavioral/VisitorDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- visitor
```

**练习。** 新增库存盘点 Visitor；再新增生鲜商品，记录需要修改的所有 Visitor。用模式匹配重写定价操作，并比较两种方案对新增元素、新增操作的改动矩阵。

### 8.2 第 14 章 Chain of Responsibility（责任链）——请求沿链传递

**问题场景。** 报销申请先给组长，再按额度交给部门经理、财务总监。请求发起者不应知道最终由谁审批，也不应写死完整审批分支。

```csharp
private sealed class Approver
{
    private Approver? _next;

    public Approver Then(Approver next)
    {
        _next = next;
        return next;
    }

    public void Handle(ExpenseRequest request)
    {
        if (request.Amount <= _approvalLimit) Approve(request);
        else if (_next is not null) _next.Handle(request);
        else Reject(request);
    }
}
```

Handler 声明处理协议并保存后继；ConcreteHandler 决定处理还是转发。链可以运行时组装，发送者与接收者解耦。必须明确链尾行为：无人处理时是失败、忽略，还是使用默认处理者？也要防止节点重复、循环以及中间处理者吞掉错误。

ASP.NET Core middleware 是重要变体：每个节点可以在调用 `next` 前后执行，因此请求和响应形成“洋葱”。审批链则通常只有一个节点最终消费请求。

**完整运行。** [ChainOfResponsibilityDemo.cs](../src/DesignPatterns/Behavioral/ChainOfResponsibilityDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- chain-of-responsibility
```

**练习。** 加入合规检查节点，它可以拒绝任何额度；再实现“所有验证器都运行并收集错误”的管线，说明它与“首个处理者消费请求”的语义差别。

## 9. 简单化：减少客户端知道的对象数量

### 9.1 第 15 章 Facade（外观）——暴露一个用例级窗口

**问题场景。** 下单要依次预留库存、扣款、创建物流单。让每个 UI、API 和批处理客户端自己编排，会重复顺序、错误处理和事务边界。

```csharp
private sealed class CheckoutFacade(
    InventoryService inventory,
    PaymentGateway payment,
    ShippingService shipping)
{
    public IReadOnlyList<string> PlaceOrder(Order order) =>
    [
        inventory.Reserve(order.Sku, order.Quantity),
        payment.Charge(order.CustomerId, order.Amount),
        shipping.CreateShipment(order.Destination)
    ];
}
```

Facade 不必实现一个现有共同接口，它提供的是更高层、面向用例的新 API。子系统仍可独立使用；Facade 只限制普通客户端需要知道的入口。它适合作为模块边界或应用服务，但不要变成包办所有业务的“上帝类”。当 Facade 同时包含几十个无关用例时，应按业务能力拆分。

真实结算还要回答失败补偿：库存成功但扣款失败怎么办？Facade 可以协调事务或 Saga，但不能用一串表面成功的调用掩盖一致性问题。

**完整运行。** [FacadeDemo.cs](../src/DesignPatterns/Structural/FacadeDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- facade
```

**练习。** 让支付失败并实现库存释放；把返回字符串改成结构化 `CheckoutResult`；再决定日志、重试和幂等键分别属于 Facade、Decorator 还是子系统。

### 9.2 第 16 章 Mediator（中介者）——同事只认识协调者

**问题场景。** 多架飞机共享跑道。若每架飞机直接询问其他飞机是否正在降落，任何新规则都会修改所有 Aircraft。塔台应该集中维护占用者和等待队列。

```csharp
private interface IControlTower
{
    void RequestLanding(Aircraft aircraft);
    void CompleteLanding(Aircraft aircraft);
}

private sealed class Aircraft(string callSign, IControlTower tower)
{
    public void RequestLanding() => tower.RequestLanding(this);
    public void CompleteLanding() => tower.CompleteLanding(this);
}
```

Mediator 知道 Colleague 并协调它们；Colleague 只知道 Mediator，而不彼此引用。这减少了对象之间的网状依赖，代价是协调规则集中到 Mediator。若它持续膨胀，可以把排队策略、跑道分配和安全规则进一步拆成策略或领域服务。

Mediator 与 Facade 都减少连接，但方向不同：Facade 是客户端向复杂子系统发起单向使用；Mediator 处理多个同事之间的双向协作。事件总线也能解耦发送者和接收者，但它通常牺牲显式控制流，不应把简单对象协作都改成“发消息”。

**完整运行。** [MediatorDemo.cs](../src/DesignPatterns/Behavioral/MediatorDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- mediator
```

**练习。** 支持紧急航班插队；再支持两条跑道。观察 ControlTower 何时需要把排队算法提取成 Strategy。

## 10. 管理状态：通知、快照与状态行为

### 10.1 第 17 章 Observer（观察者）——状态变化后通知订阅者

**问题场景。** 订单变成 Paid、Shipped、Delivered 时，邮件与审计服务要独立响应。Order 不应直接 new 这些服务，也不该知道每个响应动作。

```csharp
private sealed class Order
{
    public event EventHandler<OrderStatusChangedEventArgs>? StatusChanged;

    public void ChangeStatus(OrderStatus next)
    {
        var previous = Status;
        Status = next;
        StatusChanged?.Invoke(
            this,
            new OrderStatusChangedEventArgs(Number, previous, next));
    }
}

order.StatusChanged += email.OnStatusChanged;
order.StatusChanged += audit.OnStatusChanged;
order.StatusChanged -= email.OnStatusChanged;
```

C# `event` 限制外部只能订阅/退订，不能直接触发事件，是进程内 Observer 的惯用实现。同步事件按订阅顺序在发布线程执行：某个处理器慢或抛异常，会影响后续处理器和发布者。跨进程可靠通知应使用持久消息、Outbox、重试与幂等，而不是误以为 event 自动可靠。

**内存泄漏警告。** 发布者持有委托，委托又引用订阅者。若发布者生命周期更长，忘记退订会阻止订阅者回收。可用明确的 `Dispose` 订阅句柄、弱事件或让二者生命周期一致。

**完整运行。** [ObserverDemo.cs](../src/DesignPatterns/Behavioral/ObserverDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- observer
```

**练习。** 增加指标订阅者；让一个订阅者故意抛异常，记录传播行为；再实现一个返回 `IDisposable` 的订阅 API，确保 `using` 后不再收到通知。

### 10.2 第 18 章 Memento（备忘录）——保存状态但不泄露内部字段

**问题场景。** 文本编辑器要撤销标题和内容修改，历史对象应该保存快照，却不应知道如何解释编辑器内部状态。

```csharp
private interface IEditorMemento { }

private sealed class TextEditor
{
    public IEditorMemento Save() => new Snapshot(_title, _content);

    public void Restore(IEditorMemento memento)
    {
        var snapshot = memento as Snapshot
            ?? throw new ArgumentException("快照来源无效", nameof(memento));
        (_title, _content) = (snapshot.Title, snapshot.Content);
    }

    private sealed record Snapshot(string Title, string Content) : IEditorMemento;
}
```

Originator 创建并解释 Memento；Caretaker 只存储不透明快照；Memento 本身用不可变 record 表示。把具体快照设为 Originator 的私有嵌套类型，可以在语言层面限制 Caretaker 读取字段。

完整快照简单但占内存；大文档可用增量命令、差异、检查点或事件溯源。外部资源、时间、随机数等状态未必可恢复。并发情况下，保存必须得到一致快照。

**完整运行。** [MementoDemo.cs](../src/DesignPatterns/Behavioral/MementoDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- memento
```

**练习。** 增加 Redo 栈；限制最多保存 20 个快照；比较每次保存全文与 Command 保存逆操作的空间和实现复杂度。

### 10.3 第 19 章 State（状态）——让当前状态对象决定行为

**问题场景。** 订单在 AwaitingPayment、Paid、Shipped、Cancelled 下对 Pay、Ship、Cancel 的响应不同。继续堆叠 `if/else` 会让每个操作重复状态判断，并很难检查遗漏的转换。

```csharp
private interface IOrderState
{
    string Name { get; }
    void Pay(PurchaseOrder order);
    void Ship(PurchaseOrder order);
    void Cancel(PurchaseOrder order);
}

private sealed class PurchaseOrder
{
    private IOrderState _state = AwaitingPaymentState.Instance;
    public void Pay() => _state.Pay(this);
    internal void TransitionTo(IOrderState next) => _state = next;
}
```

Context 委托给当前 State；ConcreteState 实现该状态的行为并触发合法转换。无字段的状态对象可以安全共享为单例；一旦状态实现保存某个订单的数据，就不能跨订单共享。

State 与 Strategy 结构相似。Strategy 通常由客户端选择以完成同一目标；State 通常由 Context 或状态对象根据生命周期自动转换，而且每个状态允许的行为不同。状态很少时，清晰的 `switch` 可能更简单；状态和操作形成大矩阵、转换经常增加时，State 更有价值。

**完整运行。** [StateDemo.cs](../src/DesignPatterns/Behavioral/StateDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- state
```

**练习。** 增加 Refunded 状态；画出允许的转换表；写出每个非法操作的预期结果。再用枚举 + `switch` 重写，比较新增状态和新增操作分别需要改哪里。

## 11. 避免浪费：共享重复状态，延迟昂贵访问

### 11.1 第 20 章 Flyweight（享元）——把内在状态和外在状态分开

**问题场景。** 地图上有成千上万个咖啡店和车站标记。每个标记的名称、坐标都不同，但同类标记的图标与颜色完全相同。重复保存样式会浪费内存。

```csharp
private sealed record MarkerStyle(string Icon, string Color); // 内在状态

private sealed record MapMarker(
    string Name,                 // 外在状态
    decimal Latitude,
    decimal Longitude,
    MarkerStyle Style);

private sealed class MarkerStyleFactory
{
    private readonly Dictionary<MarkerKind, MarkerStyle> _cache = [];

    public MarkerStyle Get(MarkerKind kind) =>
        _cache.TryGetValue(kind, out var style)
            ? style
            : _cache[kind] = Create(kind);
}
```

Flyweight 保存可共享、与具体上下文无关的**内在状态**，最好不可变；Context 保存名称、坐标等**外在状态**，调用时把它们与 Flyweight 组合。Factory 确保相同 Key 返回共享对象。

先测量再优化。小对象缓存可能增加字典、锁和生命周期管理成本，甚至因为缓存永不淘汰而占用更多内存。Flyweight 与对象池不同：享元可被多个客户端同时共享；对象池借出一个通常可变且暂时独占的对象，使用后归还。

**完整运行。** [FlyweightDemo.cs](../src/DesignPatterns/Structural/FlyweightDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- flyweight
```

**练习。** 生成十万个标记，分别比较每个标记独立样式与共享样式的分配量；增加主题后，把主题加入缓存 Key；讨论无界 Key 会造成什么问题。

### 11.2 第 21 章 Proxy（代理）——客户端不变，访问路径受控

**问题场景。** 商品目录来自远程服务，按 SKU 重复查询代价高。客户端仍希望依赖 `IProductCatalog`，不用知道缓存和远程调用细节。

```csharp
private interface IProductCatalog
{
    Product FindBySku(string sku);
}

private sealed class CachingCatalogProxy(IProductCatalog remote) : IProductCatalog
{
    private readonly Dictionary<string, Product> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public Product FindBySku(string sku)
    {
        if (_cache.TryGetValue(sku, out var product)) return product;
        return _cache[sku] = remote.FindBySku(sku);
    }
}
```

Subject 是共同协议；RealSubject 完成真实访问；Proxy 也实现协议并控制何时、是否以及怎样调用 RealSubject。常见变体包括虚拟代理（延迟创建）、保护代理（鉴权）、远程代理（序列化和通信）与缓存代理。

透明接口不等于透明语义。缓存必须定义过期、失效、一致性、并发去重和错误缓存策略；远程代理必须暴露超时、取消和网络失败，不能伪装成本地永不失败的方法。

**完整运行。** [ProxyDemo.cs](../src/DesignPatterns/Structural/ProxyDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- proxy
```

**练习。** 增加 30 秒过期；并发发起十个相同查询，确保只有一个远程调用；再为不存在的 SKU 设计负缓存，并说明多久失效。

## 12. 用类来表现：请求与语法都可以成为对象

### 12.1 第 22 章 Command（命令）——让一次请求拥有历史

**问题场景。** 文本编辑器要执行追加和替换，并支持 Undo/Redo。若按钮直接调用文档方法，历史栈无法统一保存如何撤销每种操作。

```csharp
private interface IEditorCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

private sealed class CommandHistory
{
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }
}
```

Command 封装 Receiver、参数和执行/撤销知识；Invoker 只面对命令协议并管理历史；Receiver 保存真正业务行为。命令可以排队、记录、组合、重试或延后执行。可撤销命令必须保存“执行前状态”或逆操作；同一个有状态命令实例通常不应并发重复执行。

只有执行而无需撤销时，`Action` / `Func<Task>` 可能足够。需要持久化命令时，不要序列化闭包或对象引用，应保存明确的命令名、版本、参数和幂等标识。

**完整运行。** [CommandDemo.cs](../src/DesignPatterns/Behavioral/CommandDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- command
```

**练习。** 增加删除命令；增加 MacroCommand，一次撤销多个子命令；把命令改为异步并处理执行到一半失败时的历史一致性。

### 12.2 第 23 章 Interpreter（解释器）——用对象树表达小语言

**问题场景。** 授权策略是 `(role = admin OR department = security) AND active = true`。我们希望把终结表达式和 AND/OR 组合成可执行语法树，而不是把规则写死在一个条件中。

```csharp
private interface IRuleExpression
{
    bool Interpret(UserContext context);
}

IRuleExpression policy = new AndExpression(
    new OrExpression(
        new ClaimEqualsExpression("role", "admin"),
        new ClaimEqualsExpression("department", "security")),
    new ClaimEqualsExpression("active", "true"));

var allowed = policy.Interpret(user);
```

TerminalExpression 处理最小语法单元；NonterminalExpression 保存子表达式并组合结果；Context 提供解释所需的数据。Composite 表示 AST，Interpreter 在节点上定义求值行为。本示例**直接构造 AST**，没有把文本解析成 AST；词法分析和优先级解析是另一层职责。

该模式适合规则少、语法稳定的小型 DSL。语法一大，每条产生式一个类会迅速膨胀，性能和错误报告也变差；此时应使用解析器生成器、成熟表达式库，或先建立 AST 再用 Visitor 执行不同操作。对于来自用户的规则，还要限制深度、执行时间和可访问能力。

**完整运行。** [InterpreterDemo.cs](../src/DesignPatterns/Behavioral/InterpreterDemo.cs)

```powershell
dotnet run --project src/DesignPatterns.Runner -- interpreter
```

**练习。** 增加 NOT 表达式；给表达式增加 `Describe()`；再写一个仅支持括号、AND、OR 和 `key=value` 的最小解析器，并为错误位置提供可读消息。

**Command 与 Interpreter。** Command 把“要做的一次动作”对象化；Interpreter 把“描述一类表达式的语法”对象化。Command 常进入队列或历史，Interpreter 常形成树并被递归求值。

## 13. 模式组合：真实系统不会一次只出现一个模式

### 13.1 一个可演进的结算模块

假设要把本指南里的结算示例扩成真实模块，可以按职责组合：

```text
API
 |
 v
CheckoutFacade ------------------------------ 用例入口
 |         |              |
 v         v              v
Inventory  PaymentProxy   ShippingWorkflow -- Factory Method 创建 Carrier
             |
             +-- RetryDecorator
             +-- AuditDecorator

DiscountStrategy <------- CheckoutContext ----> OrderState
       |
       +-- Standard / Member / Campaign

Order events -------------------------------> Observer subscribers
```

每个模式只解决一个明确问题：Facade 缩窄入口；Proxy 控制外部支付访问；Decorator 叠加重试和审计；Strategy 替换折扣算法；State 守住订单生命周期；Observer 发布已完成的状态变化。模式组合不是把名字堆在图上，而是让每条依赖都有一个可解释的变化原因。

### 13.2 组合顺序会改变语义

以下两条管线结构相似，行为却不同：

```csharp
IPayment first = new RetryPaymentDecorator(
    new AuditPaymentDecorator(remote));

IPayment second = new AuditPaymentDecorator(
    new RetryPaymentDecorator(remote));
```

第一种可能只审计真正到达远程端的每次尝试；第二种可能只审计客户端看到的一次总体调用。组装时要用业务语言写测试，而不是只验证“类型能套起来”。

### 13.3 模式可以被语言特性压缩，但职责不能消失

委托能把 Strategy 或 Command 压缩成几行；record 能让 Prototype 或 Memento 更短；`await foreach` 内置异步 Iterator；事件内置 Observer 协议。这并不意味着模式“过时”，而是语言替你实现了部分机械结构。你仍要决定生命周期、所有权、错误语义和变化边界。

### 13.4 从组合图进入三个可运行项目

仓库的 `examples` 目录提供 OnlineStore、SmartHome 和 DocumentWorkflow 三个完整业务故事，合计覆盖 GoF 23 种模式。它们保留了失败路径、自检入口和扩展练习，用于验证模式在真实协作链上的价值，而不是重复独立 Demo。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1 -SelfTest
```

详细的业务流程、跨模式比较和四周路线见 `docs/设计模式实战项目学习指南.md`。

### 13.5 从三个教学项目进入两个高级实验

三个 `examples` 项目回答“多个模式如何服务一条业务链”；[`labs`](../labs/README.md) 则继续回答两个问题：怎样安全改变已有结构，以及怎样在并发、重试、乱序和崩溃后保持业务正确。

| 实验 | 推荐路线 | 核心证据 |
| --- | --- | --- |
| [CheckoutRefactoringKata](../labs/CheckoutRefactoringKata/README.md) | 坏代码 → 特征测试 → Strategy → Chain → State → Facade → 等价性验证 | 重构前后收据、失败和轨迹完全一致 |
| [ReliableCheckout](../labs/ReliableCheckout/README.md) | 2～3 周：幂等库存 → Outbox 恢复 → 回调状态机 | 五个真实 HTTP + SQLite 故障场景通过 |

设计模式只组织对象协作。ReliableCheckout 中真正保护持久化不变量的还有事务、条件更新、端到端幂等、至少一次投递、重试和可控故障测试；它们不能被一个模式类名替代。

## 14. 选型指南：相似模式怎样区分

### 14.1 创建相关模式

| 你的核心问题 | 优先考虑 | 警惕 |
| --- | --- | --- |
| 一个稳定流程需要子类决定一个产品 | Factory Method | 为每个配置制造子类 |
| 一次选择一整套兼容产品 | Abstract Factory | 新增产品种类会修改全部工厂 |
| 对象必须按步骤构建并集中校验 | Builder | 简单 DTO 也套 Fluent Builder |
| 从复杂样板快速产生变体 | Prototype | 浅复制共享可变引用 |
| 生命周期必须在一个进程内唯一 | Singleton 或 DI singleton | 全局可变状态、测试污染、误当分布式唯一 |

### 14.2 “包装一个对象”的五种模式

| 模式 | 接口是否通常相同 | 主要意图 | 典型时间点 |
| --- | --- | --- | --- |
| Adapter | 不同，负责转换 | 兼容旧接口和目标接口 | 接入既有/第三方类型时 |
| Decorator | 相同 | 叠加可组合职责 | 运行时组装 |
| Proxy | 相同 | 控制对真实对象的访问 | 访问前后或延迟访问 |
| Facade | 新的高层接口 | 简化一组子系统 | 模块边界设计时 |
| Bridge | 两套独立层次 | 两个变化维度独立扩展 | 架构设计早期 |

判断口诀不是定义，而是一组问题：**接口变了吗？客户端以为自己在用谁？增加的是业务职责、访问控制，还是另一个变化维度？**

### 14.3 行为相关模式

| 容易混淆的组合 | 关键区别 |
| --- | --- |
| Strategy vs State | Strategy 多由客户端选择算法；State 多由生命周期内部切换行为 |
| Template Method vs Strategy | 前者用继承固定流程；后者用组合整体替换算法 |
| Observer vs Mediator | Observer 广播变化且发布者不知道订阅者；Mediator 明确协调同事间规则 |
| Chain vs Command | Chain 决定谁处理请求；Command 让请求本身可存储、撤销、排队 |
| Visitor vs Strategy | Visitor 按元素运行时类型选择重载操作；Strategy 针对统一输入替换算法 |
| Memento vs Command undo | Memento 保存状态快照；Command 常保存逆操作或执行前局部状态 |

### 14.4 一个简短决策流程

```text
变化主要发生在哪里？
  |
  +-- 创建对象 ------ 产品？产品族？步骤？样板？生命周期？
  |
  +-- 连接对象 ------ 兼容？包装？树？双维度？简化入口？
  |
  +-- 运行时行为 ---- 算法？状态？通知？排队？传递？遍历？

找到候选后再问：
  1. 不使用模式，最小清晰实现是什么？
  2. 已经发生了哪种变化，还是只在猜未来？
  3. 新结构让哪类下一次修改变小？
  4. 它让调试、性能、并发或团队理解付出什么代价？
```

## 15. 从坏味道重构，而不是从模式名称出发

### 15.1 推荐重构顺序

1. 写一个覆盖当前行为的特征测试。
2. 标出重复分支、对象创建、状态判断或第三方调用等真实变化点。
3. 提取最小协议，让客户端先依赖这个协议。
4. 移动一个职责，保持每一步都能编译运行。
5. 只有当结构稳定后，才用模式名称沟通。
6. 删除不再需要的旧分支和兼容层。

### 15.2 例：从配送分支到 Strategy

起点通常长这样：

```csharp
decimal Calculate(string kind, Shipment shipment)
{
    if (kind == "standard") return 5m + shipment.Weight * 1.2m;
    if (kind == "express") return 12m + shipment.Weight * 2m;
    if (kind == "locker") return 3m + shipment.Weight * 0.6m;
    throw new ArgumentOutOfRangeException(nameof(kind));
}
```

重构时不要一次创建十个类型。先提取一个函数参数，验证调用方确实需要运行时替换；算法开始拥有名称、配置和多个结果后，再提升为 `IDeliveryStrategy`。模式是重构的结果，不是第一行代码。

### 15.3 过度设计的信号

- 只有一个实现，且没有实际替换、隔离或测试需求，却有多层空接口。
- 新增一个简单字段要穿过五个抽象层。
- 团队无法从类型名判断业务职责，只能看到 `ManagerFactoryProvider`。
- 为“也许有一天”创建扩展点，却没有测试证明扩展不改旧代码。
- 模式隐藏了网络、事务、性能或失败等重要语义。

### 15.4 可执行重构工坊：不要跳过中间证据

[`CheckoutRefactoringKata`](../labs/CheckoutRefactoringKata/README.md) 把本章顺序变成一套可运行练习。Starter 的结账逻辑行为正确，但一个服务同时承担校验、计价、状态和编排。练习路线固定为：

```text
坏代码
  -> 特征测试锁定已有行为
  -> Strategy 隔离价格算法
  -> Chain of Responsibility 固定校验顺序与短路
  -> State 拒绝非法状态转换
  -> Facade 收拢用例入口
  -> 等价性测试比较重构前后结果
```

```powershell
dotnet test labs/CheckoutRefactoringKata/Tests/CheckoutRefactoringKata.Tests.csproj -c Release
dotnet run --project labs/CheckoutRefactoringKata/Starter/CheckoutRefactoringKata.Starter.csproj -c Release -- success
dotnet run --project labs/CheckoutRefactoringKata/Reference/CheckoutRefactoringKata.Reference.csproj -c Release -- success
```

特征测试回答“调用方现在依赖什么”；等价性测试回答“移动职责后是否改变了这些行为”。只有两层证据始终为绿，模式才是一次安全重构的结果，而不是一次大规模重写。

## 16. 怎样验证模式代码真的有价值

### 16.1 测行为，不测类图

好的测试不是断言“某类实现某接口”，而是证明变化被隔离：

- 给 Strategy 同一输入，验证不同算法结果和 Context 的无分支委托。
- 给 Decorator 改变包装顺序，验证业务语义确实不同。
- 给 Factory 增加假 Product，验证 Creator 的固定流程仍工作。
- 给 State 覆盖每条合法/非法转换。
- 给 Observer 退订后，验证不再接收事件。
- 给 Proxy 重复访问，验证真实服务调用次数。
- 给 Prototype 修改克隆的集合，验证原型不受影响。

### 16.2 本仓库的可执行烟雾测试

本仓库保留两层互补验证。独立 Console 烟雾测试适合离线学习环境和快速确认 23 个 Demo 全部可运行，它验证：

- 恰好注册 23 个模式，Key 唯一；
- 创建型 5 个、结构型 7 个、行为型 11 个；
- 每个示例能运行并产生非空输出；
- 同一 Demo 连续运行结果确定。

```powershell
dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release
```

输出应以这句话结束：

```text
烟雾测试通过：23 个模式均可重复运行，Key、分类与输出均符合约定。
```

正式回归由 `tests/TeachingProjects.Tests` 中的 xUnit 测试承担：它验证每个模式的关键业务结果，以及三个实战项目的库存、状态、权限、撤销、恢复和组合顺序。扩展练习采用“先写失败测试，再实现，再重构”的闭环；Console 入口继续保证教学示例可以零配置运行。

```powershell
dotnet test tests/TeachingProjects.Tests/DesignPatterns.TeachingProjects.Tests.csproj --configuration Release
```

### 16.3 高级实验验证重构等价与故障恢复

```powershell
dotnet test labs/CheckoutRefactoringKata/Tests/CheckoutRefactoringKata.Tests.csproj -c Release
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release
```

重构工坊比较 Starter 与 Reference 的业务结果和完整轨迹。ReliableCheckout 则通过真实 ASP.NET Core HTTP 管道和独立 SQLite 文件验证五个毕业场景：重复提交只扣一次库存、并发抢购不超卖、重复/乱序回调不破坏状态、Outbox 首次失败后恢复，以及 handler 完成后崩溃重放不产生第二次支付。

### 16.4 一键验证全部产物

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1
```

它会构建解决方案、运行烟雾测试、打印目录，并生成 PDF。只验证代码时：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -SkipPdf
```

## 17. 六周学习路线与阶段项目

### 第 1 周：对象协作的基本边界

学习 Iterator、Adapter、Template Method、Factory Method。每天完成一章，并把一个自己项目里的第三方 API 包进 Adapter。周末目标：能解释接口、抽象类、委托与组合的选择。

### 第 2 周：创建对象而不散落创建知识

学习 Singleton、Prototype、Builder、Abstract Factory。做一个“报表配置器”：Builder 构造不可变报表，Abstract Factory 产生 HTML/PDF 产品族，Prototype 保存模板。刻意不用全局可变 Singleton。

### 第 3 周：独立维度与树形结构

学习 Bridge、Strategy、Composite、Decorator。阶段项目做一个通知系统：告警级别 × 渠道使用 Bridge；重试/审计使用 Decorator；费用计算使用 Strategy；批量目标用 Composite。

### 第 4 周：数据结构与协作拓扑

学习 Visitor、Chain、Facade、Mediator。给阶段项目增加 Visitor 统计、验证 Chain 和一个 Facade 用例入口。画出一次请求的时序图，确认调用顺序没有被模式名称遮住。

### 第 5 周：状态与时间

学习 Observer、Memento、State。为阶段项目建立状态转换表；用事件通知 UI；用 Memento 或 Command 支持撤销。至少写一个退订测试和一个非法状态转换测试。

### 第 6 周：性能、访问与小语言

学习 Flyweight、Proxy、Command、Interpreter。先用基准或分配统计证明共享/缓存值得，再实现。最后做一次设计评审：删除一个没有真实变化需求的模式，并说明删除后为什么更清楚。

### 六周之后：组合项目与 3～4 周高级路线

基础六周之后，先按 [实战指南的四周计划](设计模式实战项目学习指南.md#8-四周学习计划) 完成三个 `examples` 项目，再进入高级实验：用 4～7 天完成 [坏代码重构工坊](../labs/CheckoutRefactoringKata/README.md)，随后用 2～3 周完成 [ReliableCheckout 生产化毕业项目](../labs/ReliableCheckout/README.md)。

ReliableCheckout 的毕业线不是“列出用了哪些模式”，而是五个故障场景全部通过，并且你能解释事务、幂等、Outbox、退避和状态机分别保护什么不变量。

### 毕业验收问题

你应能不看定义回答：

1. 这个模式优化了哪一种未来变化？
2. 它新增了哪些运行时对象和间接层？
3. 错误、取消、并发和生命周期语义放在哪里？
4. 新增具体实现时哪些旧文件不需要修改？
5. 如果需求更简单，最小替代方案是什么？

## 18. 练习提示与参考方向

以下不是唯一答案，而是自查边界。

### 18.1 Iterator 异步遍历

方法签名可以是 `async IAsyncEnumerable<Track> Stream([EnumeratorCancellation] CancellationToken token)`；延迟时传 `token`，消费端用 `await foreach (... in stream.WithCancellation(token))`。不要把取消异常吞成“正常结束”。

### 18.2 Adapter 异步升级

如果 Target 从同步变异步，Adapter 虽能包装 `Task.FromResult`，但真正远程调用应原生异步。不要在 Adapter 内用 `.Result` 或 `.Wait()` 阻塞。

### 18.3 Prototype 深复制

对每个字段明确分类：不可变值可共享；可变集合要复制容器和必要的元素；外部资源通常不能复制；身份对象（数据库实体）往往不应该被当作 Prototype。

### 18.4 Decorator 顺序

用数字写预期：`(400 × 0.9 + 25) × 1.08 = 415.80`。如果先加服务费再整体九折，则 `(400 + 25) × 0.9 × 1.08 = 413.10`。先让产品经理确认语义，再固定组装顺序。

### 18.5 Observer 异常

.NET event 默认同步多播，某个处理器抛异常会停止当前调用链。若要求隔离，可读取 invocation list 逐个捕获，或发布到可靠消息基础设施；两种方案的失败可见性完全不同。

### 18.6 State 转换表

先写表再写类：行是当前状态，列是命令，单元格是“新状态 + 副作用/错误”。若大部分单元格相同且状态很少，枚举 `switch` 可能比多个状态类更合适。

### 18.7 Proxy 并发缓存

`Dictionary` 不支持无锁并发写。可以用 `ConcurrentDictionary<TKey, Lazy<Task<T>>>` 合并并发请求；失败后通常要移除缓存项，否则会永久缓存失败任务。

### 18.8 Command 撤销

Undo 必须只撤销该命令自己的效果。Redo 后仍要保证保存的前态正确；执行新命令时清空 Redo 栈。宏命令若中途失败，要决定回滚已执行子命令还是保留部分成功。

### 18.9 Interpreter 安全

解析用户表达式时限制最大长度、嵌套深度和节点数；解释时设置时间/步骤预算。不要让表达式任意反射类型、调用方法或读取未授权数据。

## 19. 常见问题

### 19.1 是否必须为每个模式画 UML？

不必须。先画三到六个角色和真正的依赖箭头。对于 Observer、Chain、Command、Mediator，时序图往往比类图更能暴露执行语义；对于 Composite、Visitor、Bridge，类图更有帮助。

### 19.2 为什么示例把很多参与者写成嵌套私有类型？

为了让每个 Demo 是独立、无命名冲突、易运行的教学单元。生产代码应按业务模块和可见性拆分，不要照搬“一文件塞完”的组织方式。模式角色也不要求一角色一文件。

### 19.3 模式是否违反 YAGNI？

如果没有已发生的变化、隔离需求或测试痛点，只凭想象增加抽象，确实可能违反 YAGNI。模式最可靠的采用时机是重构：先看到重复变化成本，再建立针对那个变化轴的边界。

### 19.4 SOLID 与设计模式是什么关系？

SOLID 是评估依赖与职责的原则，模式是常见协作结构。Strategy 常体现依赖倒置和开放封闭，Facade 体现接口隔离，Decorator 体现开放封闭；但套上模式并不自动满足 SOLID，过大的 Mediator 或 Facade 仍可能违反单一职责。

### 19.5 依赖注入容器是否就是 Abstract Factory？

不是。容器是通用对象组装基础设施；Abstract Factory 是业务可见的产品族创建协议。可以让容器提供某个具体工厂，但不要把 `IServiceProvider` 当成工厂到处传递。

### 19.6 为什么没有复制原书 Java 示例？

本项目保留原书的学习顺序、角色分析、扩展点、相关模式和练习闭环，但使用重新设计的 .NET 场景与现代 C# 实现。这样能学习同一设计思想，也避免把 Java 语法和旧版标准库习惯机械移植到 C#。

## 20. 附录：命令速查与源码索引

### 20.1 常用命令

```powershell
# 构建全部项目
dotnet build DesignPatterns.sln --configuration Release

# 查看 23 种模式
dotnet run --project src/DesignPatterns.Runner -- --list

# 运行一个模式
dotnet run --project src/DesignPatterns.Runner -- observer

# 运行一个 GoF 分类
dotnet run --project src/DesignPatterns.Runner -- --category Structural

# 运行全部模式
dotnet run --project src/DesignPatterns.Runner -- --all

# 验证全部 Demo
dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release

# 运行三个模式组合实战
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1

# 验证三个模式组合实战
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1 -SelfTest

# 验证坏代码到设计模式重构工坊
dotnet test labs/CheckoutRefactoringKata/Tests/CheckoutRefactoringKata.Tests.csproj -c Release

# 验证生产化毕业项目的五个故障场景
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release

# 生成 PDF
powershell -ExecutionPolicy Bypass -File scripts/export-guide.ps1

# 生成完整五份课程包 PDF
powershell -ExecutionPolicy Bypass -File scripts/export-all-guides.ps1

# 代码 + PDF 全量验证
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1
```

### 20.2 完整源码索引

| 模式 | 完整文件 |
| --- | --- |
| Iterator | `src/DesignPatterns/Behavioral/IteratorDemo.cs` |
| Adapter | `src/DesignPatterns/Structural/AdapterDemo.cs` |
| Template Method | `src/DesignPatterns/Behavioral/TemplateMethodDemo.cs` |
| Factory Method | `src/DesignPatterns/Creational/FactoryMethodDemo.cs` |
| Singleton | `src/DesignPatterns/Creational/SingletonDemo.cs` |
| Prototype | `src/DesignPatterns/Creational/PrototypeDemo.cs` |
| Builder | `src/DesignPatterns/Creational/BuilderDemo.cs` |
| Abstract Factory | `src/DesignPatterns/Creational/AbstractFactoryDemo.cs` |
| Bridge | `src/DesignPatterns/Structural/BridgeDemo.cs` |
| Strategy | `src/DesignPatterns/Behavioral/StrategyDemo.cs` |
| Composite | `src/DesignPatterns/Structural/CompositeDemo.cs` |
| Decorator | `src/DesignPatterns/Structural/DecoratorDemo.cs` |
| Visitor | `src/DesignPatterns/Behavioral/VisitorDemo.cs` |
| Chain of Responsibility | `src/DesignPatterns/Behavioral/ChainOfResponsibilityDemo.cs` |
| Facade | `src/DesignPatterns/Structural/FacadeDemo.cs` |
| Mediator | `src/DesignPatterns/Behavioral/MediatorDemo.cs` |
| Observer | `src/DesignPatterns/Behavioral/ObserverDemo.cs` |
| Memento | `src/DesignPatterns/Behavioral/MementoDemo.cs` |
| State | `src/DesignPatterns/Behavioral/StateDemo.cs` |
| Flyweight | `src/DesignPatterns/Structural/FlyweightDemo.cs` |
| Proxy | `src/DesignPatterns/Structural/ProxyDemo.cs` |
| Command | `src/DesignPatterns/Behavioral/CommandDemo.cs` |
| Interpreter | `src/DesignPatterns/Behavioral/InterpreterDemo.cs` |

### 20.3 最后的自检

学习完后，随机抽一个模式，用三分钟只说四件事：**问题、角色、变化方向、代价**。然后不用看原代码，写一个不同业务场景的最小实现。能做到这一点，模式才从“背过的名词”变成了你的设计词汇。
