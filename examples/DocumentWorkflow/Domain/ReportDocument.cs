using DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

/// <summary>
/// 既是可访问的文档根节点，也是发布模板的 Prototype。
/// </summary>
public sealed class ReportDocument : IPrototype<ReportDocument>, IReportElement
{
    private readonly SortedDictionary<string, string> _metadata;

    public ReportDocument(
        string reportId,
        string title,
        string department,
        SectionCollection sections,
        IEnumerable<KeyValuePair<string, string>> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(department);
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(metadata);

        ReportId = reportId;
        Title = title;
        Department = department;
        Sections = sections;
        _metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in metadata)
        {
            _metadata[pair.Key] = pair.Value;
        }
    }

    public string ReportId { get; private set; }

    public string Title { get; private set; }

    public string Department { get; }

    public SectionCollection Sections { get; private set; }

    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    public void SetPublicationIdentity(string reportId, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ReportId = reportId;
        Title = title;
    }

    public void SetMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _metadata[key] = value;
    }

    public void ReplaceSections(SectionCollection sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Sections = sections;
    }

    public ReportDocument DeepClone() =>
        new(ReportId, Title, Department, Sections.DeepClone(), _metadata);

    public void Accept(IReportElementVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.Visit(this);
        foreach (var section in Sections)
        {
            section.Accept(visitor);
        }
    }
}
