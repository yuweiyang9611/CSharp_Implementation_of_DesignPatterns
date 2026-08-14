using System.Text;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Pipeline;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Demo;

public static class ScenarioReportFormatter
{
    public static string Format(DocumentWorkflowScenarioResult scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var builder = new StringBuilder();
        builder.AppendLine("=== 企业季度报表发布教学场景 ===");
        builder.AppendLine($"Prototype：模板 {scenario.Template.ReportId} 被独立克隆；模板仍有 {scenario.Template.Sections.Count} 个章节。");
        builder.AppendLine(
            $"Flyweight：4 次样式引用只创建 {scenario.SharedStyles.Count} 个样式对象；重复 Body 引用同一实例={scenario.RepeatedBodyStyleIsShared}。");
        builder.AppendLine($"共享样式：{string.Join(", ", scenario.SharedStyles.Select(style => style.Name))}");
        builder.AppendLine();
        AppendPublication(builder, "Web 发布", scenario.WebPublication);
        builder.AppendLine();
        AppendPublication(builder, "Print 发布", scenario.PrintPublication);
        builder.AppendLine();
        builder.AppendLine(
            $"Prototype 隔离证明：模板首章含 published-copy={scenario.Template.Sections[0].Tags.Contains("published-copy", StringComparer.OrdinalIgnoreCase)}；Web 副本含 published-copy={scenario.WebPublication.Document.Sections[0].Tags.Contains("published-copy", StringComparer.OrdinalIgnoreCase)}。");
        return builder.ToString().TrimEnd();
    }

    private static void AppendPublication(
        StringBuilder builder,
        string heading,
        PublishingResult publication)
    {
        builder.AppendLine($"--- {heading} ---");
        builder.AppendLine("Template Method 阶段：");
        foreach (var step in publication.Trace)
        {
            builder.AppendLine($"  {step.Stage,-24} | {step.Detail}");
        }

        builder.AppendLine($"Interpreter：{publication.NormalizedFilter}");
        builder.AppendLine(
            $"Iterator 顺序：{string.Join(" -> ", publication.Document.Sections.Select(section => section.Id))}");
        builder.AppendLine(
            $"Visitor 统计：sections={publication.Statistics.SectionCount}, pages={publication.Statistics.EstimatedPageCount}, words={publication.Statistics.WordCount}");
        builder.AppendLine(
            $"Visitor 合规：passed={publication.Compliance.PassedCount}, blocking={publication.Compliance.BlockingIssues.Count}");
        builder.AppendLine(
            $"Abstract Factory：{publication.ComponentFamily} / {publication.Artifact.Metadata["Renderer"]} / {publication.Package.PackagerName}");
        builder.AppendLine($"Decorator 审计链：{string.Join(" -> ", publication.Artifact.AuditTrail)}");
        builder.AppendLine(
            $"发布包：{publication.Package.PackageName} [{string.Join(", ", publication.Package.Files)}]");
        builder.AppendLine($"确定性签名：{publication.Artifact.Metadata["Signature"]}");
    }
}
