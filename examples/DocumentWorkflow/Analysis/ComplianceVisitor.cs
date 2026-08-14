using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;

public sealed record ComplianceCheck(string Target, string Rule, bool Passed, string Detail);

public sealed class ComplianceVisitor : IReportElementVisitor
{
    private readonly List<ComplianceCheck> _checks = [];

    public IReadOnlyList<ComplianceCheck> Checks => _checks;

    public IReadOnlyList<ComplianceCheck> BlockingIssues =>
        _checks.Where(check => !check.Passed).ToArray();

    public int PassedCount => _checks.Count(check => check.Passed);

    public void Visit(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        AddRequiredMetadataCheck(document, "Owner");
        AddRequiredMetadataCheck(document, "Classification");
    }

    public void Visit(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        _checks.Add(new ComplianceCheck(
            section.Id,
            "章节标题不能为空",
            !string.IsNullOrWhiteSpace(section.Title),
            $"title={section.Title}"));

        _checks.Add(new ComplianceCheck(
            section.Id,
            "章节正文不能为空",
            !string.IsNullOrWhiteSpace(section.Body),
            $"characters={section.Body.Length}"));

        var forbiddenExternalTag = section.Audience == Audience.External &&
                                   section.Tags.Any(tag =>
                                       tag.Equals("internal", StringComparison.OrdinalIgnoreCase) ||
                                       tag.Equals("draft", StringComparison.OrdinalIgnoreCase));
        _checks.Add(new ComplianceCheck(
            section.Id,
            "外部章节不得带 internal/draft 标签",
            !forbiddenExternalTag,
            forbiddenExternalTag ? "发现禁止标签" : "标签合规"));
    }

    private void AddRequiredMetadataCheck(ReportDocument document, string key)
    {
        var hasValue = document.Metadata.TryGetValue(key, out var value) &&
                       !string.IsNullOrWhiteSpace(value);
        _checks.Add(new ComplianceCheck(
            document.ReportId,
            $"元数据 {key} 必填",
            hasValue,
            hasValue ? $"{key}={value}" : $"缺少 {key}"));
    }
}
