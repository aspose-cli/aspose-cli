using System.Drawing;
using Aspose.Words;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The built-in design's body color is a document default, not a color of its Normal style: a
/// paragraph style outranks a table style, so a color on Normal would hide the white header text
/// of a table style such as Grid Table 4, while everything else reads the same color.
/// </summary>
public sealed class WordsBuiltInDesignColorTests
{
    [Fact]
    public void GridTable4_HeaderTextIsWhiteAndDataTextKeepsTheBodyColor()
    {
        using var fixture = new WordsFixture();
        string created = fixture.Temp.File("created.docx");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(created) });
        string output = fixture.Temp.File("table.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertTableOp
                    {
                        At = new WordsTarget { Block = 1 }, Position = "after", RowCount = 2, ColumnCount = 2,
                        Values = [["H1", "H2"], ["a", "b"]], TableStyle = "Grid Table 4",
                    },
                ],
            },
            Output = TestOutput.At(output),
        });

        Table table = Assert.Single(new Document(output).GetChildNodes(NodeType.Table, true).OfType<Table>());
        Assert.Equal("Grid Table 4", table.StyleName);
        Assert.Equal(Color.White.ToArgb(), table.FirstRow.FirstCell.FirstParagraph.Runs[0].Font.Color.ToArgb());
        Assert.Equal(Body, table.LastRow.FirstCell.FirstParagraph.Runs[0].Font.Color.ToArgb() & 0xFFFFFF);
    }

    private const int Body = 0x1F2937;

    /// <summary>The body, a plain table's cells, the headings and the Normal style a read reports keep the colors of the design.</summary>
    [Fact]
    public void BodyHeadingsAndNormalStyle_KeepTheColorsOfTheDesign()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("colors.md");
        File.WriteAllText(markdown, "# One\n\n## Two\n\n### Three\n\n#### Four\n\n##### Five\n\n###### Six\n\nBody text.\n\n> Quoted.\n\n| A | B |\n|---|---|\n| c | d |\n");
        string output = fixture.Temp.File("colors.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        var document = new Document(output);
        Dictionary<string, Paragraph> paragraphs = document.FirstSection.Body.Paragraphs.Cast<Paragraph>()
            .Where(static paragraph => paragraph.Runs.Count > 0)
            .ToDictionary(static paragraph => paragraph.GetText().Trim(), static paragraph => paragraph);
        foreach ((string text, int color) in new[]
                 {
                     ("One", 0x0F2A3D), ("Two", 0x0F766E), ("Three", Body), ("Four", 0x475569), ("Five", 0x475569), ("Six", 0x475569),
                     ("Body text.", Body), ("Quoted.", 0x475569),
                 })
        {
            Assert.Equal((text, color), (text, paragraphs[text].Runs[0].Font.Color.ToArgb() & 0xFFFFFF));
        }

        Table table = Assert.Single(document.GetChildNodes(NodeType.Table, true).OfType<Table>());
        Assert.Equal(Body, table.LastRow.FirstCell.FirstParagraph.Runs[0].Font.Color.ToArgb() & 0xFFFFFF);
        WordsInspectResult info = WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = output, Details = ["styles"] });
        Assert.Equal("#1F2937", Assert.Single(info.Styles!, static style => style.Name == "Normal").Color);
    }
}
