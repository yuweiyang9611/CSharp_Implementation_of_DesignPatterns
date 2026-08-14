using DesignPatterns.TeachingProjects.DocumentWorkflow.Analysis;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Pipeline;

public sealed record PublishingRequest(
    ReportDocument Template,
    string ReportId,
    string Title,
    IReadOnlyDictionary<string, string> Metadata,
    string FilterExpression,
    OutputChannel Channel,
    string Watermark,
    string PublishedBy);

public enum PublishingStage
{
    ClonePrototype,
    CustomizeDocument,
    InterpretSectionFilter,
    RunVisitors,
    CreateOutputFamily,
    ApplyDecorators,
    PackagePublication
}

public sealed record PublishingTrace(PublishingStage Stage, string Detail);

public sealed record PublishingResult(
    ReportDocument Document,
    string NormalizedFilter,
    int RemovedSectionCount,
    ReportStatisticsVisitor Statistics,
    ComplianceVisitor Compliance,
    string ComponentFamily,
    RenderedArtifact Artifact,
    PublicationPackage Package,
    IReadOnlyList<PublishingTrace> Trace);
