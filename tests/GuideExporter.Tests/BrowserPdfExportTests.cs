using System.Diagnostics;

namespace DesignPatterns.GuideExporter.Tests;

public sealed class BrowserPdfExportTests : IDisposable
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly byte[] StartOfPdf = "%PDF-1.7\n1 0 obj\n<< >>\n"u8.ToArray();
    private static readonly byte[] EndOfPdf = "endobj\ntrailer\n<< >>\n%%EOF\n"u8.ToArray();

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("guide-exporter-tests-");

    private string PdfPath => Path.Combine(directory.FullName, "export.pdf");

    private static byte[] WholePdf => [.. StartOfPdf, .. EndOfPdf];

    public void Dispose() => directory.Delete(recursive: true);

    [Fact]
    public async Task WaitForCompletePdf_WaitsForAFileThatAppearsAfterTheBrowserExits()
    {
        using var cancellation = new CancellationTokenSource(Patience);
        var wait = BrowserPdfExport.WaitForCompletePdfAsync(PdfPath, Poll, cancellation.Token);

        await SeveralPolls();
        Assert.False(wait.IsCompleted);

        await File.WriteAllBytesAsync(PdfPath, WholePdf);
        Assert.True(await wait);
    }

    [Fact]
    public async Task WaitForCompletePdf_DoesNotAcceptAPartlyWrittenFile()
    {
        using var cancellation = new CancellationTokenSource(Patience);
        await File.WriteAllBytesAsync(PdfPath, StartOfPdf);
        var wait = BrowserPdfExport.WaitForCompletePdfAsync(PdfPath, Poll, cancellation.Token);

        await SeveralPolls();
        Assert.False(wait.IsCompleted);

        await File.WriteAllBytesAsync(PdfPath, WholePdf);
        Assert.True(await wait);
    }

    [Fact]
    public async Task WaitForCompletePdf_WaitsForTheWriterToCloseAWholeFile()
    {
        using var cancellation = new CancellationTokenSource(Patience);
        var writer = new FileStream(PdfPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await writer.WriteAsync(WholePdf);
        await writer.FlushAsync();
        var wait = BrowserPdfExport.WaitForCompletePdfAsync(PdfPath, Poll, cancellation.Token);

        await SeveralPolls();
        Assert.False(wait.IsCompleted);

        await writer.DisposeAsync();
        Assert.True(await wait);
    }

    [Fact]
    public async Task WaitForCompletePdf_ReturnsFalseWhenAFileStopsGrowingWithoutItsTrailer()
    {
        await File.WriteAllBytesAsync(PdfPath, StartOfPdf);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.False(await BrowserPdfExport.WaitForCompletePdfAsync(PdfPath, Poll, cancellation.Token));
    }

    [Fact]
    public async Task WaitForCompletePdf_ReturnsFalseForAFileThatIsNotAPdf()
    {
        await File.WriteAllTextAsync(PdfPath, "<html>not a pdf</html>\n%%EOF\n");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.False(await BrowserPdfExport.WaitForCompletePdfAsync(PdfPath, Poll, cancellation.Token));
    }

    [Fact]
    public async Task WaitForCompletePdf_ReturnsFalseWhenNoFileAppears()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.False(await BrowserPdfExport.WaitForCompletePdfAsync(PdfPath, Poll, cancellation.Token));
    }

    [Fact]
    public async Task Run_SucceedsWhenTheLauncherExitsBeforeTheBrowserWritesThePdf()
    {
        var run = BrowserPdfExport.RunAsync(BrowserThatExits(0), PdfPath, Patience);

        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(run.IsCompleted);

        await File.WriteAllBytesAsync(PdfPath, WholePdf);
        Assert.Equal(0, await run);
    }

    [Fact]
    public async Task Run_ReportsANonZeroExitCodeEvenWhenAPdfExists()
    {
        await File.WriteAllBytesAsync(PdfPath, WholePdf);

        Assert.Equal(4, await BrowserPdfExport.RunAsync(BrowserThatExits(7), PdfPath, Patience));
    }

    [Fact]
    public async Task Run_ReportsAnExitCodeOfZeroThatNeverProducesAPdf()
    {
        Assert.Equal(4, await BrowserPdfExport.RunAsync(BrowserThatExits(0), PdfPath, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Run_KillsAHungBrowserAndReportsTheTimeout()
    {
        var elapsed = Stopwatch.StartNew();

        Assert.Equal(5, await BrowserPdfExport.RunAsync(BrowserThatHangs(), PdfPath, TimeSpan.FromMilliseconds(500)));
        Assert.True(elapsed.Elapsed < Patience);
    }

    private static Task SeveralPolls() => Task.Delay(Poll * 10);

    private static ProcessStartInfo BrowserThatExits(int exitCode) =>
        Shell($"exit {exitCode}", $"exit {exitCode}");

    private static ProcessStartInfo BrowserThatHangs() =>
        Shell("ping -n 120 127.0.0.1 >NUL", "sleep 120");

    private static ProcessStartInfo Shell(string windowsCommand, string unixCommand)
    {
        var startInfo = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? windowsCommand : unixCommand);
        return startInfo;
    }
}
