using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Filtering;

public sealed class AudienceExpression(Audience expected) : ISectionExpression
{
    public bool Interpret(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return section.Audience == expected;
    }

    public string Describe() => $"audience = {expected.ToString().ToLowerInvariant()}";
}

public sealed class TagExpression(string expectedTag) : ISectionExpression
{
    public bool Interpret(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return section.Tags.Contains(expectedTag, StringComparer.OrdinalIgnoreCase);
    }

    public string Describe() => $"tag = {expectedTag.ToLowerInvariant()}";
}

public sealed class MinimumPagesExpression(int minimumPages) : ISectionExpression
{
    public bool Interpret(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return section.EstimatedPages >= minimumPages;
    }

    public string Describe() => $"pages >= {minimumPages}";
}

public sealed class AndExpression(ISectionExpression left, ISectionExpression right) : ISectionExpression
{
    public bool Interpret(ReportSection section) =>
        left.Interpret(section) && right.Interpret(section);

    public string Describe() => $"({left.Describe()} AND {right.Describe()})";
}

public sealed class OrExpression(ISectionExpression left, ISectionExpression right) : ISectionExpression
{
    public bool Interpret(ReportSection section) =>
        left.Interpret(section) || right.Interpret(section);

    public string Describe() => $"({left.Describe()} OR {right.Describe()})";
}

public sealed class NotExpression(ISectionExpression operand) : ISectionExpression
{
    public bool Interpret(ReportSection section) => !operand.Interpret(section);

    public string Describe() => $"NOT ({operand.Describe()})";
}
