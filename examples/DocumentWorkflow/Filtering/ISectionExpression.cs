using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Filtering;

/// <summary>
/// Interpreter 的抽象表达式。每个表达式都能针对一个章节解释自身。
/// </summary>
public interface ISectionExpression
{
    bool Interpret(ReportSection section);

    string Describe();
}
