using DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Filtering;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Pipeline;

/// <summary>
/// Template Method：Publish 是不可覆盖的发布骨架，子类只提供变化点。
/// </summary>
public abstract class PublishingPipeline
{
    public PublishingResult Publish(PublishingRequest request)
    {
        ValidateRequest(request);
        var trace = new List<PublishingTrace>();

        var document = request.Template.DeepClone();
        trace.Add(new PublishingTrace(
            PublishingStage.ClonePrototype,
            $"从模板 {request.Template.ReportId} 深克隆文档"));

        CustomizeDocument(document, request);
        trace.Add(new PublishingTrace(
            PublishingStage.CustomizeDocument,
            $"生成 {document.ReportId} 并写入 {request.Metadata.Count} 项元数据"));

        var expression = CreateFilterParser().Parse(request.FilterExpression);
        var originalSectionCount = document.Sections.Count;
        document.ReplaceSections(document.Sections.SelectMatching(expression.Interpret));
        var removedSectionCount = originalSectionCount - document.Sections.Count;
        trace.Add(new PublishingTrace(
            PublishingStage.InterpretSectionFilter,
            $"{expression.Describe()}，保留 {document.Sections.Count}，移除 {removedSectionCount}"));

        var statistics = new ReportStatisticsVisitor();
        var compliance = new ComplianceVisitor();
        document.Accept(statistics);
        document.Accept(compliance);
        trace.Add(new PublishingTrace(
            PublishingStage.RunVisitors,
            $"统计 {statistics.SectionCount} 个章节；合规通过 {compliance.PassedCount} 项"));

        if (compliance.BlockingIssues.Count > 0)
        {
            var issues = string.Join(
                "; ",
                compliance.BlockingIssues.Select(issue => $"{issue.Target}: {issue.Rule}"));
            throw new InvalidOperationException($"发布被合规 Visitor 阻止：{issues}");
        }

        var factory = CreateOutputComponentFactory(request.Channel);
        var renderer = factory.CreateRenderer();
        var packager = factory.CreatePackager();
        trace.Add(new PublishingTrace(
            PublishingStage.CreateOutputFamily,
            $"{factory.FamilyName}: {renderer.ComponentName} + {packager.ComponentName}"));

        var decoratedProducer = ConfigureDecorators(renderer, request);
        var artifact = decoratedProducer.Produce(document);
        trace.Add(new PublishingTrace(
            PublishingStage.ApplyDecorators,
            string.Join(" -> ", artifact.AuditTrail.Skip(1).Select(DecorationName))));

        var package = packager.Package(document, artifact);
        trace.Add(new PublishingTrace(
            PublishingStage.PackagePublication,
            $"生成 {package.PackageName}（{package.Files.Count} 个逻辑文件）"));

        return new PublishingResult(
            document,
            expression.Describe(),
            removedSectionCount,
            statistics,
            compliance,
            factory.FamilyName,
            artifact,
            package,
            trace);
    }

    protected virtual SectionFilterParser CreateFilterParser() => new();

    protected virtual void CustomizeDocument(ReportDocument document, PublishingRequest request)
    {
        document.SetPublicationIdentity(request.ReportId, request.Title);
        foreach (var pair in request.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            document.SetMetadata(pair.Key, pair.Value);
        }

        if (document.Sections.Count > 0)
        {
            document.Sections[0].AddTag("published-copy");
        }
    }

    protected abstract IOutputComponentFactory CreateOutputComponentFactory(OutputChannel channel);

    protected virtual IArtifactProducer ConfigureDecorators(
        IDocumentRenderer renderer,
        PublishingRequest request)
    {
        IArtifactProducer producer = renderer;
        producer = new WatermarkDecorator(producer, request.Watermark);
        producer = new SignatureDecorator(producer);
        producer = new AuditDecorator(producer, request.PublishedBy);
        return producer;
    }

    private static string DecorationName(string auditEntry)
    {
        var separator = auditEntry.IndexOf(':', StringComparison.Ordinal);
        return separator >= 0 ? auditEntry[..separator] : auditEntry;
    }

    private static void ValidateRequest(PublishingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Template);
        ArgumentNullException.ThrowIfNull(request.Metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FilterExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Watermark);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PublishedBy);
    }
}
