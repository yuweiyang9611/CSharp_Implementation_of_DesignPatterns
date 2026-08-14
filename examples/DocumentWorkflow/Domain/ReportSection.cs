using DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

public sealed class ReportSection : IPrototype<ReportSection>, IReportElement
{
    private readonly List<string> _tags;

    public ReportSection(
        string id,
        string title,
        string body,
        Audience audience,
        int estimatedPages,
        StyleDefinition style,
        IEnumerable<string> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedPages);

        Id = id;
        Title = title;
        Body = body;
        Audience = audience;
        EstimatedPages = estimatedPages;
        Style = style;
        _tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string Id { get; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public Audience Audience { get; }

    public int EstimatedPages { get; }

    public StyleDefinition Style { get; }

    public IReadOnlyList<string> Tags => _tags;

    public void Rename(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
    }

    public void ReplaceBody(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        Body = body;
    }

    public void AddTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        if (!_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(tag);
        }
    }

    public ReportSection DeepClone() =>
        new(Id, Title, Body, Audience, EstimatedPages, Style, _tags);

    public void Accept(IReportElementVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.Visit(this);
    }
}
