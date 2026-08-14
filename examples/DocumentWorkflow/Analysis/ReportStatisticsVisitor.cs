using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;

public sealed class ReportStatisticsVisitor : IReportElementVisitor
{
    private static readonly char[] WordSeparators =
        [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?'];

    public int DocumentCount { get; private set; }

    public int SectionCount { get; private set; }

    public int EstimatedPageCount { get; private set; }

    public int WordCount { get; private set; }

    public void Visit(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DocumentCount++;
    }

    public void Visit(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        SectionCount++;
        EstimatedPageCount += section.EstimatedPages;
        WordCount += section.Body.Split(
            WordSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }
}
