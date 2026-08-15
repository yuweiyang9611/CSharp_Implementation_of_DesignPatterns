# C# 设计模式学习项目

这是基于《图解设计模式》学习顺序重新编写的现代 C# 14 / .NET 10 课程：覆盖 GoF 23 种模式、23 个可独立运行的示例、3 个模式组合教学项目、2 个高级实验，以及可一次生成 5 份 PDF 的课程交付链。

> 第一次打开仓库？请从 [START_HERE：选择你的学习路线](START_HERE.md) 开始。需要快速定位源码时使用 [23 种模式索引](docs/模式索引.md)。

详细教程： [C# 设计模式学习指导](docs/CSharp设计模式学习指南.md)

实战教程： [设计模式实战项目学习指南](docs/设计模式实战项目学习指南.md)

高级实验： [重构工坊与生产化毕业项目](labs/README.md)

## 公开迁移与提交隐私

本公开仓库由先前的私有仓库迁移而来。由于旧仓库的提交元数据和 Pull Request 记录包含个人隐私信息，旧仓库已永久删除，原有提交、分支和 PR 历史均未迁移；本仓库从当前代码快照重新初始化，历史以 `Initial public release` 为起点。

为避免提交元数据再次暴露私人邮箱，本仓库只接受 GitHub noreply 邮箱作为 Git author/committer 邮箱。维护者本机已启用版本化 Git hooks，CI 也会扫描全部可达提交，并在发现非 noreply 邮箱时失败且不在日志中输出邮箱值。

Git 不会在克隆时自动启用仓库中的 hooks。每个新克隆都必须运行一次初始化脚本：

```powershell
pwsh -File ./scripts/enable-git-hooks.ps1 -Email "你的 GitHub noreply 邮箱"
```

如果当前 Git 配置已经使用 noreply 邮箱，可以省略 `-Email`。完整说明、Windows PowerShell 命令和故障排查见 [Git hooks 启用指南](GIT_HOOKS.md)。

GitHub noreply 邮箱可在 GitHub 的 **Settings → Emails** 中查看。建议同时启用 **Keep my email addresses private** 和 **Block command line pushes that expose my email**。

## 课程分层

| 层次 | 内容 | 主要问题 | 验证方式 |
| --- | --- | --- | --- |
| 1. 独立模式 | `src/DesignPatterns` 中 23 个 Demo | 一个模式的角色怎样协作 | Runner + 离线烟雾测试 |
| 2. 模式组合 | OnlineStore、SmartHome、DocumentWorkflow | 多个模式怎样进入同一业务链 | Console 自检 + xUnit 行为测试 |
| 3. 安全重构 | [CheckoutRefactoringKata](labs/CheckoutRefactoringKata/README.md) | 怎样从坏代码小步得到 Strategy、Chain、State、Facade | 特征测试 + 前后等价性测试 |
| 4. 生产可靠性 | [ReliableCheckout](labs/ReliableCheckout/README.md) | 重试、并发、乱序和崩溃后怎样保持正确 | HTTP + SQLite 集成测试 |

## 快速开始

先用一条命令完成环境体检、构建、正式测试、23 个 Demo 烟雾测试、3 个教学项目自检和高级实验验证：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -SkipPdf
```

也可以分步运行：

```powershell
dotnet build DesignPatterns.sln --configuration Release
dotnet test tests/TeachingProjects.Tests/DesignPatterns.TeachingProjects.Tests.csproj --configuration Release
dotnet run --project src/DesignPatterns.Runner -- --list
dotnet run --project src/DesignPatterns.Runner -- iterator
dotnet run --project src/DesignPatterns.Runner -- --all
dotnet run --project tests/DesignPatterns.SmokeTests --configuration Release
```

Runner 还支持按经典 GoF 分类运行：

```powershell
dotnet run --project src/DesignPatterns.Runner -- --category Creational
dotnet run --project src/DesignPatterns.Runner -- --category Structural
dotnet run --project src/DesignPatterns.Runner -- --category Behavioral
```

## 三个模式组合实战项目

| 项目 | 业务场景 | 主要模式 |
| --- | --- | --- |
| [OnlineStore](examples/OnlineStore/README.md) | 电商结算、支付和订单生命周期 | Builder、Factory Method、Strategy、责任链、State、Observer、Facade |
| [SmartHome](examples/SmartHome/README.md) | 多厂商设备接入、联动、撤销和场景恢复 | Singleton、Adapter、Bridge、Composite、Proxy、Command、Mediator、Memento |
| [DocumentWorkflow](examples/DocumentWorkflow/README.md) | 报表筛选、合规检查和多渠道发布 | Abstract Factory、Prototype、Decorator、Flyweight、Interpreter、Iterator、Template Method、Visitor |

运行全部实战故事或自检：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1
powershell -ExecutionPolicy Bypass -File scripts/run-teaching-projects.ps1 -SelfTest
```

三个项目合计覆盖全部 GoF 23 种模式。详细阅读顺序、模式协作关系、反例和渐进练习见 [实战项目索引](examples/README.md)。

正式 xUnit 测试验证库存、状态转换、权限、Undo、Memento、解析优先级和装饰器顺序等业务契约；原有 `--self-test` 继续作为零依赖、可随手运行的教学入口。

## 两个高级实验

推荐先做重构工坊，再做生产化毕业项目：

```powershell
# 坏代码 -> 特征测试 -> Strategy/Chain/State/Facade -> 等价性验证
dotnet test labs/CheckoutRefactoringKata/Tests/CheckoutRefactoringKata.Tests.csproj -c Release

# 幂等、并发库存、Transactional Outbox、重试和乱序回调
dotnet test labs/ReliableCheckout/ReliableCheckout.slnx -c Release
```

重构工坊建议 4～7 天；ReliableCheckout 建议 2～3 周。统一入口、阶段路线和五个毕业验收场景见 [高级实验索引](labs/README.md)。

## 持续集成

GitHub Actions 在每次 push 和 pull request 时使用 .NET 10 自动执行锁定还原、格式检查、构建、xUnit 测试、全部轻量自检、文档校验和 HTML 指南导出。四个测试程序集都生成 Cobertura，且每份报告必须达到行覆盖率 55%、分支覆盖率 40% 的防回退基线。PDF 由手动或版本标签工作流生成，避免浏览器打印影响常规代码验证。

## 生成 PDF

需要本机安装 Microsoft Edge、Google Chrome 或 Chromium：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/export-guide.ps1
```

成品位于 `output/pdf/CSharp设计模式学习指南.pdf`，同时保留便于预览的 HTML。导出器是仓库内的 C# 项目，不需要额外 NuGet 包。

生成完整的五份课程包：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/export-all-guides.ps1
```

生成结果：

```text
output/pdf/CSharp设计模式学习指南.pdf
output/pdf/设计模式实战项目学习指南.pdf
output/pdf/CSharp-Design-Patterns-Learning-Path.pdf
output/pdf/Checkout-Refactoring-Workshop.pdf
output/pdf/Reliable-Checkout-Graduation-Project.pdf
```

全量验证（编译、运行 23 个示例、列出目录、生成 PDF）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1
```

只验证代码：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1 -SkipPdf
```

## 项目结构

```text
src/DesignPatterns/               模式实现
src/DesignPatterns.Runner/        命令行演示器
tests/DesignPatterns.SmokeTests/   离线可执行验证
tests/TeachingProjects.Tests/      可由 dotnet test / IDE 发现的行为测试
examples/                           三个模式组合实战项目
labs/                               重构工坊与生产化毕业项目
docs/                              Markdown 学习指导
tools/GuideExporter/              Markdown/HTML/PDF 导出器
scripts/                           验证与导出脚本
```

教程和示例采用原书的初学者顺序（Iterator 到 Interpreter），同时在目录中标注经典 GoF 创建型、结构型、行为型分类。
