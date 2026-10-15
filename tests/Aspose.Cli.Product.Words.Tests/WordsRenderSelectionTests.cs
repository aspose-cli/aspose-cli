using Aspose.Cli.Product.Words.Engine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What <c>words render</c> and <c>split</c> write: every page by default, at most 20 per render,
/// as <c>&lt;stem&gt;.&lt;n&gt;.&lt;ext&gt;</c> numbered by page or part and padded to the digits of
/// the highest number.
/// </summary>
public sealed class WordsRenderSelectionTests : IDisposable
{
    private readonly WordsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private string CreatePages(string fileName, int pages)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        for (int page = 1; page <= pages; page++)
        {
            builder.Write($"Page {page}");
            if (page < pages)
            {
                builder.InsertBreak(BreakType.PageBreak);
            }
        }

        string path = _fixture.Temp.File(fileName);
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    private string[] Names(string directory) =>
        [.. Directory.GetFiles(_fixture.Temp.File(directory)).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    [Fact]
    public void Render_WritesEveryPageByDefaultPaddedToTheHighestNumber()
    {
        string input = CreatePages("twelve.docx", 12);

        WordsRenderResult every = WordsRender.Run(_fixture.Session, new WordsRenderRequest
        {
            Input = input,
            Output = TestOutput.At(_fixture.Temp.File("every/page.png"), format: "png"),
            Dpi = 24,
        });
        WordsRender.Run(_fixture.Session, new WordsRenderRequest
        {
            Input = input,
            Output = TestOutput.At(_fixture.Temp.File("late/page.png"), format: "png"),
            Pages = PageRange.Parse("9-12"),
            Dpi = 24,
        });

        Assert.Equal(Enumerable.Range(1, 12), every.Outputs.Select(static output => output.Page));
        Assert.Equal([.. Enumerable.Range(1, 12).Select(static page => $"page.{page:00}.png")], Names("every"));
        Assert.Equal(["page.09.png", "page.10.png", "page.11.png", "page.12.png"], Names("late"));
    }

    [Fact]
    public void Render_RefusesMoreThanTwentyPagesBeforeWritingAny()
    {
        string input = CreatePages("long.docx", 21);
        Directory.CreateDirectory(_fixture.Temp.File("out"));

        CliException error = Assert.Throws<CliException>(() => WordsRender.Run(_fixture.Session, new WordsRenderRequest
        {
            Input = input,
            Output = TestOutput.At(_fixture.Temp.File("out/page.png"), format: "png"),
            Dpi = 24,
        }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal("--pages", error.Details!["option"]!.GetValue<string>());
        Assert.Equal(20, error.Details["limit"]!.GetValue<int>());
        Assert.Equal(21, error.Details["actual"]!.GetValue<int>());
        Assert.Empty(Directory.GetFiles(_fixture.Temp.File("out")));
    }

    [Fact]
    public void SplitByPages_NumbersEachPartByItsPage()
    {
        string input = CreatePages("report.docx", 11);

        WordsSplit.Run(_fixture.Session, new WordsSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(_fixture.Temp.File("parts")),
            Plan = new SplitPlan(SplitBy.Pages, null, null, PageRange.Parse("9-11")),
        });

        Assert.Equal(["report.09.docx", "report.10.docx", "report.11.docx"], Names("parts"));
    }

    [Fact]
    public void SplitByPages_RunsTheSelectedPagesInPartsNumberedByPosition()
    {
        string input = CreatePages("runs.docx", 6);

        WordsSplitResult result = WordsSplit.Run(_fixture.Session, new WordsSplitRequest
        {
            Input = input,
            Output = new ResolvedDirectory(_fixture.Temp.File("runs")),
            Plan = new SplitPlan(SplitBy.Pages, 2, null, PageRange.Parse("1,3-5")),
        });

        Assert.Equal(["runs.1.docx", "runs.2.docx"], Names("runs"));
        Assert.Equal(["1,3", "4-5"], result.Outputs.Select(static part => part.Pages));
        Assert.Equal([2, 2], result.Outputs.Select(static part => new Document(part.Path).PageCount));
    }
}
