using Aspose.Words;
using Aspose.Words.Saving;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsBookmarkTextTests
{
    [Fact]
    public void SetTextAtAMidParagraphBookmark_ReplacesOnlyTheBookmarkedText()
    {
        using var fixture = new WordsFixture();
        string input = CreateDocument(fixture);
        string output = fixture.Temp.File("mid.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest { Input = input, Batch = Batch("Mid", "Acme Ltd"), Output = TestOutput.At(output) });

        var document = new Document(output);
        Paragraph paragraph = Paragraphs(document).Single(item => item.GetText().Contains("Party:", StringComparison.Ordinal));
        Assert.Equal("Party: Acme Ltd, signed.", paragraph.GetText().Trim());
        Bookmark bookmark = document.Range.Bookmarks["Mid"];
        Assert.NotNull(bookmark);
        Assert.Equal("Acme Ltd", bookmark.Text);
        Run replaced = paragraph.Runs.Cast<Run>().Single(run => run.Text == "Acme Ltd");
        Assert.True(replaced.Font.Bold);
    }

    [Fact]
    public void SetTextAtATableCellBookmark_ReplacesTheCellTextOnly()
    {
        using var fixture = new WordsFixture();
        string input = CreateDocument(fixture);
        string output = fixture.Temp.File("cell.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest { Input = input, Batch = Batch("Amount", "250"), Output = TestOutput.At(output) });

        var document = new Document(output);
        Table table = (Table)document.GetChild(NodeType.Table, 0, true);
        Assert.Equal("Total: 250", table.Rows[0].Cells[1].GetText().Trim('\a', ' ', '\r'));
        Assert.Equal("Label", table.Rows[0].Cells[0].GetText().Trim('\a', ' ', '\r'));
        Assert.Equal("250", document.Range.Bookmarks["Amount"].Text);
    }

    private static WordsOpsBatch Batch(string bookmark, string text) => new()
    {
        Ops = [new SetTextOp { At = new WordsTarget { Bookmark = bookmark }, Text = text }],
    };

    private static IEnumerable<Paragraph> Paragraphs(Document document) =>
        document.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>();

    private static string CreateDocument(WordsFixture fixture)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Party: ");
        builder.StartBookmark("Mid");
        builder.Font.Bold = true;
        builder.Write("Placeholder Inc");
        builder.Font.Bold = false;
        builder.EndBookmark("Mid");
        builder.Writeln(", signed.");
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Label");
        builder.InsertCell();
        builder.Write("Total: ");
        builder.StartBookmark("Amount");
        builder.Write("100");
        builder.EndBookmark("Amount");
        builder.EndRow();
        builder.EndTable();
        builder.Writeln("After the table.");
        string path = fixture.Temp.File("bookmarks.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }
}
