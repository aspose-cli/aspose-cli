using Aspose.Cli.Product.Pdf.Engine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// What <c>pdf render</c>, page-image <c>convert</c> and <c>split</c> write: every page by default,
/// at most 20 per render, as <c>&lt;stem&gt;.&lt;n&gt;.&lt;ext&gt;</c> numbered by page or part and padded
/// to the digits of the highest number, and a single page as the output itself.
/// </summary>
public sealed class PdfRenderSelectionTests
{
    private static string[] Names(IEnumerable<string> paths) =>
        [.. paths.Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    [Fact]
    public void Render_WritesEveryPageByDefaultNumberedByPage()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("two.pdf", pages: 2);

        PdfRenderResult result = PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("out/page.png"), format: "png"),
            Dpi = 36,
        });

        Assert.Equal([1, 2], result.Outputs.Select(static output => output.Page));
        Assert.Equal(["page.1.png", "page.2.png"], Names(result.Outputs.Select(static output => output.Output.Path)));
        Assert.Equal(["page.1.png", "page.2.png"], Names(Directory.GetFiles(fixture.File("out"))));
    }

    [Fact]
    public void Render_OfOnePageWritesTheOutputItself()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("two.pdf", pages: 2);

        PdfRenderResult result = PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("out/page.png"), format: "png"),
            Pages = PageRange.Parse("2"),
            Dpi = 36,
        });

        Assert.Equal(["page.png"], Names(Directory.GetFiles(fixture.File("out"))));
        Assert.Equal(2, Assert.Single(result.Outputs).Page);
    }

    [LicensedFact]
    public void Render_PadsThePageNumbersToTheDigitsOfTheHighest()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("twelve.pdf", pages: 12);

        PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("late/page.png"), format: "png"),
            Pages = PageRange.Parse("9-12"),
            Dpi = 36,
        });
        PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("all/page.png"), format: "png"),
            Pages = PageRange.Parse("all"),
            Dpi = 36,
        });

        Assert.Equal(["page.09.png", "page.10.png", "page.11.png", "page.12.png"], Names(Directory.GetFiles(fixture.File("late"))));
        Assert.Equal([.. Enumerable.Range(1, 12).Select(static page => $"page.{page:00}.png")], Names(Directory.GetFiles(fixture.File("all"))));
    }

    /// <summary>Licensed only: evaluation mode lets the engine build no more than four pages.</summary>
    [LicensedFact]
    public void Render_RefusesMoreThanTwentyPagesBeforeWritingAny()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("long.pdf", pages: 21);
        Directory.CreateDirectory(fixture.File("out"));

        CliException error = Assert.Throws<CliException>(() => PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("out/page.png"), format: "png"),
            Dpi = 36,
        }));

        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.Equal("--pages", error.Details!["option"]!.GetValue<string>());
        Assert.Equal(20, error.Details["limit"]!.GetValue<int>());
        Assert.Equal(21, error.Details["actual"]!.GetValue<int>());
        Assert.Contains("--pages 1-20", error.Hint, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(fixture.File("out")));
    }

    [Fact]
    public void ConvertToImages_NumbersEachPage()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("two.pdf", pages: 2);

        PdfConvert.Run(fixture.Session, new PdfConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("out/page.png"), format: "png"),
        });

        Assert.Equal(["page.1.png", "page.2.png"], Names(Directory.GetFiles(fixture.File("out"))));
    }

    [Fact]
    public void Split_NumbersThePartsAfterTheInput()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("report.pdf", pages: 3);

        PdfSplitResult result = PdfSplit.Run(fixture.Session, new PdfSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("parts")),
            Plan = new Sdk.Addressing.SplitPlan(Sdk.Addressing.SplitBy.Pages, 1, null, null),
        });

        Assert.Equal(["report.1.pdf", "report.2.pdf", "report.3.pdf"], Names(result.Outputs.Select(static part => part.Output.Path)));
    }
}
