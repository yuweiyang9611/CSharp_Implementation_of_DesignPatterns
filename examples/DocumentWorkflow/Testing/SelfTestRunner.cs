using System.Security.Cryptography;
using System.Text;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Demo;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Filtering;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Output;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Pipeline;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Testing;

public static class SelfTestRunner
{
    public static int Run(TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var tests = new (string Name, Action Body)[]
        {
            ("Prototype 深克隆并隔离可变章节", PrototypeCloneIsIndependent),
            ("Prototype 克隆保留不可变 Flyweight", PrototypeSharesFlyweight),
            ("Flyweight 复用同名样式", FlyweightIsShared),
            ("Interpreter 正确筛选外部非草稿章节", InterpreterFiltersSections),
            ("Interpreter 遵守 AND 高于 OR 的优先级", InterpreterHonorsPrecedence),
            ("Iterator 保持章节业务顺序", IteratorPreservesOrder),
            ("Visitor 生成确定统计", StatisticsVisitorProducesExpectedTotals),
            ("Compliance Visitor 识别违规草稿", ComplianceVisitorFindsViolation),
            ("Template Method 阶段固定且完整", TemplateMethodHasFixedStages),
            ("Abstract Factory 创建匹配的 Web 组件族", WebFactoryCreatesMatchingFamily),
            ("Abstract Factory 创建匹配的 Print 组件族", PrintFactoryCreatesMatchingFamily),
            ("组件族拒绝错误渲染产物", PackagerRejectsMismatchedRenderer),
            ("Decorator 让签名覆盖水印并把审计放在最后", DecoratorsRunInExpectedOrder),
            ("Web 与 Print 产物具有渠道差异", ChannelsProduceDifferentArtifacts),
            ("完整场景可确定性重复运行", ScenarioIsDeterministic)
        };

        var passed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Body();
                output.WriteLine($"[PASS] {test.Name}");
                passed++;
            }
            catch (Exception exception)
            {
                error.WriteLine($"[FAIL] {test.Name}: {exception.Message}");
            }
        }

        if (passed == tests.Length)
        {
            output.WriteLine($"SELF-TEST PASSED: {passed}/{tests.Length}");
            return 0;
        }

        error.WriteLine($"SELF-TEST FAILED: {passed}/{tests.Length} passed");
        return 1;
    }

    private static void PrototypeCloneIsIndependent()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        Assert(scenario.Template.ReportId == "TEMPLATE-QUARTERLY", "模板 ID 被发布流程修改。 ");
        Assert(scenario.Template.Sections.Count == 4, "模板章节被筛选流程修改。 ");
        Assert(!ReferenceEquals(scenario.Template, scenario.WebPublication.Document), "发布文档仍是模板实例。 ");
        Assert(!ReferenceEquals(scenario.Template.Sections[0], scenario.WebPublication.Document.Sections[0]), "章节没有深克隆。 ");
        Assert(!scenario.Template.Sections[0].Tags.Contains("published-copy", StringComparer.OrdinalIgnoreCase), "模板标签被副本修改污染。 ");
        Assert(scenario.WebPublication.Document.Sections[0].Tags.Contains("published-copy", StringComparer.OrdinalIgnoreCase), "克隆副本未执行定制。 ");
    }

    private static void PrototypeSharesFlyweight()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        Assert(
            ReferenceEquals(scenario.Template.Sections[2].Style, scenario.WebPublication.Document.Sections[1].Style),
            "不可变样式不应被深复制。 ");
    }

    private static void FlyweightIsShared()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        Assert(scenario.SharedStyles.Count == 3, "预期只创建 3 个样式对象。 ");
        Assert(scenario.RepeatedBodyStyleIsShared, "重复 Body 样式未复用实例。 ");
        Assert(ReferenceEquals(scenario.Template.Sections[2].Style, scenario.Template.Sections[3].Style), "模板中的 Body 样式不是同一实例。 ");
    }

    private static void InterpreterFiltersSections()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        var ids = scenario.WebPublication.Document.Sections.Select(section => section.Id).ToArray();
        Assert(ids.SequenceEqual(["SEC-01", "SEC-03"], StringComparer.Ordinal), "筛选结果不是 SEC-01、SEC-03。 ");
        Assert(scenario.WebPublication.RemovedSectionCount == 2, "应移除两个章节。 ");
    }

    private static void InterpreterHonorsPrecedence()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        var expression = new SectionFilterParser().Parse(
            "tag = public OR tag = finance AND audience = internal");
        Assert(expression.Interpret(scenario.Template.Sections[0]), "public 章节应匹配 OR 左侧。 ");
        Assert(expression.Interpret(scenario.Template.Sections[1]), "内部 finance 章节应匹配 AND 分支。 ");
        Assert(!expression.Interpret(scenario.Template.Sections[3]), "draft 章节不应匹配。 ");
    }

    private static void IteratorPreservesOrder()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        using var iterator = scenario.Template.Sections.GetEnumerator();
        var ids = new List<string>();
        while (iterator.MoveNext())
        {
            ids.Add(iterator.Current.Id);
        }

        Assert(ids.SequenceEqual(["SEC-01", "SEC-02", "SEC-03", "SEC-04"], StringComparer.Ordinal), "迭代顺序变化。 ");
    }

    private static void StatisticsVisitorProducesExpectedTotals()
    {
        var statistics = DocumentWorkflowScenario.Execute().WebPublication.Statistics;
        Assert(statistics.DocumentCount == 1, "应访问一个文档根节点。 ");
        Assert(statistics.SectionCount == 2, "应统计两个发布章节。 ");
        Assert(statistics.EstimatedPageCount == 3, "预计页数应为 3。 ");
        Assert(statistics.WordCount == 22, $"单词数应为 22，实际 {statistics.WordCount}。 ");
    }

    private static void ComplianceVisitorFindsViolation()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        var visitor = new ComplianceVisitor();
        scenario.Template.Accept(visitor);
        Assert(visitor.BlockingIssues.Any(issue => issue.Target == "SEC-04"), "未识别外部草稿章节。 ");
        Assert(scenario.WebPublication.Compliance.BlockingIssues.Count == 0, "过滤后的发布文档不应有阻断项。 ");
    }

    private static void TemplateMethodHasFixedStages()
    {
        var stages = DocumentWorkflowScenario.Execute().WebPublication.Trace.Select(step => step.Stage).ToArray();
        var expected = Enum.GetValues<PublishingStage>();
        Assert(stages.SequenceEqual(expected), "发布阶段顺序与固定骨架不一致。 ");
    }

    private static void WebFactoryCreatesMatchingFamily()
    {
        var publication = DocumentWorkflowScenario.Execute().WebPublication;
        Assert(publication.ComponentFamily == "ResponsiveWebFamily", "Web 组件族错误。 ");
        Assert(publication.Artifact.Metadata["Renderer"] == nameof(ResponsiveHtmlRenderer), "Web 渲染器错误。 ");
        Assert(publication.Package.PackagerName == nameof(WebBundlePackager), "Web 打包器错误。 ");
        Assert(publication.Package.PackageName.EndsWith(".sitepkg", StringComparison.Ordinal), "Web 包扩展名错误。 ");
    }

    private static void PrintFactoryCreatesMatchingFamily()
    {
        var publication = DocumentWorkflowScenario.Execute().PrintPublication;
        Assert(publication.ComponentFamily == "PagedPrintFamily", "Print 组件族错误。 ");
        Assert(publication.Artifact.Metadata["Renderer"] == nameof(PagedPrintRenderer), "Print 渲染器错误。 ");
        Assert(publication.Package.PackagerName == nameof(PrintBundlePackager), "Print 打包器错误。 ");
        Assert(publication.Package.PackageName.EndsWith(".printpkg", StringComparison.Ordinal), "Print 包扩展名错误。 ");
    }

    private static void PackagerRejectsMismatchedRenderer()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        var printArtifact = new PagedPrintRenderer().Produce(scenario.Template);
        AssertThrows<InvalidOperationException>(
            () => new WebBundlePackager().Package(scenario.Template, printArtifact),
            "Web 打包器接受了 Print 渲染产物。 ");
    }

    private static void DecoratorsRunInExpectedOrder()
    {
        var artifact = DocumentWorkflowScenario.Execute().WebPublication.Artifact;
        Assert(artifact.Metadata.ContainsKey("Watermark"), "缺少水印元数据。 ");
        Assert(artifact.Metadata.ContainsKey("Signature"), "缺少签名元数据。 ");
        Assert(artifact.Metadata.ContainsKey("AuditActor"), "缺少审计元数据。 ");

        const string watermarkMarker = "<!-- WATERMARK: TRAINING COPY -->";
        const string signaturePrefix = "\n<!-- SIGNATURE:";
        const string auditPrefix = "\n<!-- AUDIT:";
        int watermarkIndex = artifact.Content.IndexOf(watermarkMarker, StringComparison.Ordinal);
        int signatureIndex = artifact.Content.IndexOf(signaturePrefix, StringComparison.Ordinal);
        int auditIndex = artifact.Content.IndexOf(auditPrefix, StringComparison.Ordinal);

        Assert(watermarkIndex >= 0, "最终内容缺少水印。 ");
        Assert(signatureIndex > watermarkIndex, "签名必须在水印之后生成。 ");
        Assert(auditIndex > signatureIndex, "审计记录必须在签名之后追加。 ");

        string contentCoveredBySignature = artifact.Content[..signatureIndex];
        string expectedSignature = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(contentCoveredBySignature)))[..16];
        Assert(
            artifact.Metadata["Signature"] == expectedSignature,
            "签名没有覆盖包含水印的最终业务内容。 ");
    }

    private static void ChannelsProduceDifferentArtifacts()
    {
        var scenario = DocumentWorkflowScenario.Execute();
        Assert(scenario.WebPublication.Artifact.Format == "html", "Web 格式错误。 ");
        Assert(scenario.PrintPublication.Artifact.Format == "print-text", "Print 格式错误。 ");
        Assert(
            scenario.WebPublication.Artifact.Metadata["Signature"] != scenario.PrintPublication.Artifact.Metadata["Signature"],
            "不同渠道应因渲染内容不同而具有不同签名。 ");
    }

    private static void ScenarioIsDeterministic()
    {
        var first = ScenarioReportFormatter.Format(DocumentWorkflowScenario.Execute());
        var second = ScenarioReportFormatter.Format(DocumentWorkflowScenario.Execute());
        Assert(first == second, "重复执行产生了不同输出。 ");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message.Trim());
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message.Trim());
    }
}
