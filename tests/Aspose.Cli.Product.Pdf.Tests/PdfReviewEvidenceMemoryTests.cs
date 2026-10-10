using System.Diagnostics;
using Aspose.Cli.TestKit;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>The memory a review of a long PDF holds while it renders page evidence.</summary>
public sealed class PdfReviewEvidenceMemoryTests : IDisposable
{
    private const int Pages = 40;

    /// <summary>
    /// Each evidence page raster leaves several hundred megabytes of engine garbage; when it
    /// piles up across pages the review of this document peaks near 900 MB, and when it is
    /// released before the next page the peak stays near 410 MB.
    /// </summary>
    private const long PeakLimitBytes = 650L * 1024 * 1024;

    private readonly TempWorkspace _workspace = new();

    /// <summary>Evaluation mode reads only the first 4 pages of a PDF, so the review needs a license.</summary>
    [Category(TestCategory.Slow)]
    [LicensedFact]
    public void Review_OfALongDocumentPeaksNearOnePageRaster()
    {
        _ = TestLicense.Apply(PdfActivation.ApplyLicense);
        string input = _workspace.File("long.pdf");
        using (var document = new Document())
        {
            for (int number = 1; number <= Pages; number++)
            {
                Page page = document.Pages.Add();
                page.Paragraphs.Add(new TextFragment($"Section {number}"));
                page.Paragraphs.Add(new TextFragment(string.Concat(Enumerable.Repeat(
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor. ", 30))));
            }

            document.Save(input);
        }

        ProcessStartInfo start = _workspace.StartInfo();
        foreach (string argument in new[]
        {
            "review", "long.pdf", "--out", "evidence", "--license", TestLicense.Path!, "--output", "json", "--quiet",
        })
        {
            start.ArgumentList.Add(argument);
        }

        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        long peak = 0;
        var deadline = Stopwatch.StartNew();
        while (!process.WaitForExit(100))
        {
            peak = Math.Max(peak, PeakWorkingSet(process));
            if (deadline.Elapsed > TimeSpan.FromMinutes(5))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("The review did not finish within 5 minutes.");
            }
        }

        Assert.True(process.ExitCode == 0, $"exit {process.ExitCode}: {error.Result}{output.Result}");
        Assert.Equal(Pages, Directory.GetFiles(_workspace.File("evidence"), "page-*.png", SearchOption.AllDirectories).Length);
        Assert.True(
            peak < PeakLimitBytes,
            $"The review of {Pages} pages peaked at {peak / (1024 * 1024)} MB, above {PeakLimitBytes / (1024 * 1024)} MB.");
    }

    /// <summary>The peak working set so far, or 0 once the process has exited between two polls.</summary>
    private static long PeakWorkingSet(Process process)
    {
        try
        {
            process.Refresh();
            return process.PeakWorkingSet64;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public void Dispose() => _workspace.Dispose();
}
