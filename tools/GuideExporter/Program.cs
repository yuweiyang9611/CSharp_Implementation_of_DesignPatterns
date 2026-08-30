using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

return await GuideExporter.RunAsync(args);

internal static class GuideExporter
{
    private const string DefaultInput = "docs/CSharp设计模式学习指南.md";
    private const string DefaultOutput = "output/pdf/CSharp设计模式学习指南.pdf";

    public static async Task<int> RunAsync(string[] arguments)
    {
        var root = FindRepositoryRoot();
        var positional = arguments.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var configuredInput = Environment.GetEnvironmentVariable("CSHARP_DESIGN_PATTERNS_GUIDE_INPUT_PATH");
        var configuredOutput = Environment.GetEnvironmentVariable("CSHARP_DESIGN_PATTERNS_GUIDE_OUTPUT_PATH");
        var inputPath = Path.GetFullPath(
            positional.ElementAtOrDefault(0)
            ?? configuredInput
            ?? Path.Combine(root, DefaultInput));
        var outputPath = Path.GetFullPath(
            positional.ElementAtOrDefault(1)
            ?? configuredOutput
            ?? Path.Combine(root, DefaultOutput));
        var htmlOnly = arguments.Contains("--html-only", StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Markdown file not found: {inputPath}");
            return 2;
        }

        var markdown = await File.ReadAllTextAsync(inputPath, Encoding.UTF8);
        var title = MarkdownRenderer.FindTitle(markdown) ?? Path.GetFileNameWithoutExtension(inputPath);
        var repositoryBlobBase = Environment.GetEnvironmentVariable(
            "CSHARP_DESIGN_PATTERNS_REPOSITORY_BLOB_BASE");

        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("The output path must have a parent directory.");
        Directory.CreateDirectory(outputDirectory);

        var htmlPath = Path.ChangeExtension(outputPath, ".html");
        var html = MarkdownRenderer.Render(
            markdown,
            title,
            inputPath,
            htmlPath,
            root,
            repositoryBlobBase);
        await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (htmlOnly)
        {
            Console.WriteLine($"HTML generated: {htmlPath}");
            return 0;
        }

        var browserPath = BrowserLocator.Find();
        if (browserPath is null)
        {
            Console.Error.WriteLine("No supported Chromium browser was found. HTML was still generated at:");
            Console.Error.WriteLine(htmlPath);
            Console.Error.WriteLine("Install Microsoft Edge, Google Chrome, or Chromium, then run the command again.");
            return 3;
        }

        var profilePath = Path.Combine(Path.GetTempPath(), $"design-pattern-guide-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profilePath);
        var temporaryPdfPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            var startInfo = new ProcessStartInfo(browserPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("--headless=new");
            startInfo.ArgumentList.Add("--disable-gpu");
            startInfo.ArgumentList.Add("--disable-background-networking");
            startInfo.ArgumentList.Add("--disable-component-update");
            startInfo.ArgumentList.Add("--disable-default-apps");
            startInfo.ArgumentList.Add("--disable-extensions");
            startInfo.ArgumentList.Add("--disable-features=SkiaGraphite,UseDMSAA");
            startInfo.ArgumentList.Add("--disable-sync");
            startInfo.ArgumentList.Add("--metrics-recording-only");
            startInfo.ArgumentList.Add("--no-default-browser-check");
            startInfo.ArgumentList.Add("--no-first-run");
            startInfo.ArgumentList.Add("--no-pdf-header-footer");
            startInfo.ArgumentList.Add($"--user-data-dir={profilePath}");
            startInfo.ArgumentList.Add($"--print-to-pdf={temporaryPdfPath}");
            startInfo.ArgumentList.Add(new Uri(htmlPath).AbsoluteUri);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the browser process.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }

                Console.Error.WriteLine("Browser PDF export timed out after 60 seconds.");
                return 5;
            }

            if (process.ExitCode != 0 ||
                !File.Exists(temporaryPdfPath) ||
                new FileInfo(temporaryPdfPath).Length == 0)
            {
                Console.Error.WriteLine($"Browser PDF export failed with exit code {process.ExitCode}.");
                return 4;
            }

            if (File.Exists(outputPath))
            {
                File.Replace(temporaryPdfPath, outputPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPdfPath, outputPath);
            }

            Console.WriteLine($"PDF generated: {outputPath}");
            Console.WriteLine($"HTML preview: {htmlPath}");
            return 0;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPdfPath);
            }
            catch (IOException)
            {
                // A force-closed browser can briefly retain the temporary output.
            }
            catch (UnauthorizedAccessException)
            {
                // The next export uses a unique name, so a stale file is harmless.
            }

            try
            {
                Directory.Delete(profilePath, recursive: true);
            }
            catch (IOException)
            {
                // Edge can briefly retain cache files after the main process exits.
            }
            catch (UnauthorizedAccessException)
            {
                // A leftover temporary profile is harmless and can be removed later.
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DesignPatterns.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

internal static partial class BrowserLocator
{
    public static string? Find()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("EDGE_PATH"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/usr/bin/microsoft-edge",
            "/usr/bin/google-chrome",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }
}

internal static partial class MarkdownRenderer
{
    private static readonly Regex HeadingRegex = CreateHeadingRegex();
    private static readonly Regex FenceRegex = CreateFenceRegex();
    private static readonly Regex OrderedListRegex = CreateOrderedListRegex();
    private static readonly Regex TableSeparatorRegex = CreateTableSeparatorRegex();
    private static readonly Regex CodeSpanRegex = CreateCodeSpanRegex();
    private static readonly Regex LinkRegex = CreateLinkRegex();
    private static readonly Regex BoldRegex = CreateBoldRegex();

    public static string? FindTitle(string markdown)
    {
        return markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => HeadingRegex.Match(line))
            .Where(match => match.Success && match.Groups[1].Value.Length == 1)
            .Select(match => match.Groups[2].Value.Trim())
            .FirstOrDefault();
    }

    public static string Render(
        string markdown,
        string title,
        string markdownPath,
        string htmlPath,
        string? repositoryRoot = null,
        string? repositoryBlobBase = null)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var body = new StringBuilder();
        var paragraph = new List<string>();
        var headingIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var tableCount = 0;
        var codeBlockCount = 0;
        var inCode = false;
        var codeLanguage = string.Empty;
        var code = new StringBuilder();
        var listKind = ListKind.None;
        var markdownDirectory = Path.GetDirectoryName(markdownPath)
            ?? throw new InvalidOperationException("The Markdown path must have a parent directory.");
        var htmlDirectory = Path.GetDirectoryName(htmlPath)
            ?? throw new InvalidOperationException("The HTML path must have a parent directory.");

        string RenderInline(string value) => Inline(
            value,
            markdownDirectory,
            htmlDirectory,
            repositoryRoot,
            repositoryBlobBase);

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            body.Append("<p>");
            body.Append(RenderInline(string.Join(' ', paragraph)));
            body.AppendLine("</p>");
            paragraph.Clear();
        }

        void CloseList()
        {
            if (listKind == ListKind.Unordered)
            {
                body.AppendLine("</ul>");
            }
            else if (listKind == ListKind.Ordered)
            {
                body.AppendLine("</ol>");
            }

            listKind = ListKind.None;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var fenceMatch = FenceRegex.Match(line);
            if (fenceMatch.Success)
            {
                FlushParagraph();
                CloseList();
                if (!inCode)
                {
                    inCode = true;
                    codeLanguage = fenceMatch.Groups[1].Value.Trim();
                    code.Clear();
                }
                else
                {
                    var languageClass = string.IsNullOrWhiteSpace(codeLanguage)
                        ? string.Empty
                        : $" class=\"language-{WebUtility.HtmlEncode(codeLanguage)}\"";
                    codeBlockCount++;
                    body.Append($"<pre tabindex=\"0\" aria-label=\"代码块 {codeBlockCount}，可横向滚动\"><code{languageClass}>");
                    body.Append(WebUtility.HtmlEncode(code.ToString().TrimEnd('\r', '\n')));
                    body.AppendLine("</code></pre>");
                    inCode = false;
                }

                continue;
            }

            if (inCode)
            {
                code.AppendLine(line);
                continue;
            }

            if (line.Trim().Equals("<!-- pagebreak -->", StringComparison.OrdinalIgnoreCase))
            {
                FlushParagraph();
                CloseList();
                body.AppendLine("<div class=\"page-break\"></div>");
                continue;
            }

            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                FlushParagraph();
                CloseList();
                var level = headingMatch.Groups[1].Value.Length;
                var heading = headingMatch.Groups[2].Value.Trim();
                var baseId = Slugify(heading);
                headingIds.TryGetValue(baseId, out var duplicateCount);
                headingIds[baseId] = duplicateCount + 1;
                var id = duplicateCount == 0 ? baseId : $"{baseId}-{duplicateCount}";
                body.AppendLine($"<h{level} id=\"{WebUtility.HtmlEncode(id)}\">{RenderInline(heading)}</h{level}>");
                continue;
            }

            if (index + 1 < lines.Length && line.Contains('|') && TableSeparatorRegex.IsMatch(lines[index + 1]))
            {
                FlushParagraph();
                CloseList();
                var headers = SplitTableRow(line);
                index += 2;
                tableCount++;
                body.AppendLine($"<div class=\"table-wrap\" role=\"region\" aria-label=\"数据表 {tableCount}，可横向滚动\" tabindex=\"0\"><table><thead><tr>");
                foreach (var header in headers)
                {
                    body.AppendLine($"<th>{RenderInline(header)}</th>");
                }

                body.AppendLine("</tr></thead><tbody>");
                while (index < lines.Length && lines[index].Contains('|') && !string.IsNullOrWhiteSpace(lines[index]))
                {
                    body.AppendLine("<tr>");
                    foreach (var cell in SplitTableRow(lines[index]))
                    {
                        body.AppendLine($"<td>{RenderInline(cell)}</td>");
                    }

                    body.AppendLine("</tr>");
                    index++;
                }

                body.AppendLine("</tbody></table></div>");
                index--;
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushParagraph();
                CloseList();
                body.AppendLine($"<blockquote>{RenderInline(line[2..].Trim())}</blockquote>");
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                if (listKind != ListKind.Unordered)
                {
                    CloseList();
                    body.AppendLine("<ul>");
                    listKind = ListKind.Unordered;
                }

                body.AppendLine($"<li>{RenderInline(line[2..].Trim())}</li>");
                continue;
            }

            var orderedMatch = OrderedListRegex.Match(line);
            if (orderedMatch.Success)
            {
                FlushParagraph();
                if (listKind != ListKind.Ordered)
                {
                    CloseList();
                    body.AppendLine("<ol>");
                    listKind = ListKind.Ordered;
                }

                body.AppendLine($"<li>{RenderInline(orderedMatch.Groups[1].Value.Trim())}</li>");
                continue;
            }

            if (line.Trim() is "---" or "***")
            {
                FlushParagraph();
                CloseList();
                body.AppendLine("<hr>");
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                CloseList();
                continue;
            }

            paragraph.Add(line.Trim());
        }

        FlushParagraph();
        CloseList();
        if (inCode)
        {
            body.Append("<pre><code>");
            body.Append(WebUtility.HtmlEncode(code.ToString().TrimEnd('\r', '\n')));
            body.AppendLine("</code></pre>");
        }

        return HtmlDocument(title, body.ToString());
    }

    private static string[] SplitTableRow(string row)
    {
        return row.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
    }

    private static string Inline(
        string value,
        string markdownDirectory,
        string htmlDirectory,
        string? repositoryRoot,
        string? repositoryBlobBase)
    {
        var codeTokens = new List<string>();
        value = CodeSpanRegex.Replace(value, match =>
        {
            codeTokens.Add(match.Groups[1].Value);
            return $"@@CODE{codeTokens.Count - 1}@@";
        });

        var links = new List<(string Label, string Url)>();
        value = LinkRegex.Replace(value, match =>
        {
            links.Add((
                match.Groups[1].Value,
                RewriteRelativeLink(
                    match.Groups[2].Value,
                    markdownDirectory,
                    htmlDirectory,
                    repositoryRoot,
                    repositoryBlobBase)));
            return $"@@LINK{links.Count - 1}@@";
        });

        var encoded = WebUtility.HtmlEncode(value);
        encoded = BoldRegex.Replace(encoded, "<strong>$1</strong>");

        for (var index = 0; index < codeTokens.Count; index++)
        {
            encoded = encoded.Replace(
                $"@@CODE{index}@@",
                $"<code>{WebUtility.HtmlEncode(codeTokens[index])}</code>",
                StringComparison.Ordinal);
        }

        for (var index = 0; index < links.Count; index++)
        {
            var (label, url) = links[index];
            var encodedLabel = WebUtility.HtmlEncode(label);
            encodedLabel = BoldRegex.Replace(encodedLabel, "<strong>$1</strong>");
            for (var codeIndex = 0; codeIndex < codeTokens.Count; codeIndex++)
            {
                encodedLabel = encodedLabel.Replace(
                    $"@@CODE{codeIndex}@@",
                    $"<code>{WebUtility.HtmlEncode(codeTokens[codeIndex])}</code>",
                    StringComparison.Ordinal);
            }

            encoded = encoded.Replace(
                $"@@LINK{index}@@",
                $"<a href=\"{WebUtility.HtmlEncode(url)}\">{encodedLabel}</a>",
                StringComparison.Ordinal);
        }

        return encoded;
    }

    private static string RewriteRelativeLink(
        string url,
        string markdownDirectory,
        string htmlDirectory,
        string? repositoryRoot,
        string? repositoryBlobBase)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            url.StartsWith('#') ||
            Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        int suffixIndex = url.IndexOfAny(['#', '?']);
        string pathPart = suffixIndex >= 0 ? url[..suffixIndex] : url;
        string suffix = suffixIndex >= 0 ? url[suffixIndex..] : string.Empty;
        string platformPath = Uri.UnescapeDataString(pathPart)
            .Replace('/', Path.DirectorySeparatorChar);
        string absoluteTarget = Path.GetFullPath(Path.Combine(markdownDirectory, platformPath));
        if (!string.IsNullOrWhiteSpace(repositoryRoot) &&
            !string.IsNullOrWhiteSpace(repositoryBlobBase))
        {
            string relativeToRepository = Path.GetRelativePath(repositoryRoot, absoluteTarget);
            if (!relativeToRepository.Equals("..", StringComparison.Ordinal) &&
                !relativeToRepository.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                string escapedPath = string.Join(
                    '/',
                    relativeToRepository
                        .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(Uri.EscapeDataString));
                return $"{repositoryBlobBase.TrimEnd('/')}/{escapedPath}{suffix}";
            }
        }

        string relativeTarget = Path.GetRelativePath(htmlDirectory, absoluteTarget)
            .Replace(Path.DirectorySeparatorChar, '/');
        return relativeTarget + suffix;
    }

    private static string Slugify(string value)
    {
        var heading = LinkRegex.Replace(value, "$1").Replace("`", string.Empty, StringComparison.Ordinal);
        var slug = Regex.Replace(
            heading.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}\p{M}\s_-]",
            string.Empty);
        slug = Regex.Replace(slug, @"\s+", "-");
        return string.IsNullOrWhiteSpace(slug) ? "section" : slug;
    }

    private static string HtmlDocument(string title, string body)
    {
        return $$"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{WebUtility.HtmlEncode(title)}}</title>
              <style>
                :root { --ink: #172033; --muted: #556176; --accent: #275dad; --soft: #eef4fc; --line: #ced8e7; }
                @page {
                  size: A4;
                  margin: 17mm 16mm 19mm;
                  @bottom-center {
                    content: "{{WebUtility.HtmlEncode(title)}}  ·  " counter(page);
                    color: #697386;
                    font: 9pt "Microsoft YaHei", "Noto Sans CJK SC", sans-serif;
                  }
                }
                * { box-sizing: border-box; }
                html { color: var(--ink); font: 10.5pt/1.72 "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", sans-serif; }
                body { margin: 0; background: white; }
                main { max-width: 178mm; margin: 0 auto; }
                h1, h2, h3, h4 { color: #13294b; line-height: 1.3; break-after: avoid; }
                h1 { margin: 6mm 0 8mm; font-size: 28pt; letter-spacing: .04em; text-align: center; }
                h2 { margin: 9mm 0 7mm; padding-bottom: 3mm; border-bottom: 2px solid var(--accent); font-size: 21pt; }
                h3 { margin: 8mm 0 3mm; padding-left: 3mm; border-left: 4px solid var(--accent); font-size: 15.5pt; }
                h4 { margin: 5mm 0 2mm; color: #24466f; font-size: 12.5pt; }
                p { margin: 0 0 3.2mm; text-align: justify; orphans: 3; widows: 3; }
                a { color: #1c57a5; text-decoration: none; }
                strong { color: #13294b; }
                ul, ol { margin: 1mm 0 4mm 6mm; padding-left: 5mm; }
                li { margin: 1mm 0; }
                blockquote { margin: 4mm 0; padding: 3mm 4mm; border-left: 4px solid #5a85c2; background: var(--soft); color: #294362; break-inside: avoid; }
                code { padding: .12em .35em; border-radius: 3px; background: #eef1f5; font: 9.2pt/1.45 "Cascadia Mono", Consolas, monospace; }
                pre { margin: 3mm 0 5mm; padding: 4mm; border: 1px solid #27364d; border-radius: 5px; background: #101827; color: #eef5ff; font: 8.1pt/1.48 "Cascadia Mono", Consolas, monospace; white-space: pre-wrap; overflow-wrap: anywhere; box-decoration-break: clone; }
                pre code { padding: 0; background: transparent; color: inherit; font: inherit; }
                .table-wrap { max-width: 100%; margin: 3mm 0 5mm; }
                table { width: 100%; border-collapse: collapse; font-size: 9.3pt; }
                thead { display: table-header-group; }
                tr { break-inside: avoid; }
                th, td { padding: 2.1mm 2.4mm; border: 1px solid var(--line); text-align: left; vertical-align: top; }
                th { background: #dde9f8; color: #18385f; }
                tr:nth-child(even) td { background: #f7f9fc; }
                hr { margin: 7mm 0; border: 0; border-top: 1px solid var(--line); }
                .page-break { break-before: page; }
                @media screen {
                  body { background: #edf1f7; padding: 32px 16px; }
                  main { min-width: 0; padding: clamp(32px, 6vw, 68px) clamp(22px, 5vw, 60px); background: white; box-shadow: 0 3px 18px #25344b33; }
                  .table-wrap { min-width: 0; overflow-x: auto; overscroll-behavior-inline: contain; }
                  .table-wrap:focus-visible { outline: 3px solid #275dad; outline-offset: 3px; }
                  .table-wrap table { min-width: 640px; }
                  pre { max-width: 100%; white-space: pre; overflow-x: auto; overflow-wrap: normal; }
                  pre:focus-visible { outline: 3px solid #5a85c2; outline-offset: 3px; }
                }
                @media screen and (max-width: 720px) {
                  body { padding: 0; }
                  main { width: 100%; padding: 30px 18px 48px; box-shadow: none; }
                  h1 { margin-top: 0; font-size: clamp(28px, 9vw, 40px); text-align: left; }
                  h2 { font-size: clamp(24px, 7vw, 31px); }
                  h3 { font-size: clamp(20px, 5.5vw, 25px); }
                  p { text-align: left; overflow-wrap: anywhere; }
                  th, td { overflow-wrap: anywhere; }
                }
                @media print { h1:first-child { margin-top: 48mm; } a { color: inherit; } }
              </style>
            </head>
            <body><main>{{body}}</main></body>
            </html>
            """;
    }

    private enum ListKind
    {
        None,
        Unordered,
        Ordered,
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*$")]
    private static partial Regex CreateHeadingRegex();

    [GeneratedRegex(@"^```\s*([\w#+.-]*)\s*$")]
    private static partial Regex CreateFenceRegex();

    [GeneratedRegex(@"^\d+\.\s+(.+)$")]
    private static partial Regex CreateOrderedListRegex();

    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$")]
    private static partial Regex CreateTableSeparatorRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex CreateCodeSpanRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex CreateLinkRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex CreateBoldRegex();
}
