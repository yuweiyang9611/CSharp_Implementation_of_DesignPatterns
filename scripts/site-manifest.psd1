@{
  Guides = @(
    @{ Input = 'README.md'; Output = 'repository-overview.html'; Description = 'C# 设计模式学习项目的课程结构、运行方式与仓库说明。'; Type = 'Guide' }
    @{ Input = 'START_HERE.md'; Output = 'learning-path.html'; Description = '从 30 分钟到 14 周的 C# 设计模式学习路线。'; Type = 'Learning Path' }
    @{ Input = 'docs/模式索引.md'; Output = 'pattern-index.html'; Description = 'GoF 23 种设计模式的 Runner key、源码、实战落点与教程索引。'; Type = 'Reference' }
    @{ Input = 'docs/CSharp设计模式学习指南.md'; Output = 'fundamentals.html'; Description = 'GoF 23 种设计模式的现代 C# 实现、意图、角色、取舍与练习。'; Type = 'Guide' }
    @{ Input = 'docs/设计模式实战项目学习指南.md'; Output = 'practice.html'; Description = 'OnlineStore、SmartHome 与 DocumentWorkflow 的设计模式组合实战指南。'; Type = 'Guide' }
    @{ Input = 'examples/README.md'; Output = 'projects.html'; Description = '三个教学项目的模式覆盖、运行方式与建议学习顺序。'; Type = 'Reference' }
    @{ Input = 'examples/OnlineStore/README.md'; Output = 'online-store.html'; Description = '用电商结算、支付与订单生命周期学习七种设计模式。'; Type = 'Project Guide' }
    @{ Input = 'examples/SmartHome/README.md'; Output = 'smart-home.html'; Description = '用智能家居设备接入、联动、撤销与恢复学习八种设计模式。'; Type = 'Project Guide' }
    @{ Input = 'examples/DocumentWorkflow/README.md'; Output = 'document-workflow.html'; Description = '用报表筛选、合规检查与多渠道发布学习八种设计模式。'; Type = 'Project Guide' }
    @{ Input = 'labs/README.md'; Output = 'labs.html'; Description = '从模式组合继续走向安全重构与生产可靠性的高级实验地图。'; Type = 'Lab Index' }
    @{ Input = 'labs/CheckoutRefactoringKata/README.md'; Output = 'refactoring.html'; Description = '从坏代码经特征测试逐步重构出 Strategy、Chain、State 与 Facade。'; Type = 'Lab' }
    @{ Input = 'labs/ReliableCheckout/README.md'; Output = 'reliable-checkout.html'; Description = '用 HTTP、SQLite、幂等、Outbox 与重试保护结账业务不变量。'; Type = 'Lab' }
  )
}
