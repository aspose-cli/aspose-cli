using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Commands;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility.Output;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsRenderersTests
{
    [Theory]
    [InlineData(TableFormat.Plain, "styles:", "bookmarks:")]
    [InlineData(TableFormat.Markdown, "### styles", "### bookmarks")]
    public void Info_ShowsEveryRequestedDetailUnderItsHeadingAndNoneWhenEmpty(
        TableFormat format,
        string styles,
        string bookmarks)
    {
        var result = new DocumentInfoResult
        {
            Source = new SourceInfo { Path = "report.docx", Format = "docx", SizeBytes = 10 },
            Document = new DocumentSummary
            {
                SectionCount = 1, BlockCount = 1, ParagraphCount = 1, TableCount = 0, PageCount = 1, WordCount = 2,
                RevisionsPresent = false, RevisionCount = 0, RevisionAuthors = [], CommentCount = 0,
                Protection = "none", Signed = false, HasMacros = false,
            },
            Styles = ["Normal", "Heading 1"],
            Bookmarks = [],
        };
        using var writer = new StringWriter();

        InspectCommand.Table(result, new TableSurface(writer, format));

        string text = writer.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        string gap = format == TableFormat.Markdown ? "\n\n" : "\n";
        Assert.Contains($"\n{styles}{gap}Normal, Heading 1\n", text, StringComparison.Ordinal);
        Assert.EndsWith($"\n{bookmarks}{gap}none\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts", text, StringComparison.Ordinal);
    }
}
