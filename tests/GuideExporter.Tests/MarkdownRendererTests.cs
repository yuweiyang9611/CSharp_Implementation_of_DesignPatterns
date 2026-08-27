namespace DesignPatterns.GuideExporter.Tests;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void FindTitle_ReturnsTheFirstLevelOneHeading()
    {
        const string markdown = "## Preface\n# Course Title\n# Ignored";

        Assert.Equal("Course Title", MarkdownRenderer.FindTitle(markdown));
    }

    [Fact]
    public void Render_UsesGithubCompatibleAnchorsIncludingDuplicates()
    {
        const string markdown = "# 3.1 Adapter（适配器）\n## Same heading\n## Same heading";

        string html = Render(markdown);

        Assert.Contains("id=\"31-adapter适配器\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"same-heading\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"same-heading-1\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EncodesCodeAndBuildsTables()
    {
        const string markdown = """
            # Demo

            | Input | Output |
            | --- | --- |
            | `<tag>` | **safe** |

            ```csharp
            if (left < right) return;
            ```
            """;

        string html = Render(markdown);

        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("<code>&lt;tag&gt;</code>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>safe</strong>", html, StringComparison.Ordinal);
        Assert.Contains("if (left &lt; right) return;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_RewritesRelativeLinksFromMarkdownToHtmlDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"guide-renderer-{Guid.NewGuid():N}");
        string markdownPath = Path.Combine(root, "docs", "guide.md");
        string htmlPath = Path.Combine(root, "output", "guide.html");

        string html = MarkdownRenderer.Render(
            "# Guide\n[Source](../src/Demo.cs#example)",
            "Guide",
            markdownPath,
            htmlPath);

        Assert.Contains("href=\"../src/Demo.cs#example\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_CanRewriteRepositoryLinksForPortableArtifacts()
    {
        string root = Path.Combine(Path.GetTempPath(), $"guide-renderer-{Guid.NewGuid():N}");
        string markdownPath = Path.Combine(root, "docs", "guide.md");
        string htmlPath = Path.Combine(root, "output", "guide.html");

        string html = MarkdownRenderer.Render(
            "# Guide\n[源码](../src/示例.cs#入口)",
            "Guide",
            markdownPath,
            htmlPath,
            root,
            "https://github.com/example/repo/blob/abc123/");

        Assert.Contains(
            "href=\"https://github.com/example/repo/blob/abc123/src/%E7%A4%BA%E4%BE%8B.cs#入口\"",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PreservesInlineFormattingInsideLinkLabels()
    {
        const string markdown = "# Guide\nOpen [`labs`](../labs/README.md) and [**course guide**](guide.md).";

        string html = Render(markdown);

        Assert.Contains("<a href=\"../labs/README.md\"><code>labs</code></a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"../docs/guide.md\"><strong>course guide</strong></a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("@@CODE", html, StringComparison.Ordinal);
        Assert.DoesNotContain("@@LINK", html, StringComparison.Ordinal);
    }

    private static string Render(string markdown)
    {
        string root = Path.Combine(Path.GetTempPath(), "guide-renderer-tests");
        return MarkdownRenderer.Render(
            markdown,
            "Test Guide",
            Path.Combine(root, "docs", "guide.md"),
            Path.Combine(root, "output", "guide.html"));
    }
}
