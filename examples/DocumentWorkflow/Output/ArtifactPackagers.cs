using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

public sealed class WebBundlePackager : IArtifactPackager
{
    public string ComponentName => nameof(WebBundlePackager);

    public PublicationPackage Package(ReportDocument document, RenderedArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(artifact);
        EnsureRenderer(artifact, nameof(ResponsiveHtmlRenderer));

        var files = new[] { "index.html", "styles.css", "publication-manifest.json" };
        var payload = $"FAMILY=ResponsiveWebFamily\nFILES={string.Join(',', files)}\n{artifact.Content}";
        return new PublicationPackage(
            OutputChannel.Web,
            $"{document.ReportId.ToLowerInvariant()}.sitepkg",
            "ResponsiveWebFamily",
            ComponentName,
            payload,
            files);
    }

    private static void EnsureRenderer(RenderedArtifact artifact, string renderer)
    {
        if (!artifact.Metadata.TryGetValue("Renderer", out var actual) ||
            !actual.Equals(renderer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Web 打包器只能接收 {renderer} 的产物。");
        }
    }
}

public sealed class PrintBundlePackager : IArtifactPackager
{
    public string ComponentName => nameof(PrintBundlePackager);

    public PublicationPackage Package(ReportDocument document, RenderedArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(artifact);
        EnsureRenderer(artifact, nameof(PagedPrintRenderer));

        var files = new[] { "report.prn", "print-profile.json", "publication-manifest.json" };
        var payload = $"FAMILY=PagedPrintFamily\nFILES={string.Join(',', files)}\n{artifact.Content}";
        return new PublicationPackage(
            OutputChannel.Print,
            $"{document.ReportId.ToLowerInvariant()}.printpkg",
            "PagedPrintFamily",
            ComponentName,
            payload,
            files);
    }

    private static void EnsureRenderer(RenderedArtifact artifact, string renderer)
    {
        if (!artifact.Metadata.TryGetValue("Renderer", out var actual) ||
            !actual.Equals(renderer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Print 打包器只能接收 {renderer} 的产物。");
        }
    }
}
