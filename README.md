# C# 设计模式学习项目

这是基于《图解设计模式》学习顺序重新编写的现代 C# 14 / .NET 10 课程：覆盖 GoF 23 种模式、23 个可独立运行的示例、3 个模式组合教学项目、2 个高级实验，以及可一次生成 5 份 PDF 的课程交付链。

> **在线学习：** [打开 C# 设计模式学习地图](https://yuweiyang9611.github.io/CSharp_Implementation_of_DesignPatterns/)——按时间或实际问题选择路线，在 23 个独立课件、3 个项目和 2 个实验之间持续记录“阅读 → 运行 → 改造 → 验证”进度。

> **免配置运行：** [在 GitHub Codespaces 中打开](https://codespaces.new/yuweiyang9611/CSharp_Implementation_of_DesignPatterns?quickstart=1)——预配置 .NET 10、Node.js 24、站点构建与预览任务；创建 Codespace 可能消耗 GitHub 使用额度。

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

不想先配置本地环境时，直接使用上面的 Codespaces 入口；容器创建后会锁定还原依赖、构建解决方案并生成学习站。VS Code 中运行 `Site: preview` 任务即可在 4173 端口预览。真实浏览器回归需要系统 Chrome 或 Edge，由 GitHub Actions 或安装了浏览器的本机执行。

本地验证需要 PowerShell 7、.NET 10 SDK 和 Node.js 24。日常改动先运行快速档：构建、正式测试（含高级实验）、23 个 Demo 烟雾测试、3 个项目自检、目录与文档检查，以及复习和存储故障测试。

```powershell
pwsh -File scripts/verify.ps1 -Mode Quick
```

提交前运行完整档，需要可联网还原依赖，并安装 Chrome 或 Edge（也可用 `CHROME_PATH` 指定浏览器）：

```powershell
pwsh -File scripts/verify.ps1 -Mode Full -SkipPdf
```

完整档增加锁定还原、格式检查、四份覆盖率报告的行 55%／分支 40% 门槛、验证脚本自测、站点构建、真实浏览器与可访问性回归，以及 HTML 导出。每次覆盖率结果写入独立的 `output/test-results/verify-<唯一标识>/`，避免旧报告干扰。它对应 CI 的代码与站点检查；提交邮箱隐私检查、跨系统矩阵和线上部署核验仍由 CI 执行。

Quick 无需 WebAssembly 工作负载。Full 和站点构建需要在所选 .NET 10 SDK 中安装 `wasm-tools` 与 `wasm-experimental`（下方有命令）。

默认模式为 `Full`，不指定 `-SkipPdf` 时还会导出 PDF。`-NoRestore` 仅供 `Quick` 使用，要求依赖已还原；完整档始终执行锁定还原。

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

正式 xUnit 测试验证库存、状态转换、权限、Undo、Memento、解析优先级和装饰器顺序等业务契约；独立模式还直接验证取消订阅后的通知、Undo/Redo 内容与分支、克隆集合隔离，以及既有告警接入新渠道。空命令历史沿用抛出 `InvalidOperationException` 的约定。原有 `--self-test` 继续作为零依赖、可随手运行的教学入口。

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

GitHub Actions 在每次 push 和 pull request 时使用 .NET 10 自动执行锁定还原、格式检查、构建、xUnit 测试、全部轻量自检、文档校验和 HTML 指南导出。四个测试程序集都生成 Cobertura，且每份报告必须达到行覆盖率 55%、分支覆盖率 40% 的防回退基线。站点还会校验学习目录 JSON Schema 与交叉引用，对 Pages 验证器执行故障注入自测，并在系统 Chrome 中执行桌面与 390px 移动端回归、顺序证据、进度备份、全文搜索、测验复习、深链接、横向溢出和 axe 严重级可访问性检查。只有 Windows/Linux 验证与浏览器检查全部通过，主分支才会发布 GitHub Pages，并核对线上 commit 与关键资源。PDF 由手动或版本标签工作流生成。

## 在线学习站

GitHub Pages 首页位于 `site/`；主分支更新时，[CI 与 Pages 工作流](.github/workflows/ci.yml) 会生成并发布 学习仪表盘、辨析训练页、C# 编码页、Markdown 指南和独立模式课件。每个模式都有“阅读 → 运行 → 改造 → 验证”四项真实证据，三个项目与两个实验提供顺序里程碑，任务数由目录生成；首页提供全文搜索、JSON/Markdown 进度备份、24 道间隔复习辨析题及 8 道浏览器 C# 编码练习。编码验收完成度单独统计。

模式名称、顺序、分类、意图与真实预期输出来自 `PatternCatalog` 和 Runner；网站增量字段集中在 `site/data/learning-catalog.json`，并由 `learning-catalog.schema.json` 与语义验证器约束。指南清单、搜索索引生成和 Pages 产物验证已经拆成独立脚本，Markdown 模式索引仍由同一目录同步生成。可在本地复现同一份静态产物：

```powershell
pwsh -File ./scripts/build-pages.ps1
```

学习进度与复习记录保存在当前浏览器。如果浏览器禁止存储或空间不足，首页、模式课件、项目指南和测验页会显示保存失败提示；当前页面仍可操作，并可直接下载未保存记录。请在刷新或离开前备份，之后可在首页导入。成功保存后提示自动消失。

生成结果位于 `output/pages-site/`。构建会根据目录生成并校验页面总数、canonical URL、JSON-LD、sitemap、模式条目、里程碑、辨析题、编码题和搜索索引、源码与实战路径、教程锚点及全部站内链接。也可以独立复核已生成产物：

```powershell
pwsh -File ./scripts/verify-pages-site.ps1 -SiteDirectory output/pages-site
```

需要验证真实浏览器行为时先安装锁定的轻量测试依赖：

```powershell
npm ci
npm run test:site
```

修改模式目录后可运行 `pwsh -File ./scripts/sync-pattern-index.ps1` 更新 [23 种模式索引](docs/模式索引.md)；CI 会拒绝未同步的索引。

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

完整验证并生成 PDF：

```powershell
pwsh -File scripts/verify.ps1
```

日常快速验证（不构建站点、不启动浏览器、不导出 PDF）：

```powershell
pwsh -File scripts/verify.ps1 -Mode Quick
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

## 浏览器 C# 练习与构建

打开学习站的“编码练习”，选择题目，修改源码并运行逐项验收。初始实现故意包含缺陷，参考答案折叠显示。四道改错题覆盖 Observer 取消订阅、Command 撤销／重做、Prototype 克隆隔离和 State 非法转换；四道扩展题覆盖 Strategy 折扣边界、Bridge 新渠道、责任链短路和简单函数重构。

题目、初始代码、参考答案和验收代码唯一来源是 [coding-exercises.json](site/data/coding-exercises.json)。[ExerciseCompiler](tools/ExerciseEngine/ExerciseCompiler.cs) 是浏览器和本地测试共用的 Roslyn 编译／执行核心。[playground.html](site/playground.html) 保留普通 JavaScript 页面，运行时采用 [.NET 独立 Web Worker](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers?view=aspnetcore-10.0)，每次运行创建新 Worker，结束、停止或超时后销毁。编译使用 C# 14，关闭并行编译，避免浏览器单线程等待问题。

首次运行才下载 .NET 和 Roslyn，首次下载可能较慢。仅提供纯 C#、集合、LINQ 等练习所需引用，没有任意 NuGet 安装、数据库或 ASP.NET 宿主。源码上限 100 KB，输出上限 64 KB；页面分别限制加载 60 秒、编译 30 秒和执行 5 秒。停止由页面终止 Worker，所以无限循环不会阻塞编辑界面。诊断、代码与输出均按文本显示。

草稿和通过记录按题目 ID／版本保存，编辑后撤销当前通过状态。完整 JSON 备份增加可选 exercises 字段，旧备份仍可导入；编码页也可单独备份。保存失败时使用全站统一提示，下载内存中的最新草稿后可恢复。

浏览器项目的 WebAssembly SDK 包锁定于 .NET SDK 10.0.401 对应版本，站点 CI 固定使用该 SDK。完整构建需要 PowerShell 7、.NET SDK 10.0.401、Node.js 24、Chrome／Edge，以及两个 WebAssembly 工作负载：

~~~powershell
dotnet workload install wasm-tools wasm-experimental
pwsh -File scripts/verify.ps1 -Mode Full -SkipPdf
# 仅重建站点（含浏览器编译器）
pwsh -File scripts/build-pages.ps1
node tests/site-server.mjs
# 打开 http://localhost:4173/playground.html
~~~

安装工作负载需要网络，系统级 SDK 可能需要管理员权限。若 Windows MSI 安装失败，可用微软 dotnet-install.ps1 在仓库外或忽略目录安装独立 SDK，再对该 dotnet.exe 安装工作负载，并设置 CSHARP_DESIGN_PATTERNS_WASM_DOTNET 为它的绝对路径。构建脚本使用此变量选择浏览器 SDK；普通解决方案仍使用 PATH 中的 dotnet。CI 的 site 作业自动安装工作负载并执行真实浏览器回归，Quick 保持无需工作负载。

~~~powershell
dotnet test tests/GuideExporter.Tests -c Release --filter FullyQualifiedName~ExerciseTests
npm run test:site
~~~

本地测试逐题证明初始代码失败、答案通过；浏览器回归运行同一份验收，并检查 C# 14 扩展块、编译诊断、运行异常、输出截断、无限循环超时、停止重跑、草稿备份、旧复习记录、390px 页面和可访问性。
