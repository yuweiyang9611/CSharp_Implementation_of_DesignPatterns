using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;

public interface IReportElement
{
    void Accept(IReportElementVisitor visitor);
}

/// <summary>
/// Visitor 把统计、合规等横切算法从文档节点中移出。
/// </summary>
public interface IReportElementVisitor
{
    void Visit(ReportDocument document);

    void Visit(ReportSection section);
}
