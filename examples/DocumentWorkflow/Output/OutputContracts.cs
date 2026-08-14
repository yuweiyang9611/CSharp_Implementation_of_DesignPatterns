using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

public enum OutputChannel
{
    Web,
    Print
}

public interface IArtifactProducer
{
    RenderedArtifact Produce(ReportDocument document);
}

public interface IDocumentRenderer : IArtifactProducer
{
    string ComponentName { get; }
}

public interface IArtifactPackager
{
    string ComponentName { get; }

    PublicationPackage Package(ReportDocument document, RenderedArtifact artifact);
}

/// <summary>
/// Abstract Factory：一次创建相互匹配的渲染器与打包器。
/// </summary>
public interface IOutputComponentFactory
{
    OutputChannel Channel { get; }

    string FamilyName { get; }

    IDocumentRenderer CreateRenderer();

    IArtifactPackager CreatePackager();
}
