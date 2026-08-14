namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

public sealed class WebOutputComponentFactory : IOutputComponentFactory
{
    public OutputChannel Channel => OutputChannel.Web;

    public string FamilyName => "ResponsiveWebFamily";

    public IDocumentRenderer CreateRenderer() => new ResponsiveHtmlRenderer();

    public IArtifactPackager CreatePackager() => new WebBundlePackager();
}

public sealed class PrintOutputComponentFactory : IOutputComponentFactory
{
    public OutputChannel Channel => OutputChannel.Print;

    public string FamilyName => "PagedPrintFamily";

    public IDocumentRenderer CreateRenderer() => new PagedPrintRenderer();

    public IArtifactPackager CreatePackager() => new PrintBundlePackager();
}
