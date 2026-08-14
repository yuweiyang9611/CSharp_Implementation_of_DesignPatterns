using System.Net;
using System.Text;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

public sealed class ResponsiveHtmlRenderer : IDocumentRenderer
{
    public string ComponentName => nameof(ResponsiveHtmlRenderer);

    public RenderedArtifact Produce(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><body>");
        builder.Append("<h1>").Append(WebUtility.HtmlEncode(document.Title)).AppendLine("</h1>");
        foreach (var section in document.Sections)
        {
            builder.Append("<section data-id=\"")
                .Append(WebUtility.HtmlEncode(section.Id))
                .Append("\" class=\"")
                .Append(WebUtility.HtmlEncode(section.Style.Name.ToLowerInvariant()))
                .AppendLine("\">");
            builder.Append("  <h2>").Append(WebUtility.HtmlEncode(section.Title)).AppendLine("</h2>");
            builder.Append("  <p>").Append(WebUtility.HtmlEncode(section.Body)).AppendLine("</p>");
            builder.AppendLine("</section>");
        }

        builder.AppendLine("</body></html>");

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Renderer"] = ComponentName,
            ["MediaType"] = "text/html; charset=utf-8"
        };
        return new RenderedArtifact(
            "html",
            builder.ToString().TrimEnd(),
            metadata,
            [$"Renderer: {ComponentName}"]);
    }
}

public sealed class PagedPrintRenderer : IDocumentRenderer
{
    public string ComponentName => nameof(PagedPrintRenderer);

    public RenderedArtifact Produce(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        builder.AppendLine($"REPORT: {document.Title}");
        builder.AppendLine($"ID: {document.ReportId}");
        builder.AppendLine(new string('=', 64));

        var page = 1;
        foreach (var section in document.Sections)
        {
            builder.AppendLine($"[PAGE {page:000}] {section.Title}");
            builder.AppendLine($"STYLE={section.Style.Name}; FONT={section.Style.FontFamily}; SIZE={section.Style.FontSize}");
            builder.AppendLine(section.Body);
            builder.AppendLine(new string('-', 64));
            page += section.EstimatedPages;
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Renderer"] = ComponentName,
            ["MediaType"] = "text/plain; profile=paged-print"
        };
        return new RenderedArtifact(
            "print-text",
            builder.ToString().TrimEnd(),
            metadata,
            [$"Renderer: {ComponentName}"]);
    }
}
