using System.Drawing;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Words;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsTableEditingTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsTableEditingTests(WordsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("single", false)]
    [InlineData("single", true)]
    [InlineData("multiple-runs", false)]
    [InlineData("multiple-runs", true)]
    [InlineData("multiple-paragraphs", false)]
    [InlineData("multiple-paragraphs", true)]
    [InlineData("empty", false)]
    [InlineData("empty", true)]
    [InlineData("successive", true)]
    public void SetTableCell_PreservesExistingFormatting(string content, bool trackChanges)
    {
        _fixture.Gate.EnsureApplied();
        string input = _fixture.Temp.File($"cell-{content}-{trackChanges}.docx");
        string output = _fixture.Temp.File($"cell-{content}-{trackChanges}-changed.docx");
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.StartTable();
        Cell cell = builder.InsertCell();
        Paragraph paragraph = cell.FirstParagraph;
        paragraph.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        paragraph.ParagraphFormat.SpaceAfter = 4;
        paragraph.ParagraphBreakFont.Name = "Arial";
        paragraph.ParagraphBreakFont.Size = 9.5;
        paragraph.ParagraphBreakFont.Bold = true;
        paragraph.ParagraphBreakFont.Color = Color.Navy;
        cell.CellFormat.VerticalAlignment = CellVerticalAlignment.Center;
        cell.CellFormat.Shading.BackgroundPatternColor = Color.LightCyan;
        if (content != "empty")
        {
            var first = new Run(document, content == "multiple-runs" ? "23 Oct" : "23 Oct 2026");
            first.Font.Name = "Arial";
            first.Font.Size = 9.5;
            first.Font.Bold = true;
            first.Font.Color = Color.Navy;
            paragraph.AppendChild(first);
            if (content == "multiple-runs")
            {
                var second = new Run(document, " 2026");
                second.Font.Name = "Times New Roman";
                second.Font.Size = 16;
                second.Font.Italic = true;
                paragraph.AppendChild(second);
            }
            else if (content == "multiple-paragraphs")
            {
                var second = new Paragraph(document);
                second.ParagraphFormat.Alignment = ParagraphAlignment.Right;
                second.ParagraphFormat.SpaceAfter = 6;
                var note = new Run(document, "Superseded planning note");
                note.Font.Name = "Calibri";
                note.Font.Size = 11.5;
                note.Font.Italic = true;
                second.AppendChild(note);
                cell.AppendChild(second);
            }
        }

        builder.InsertCell();
        builder.Write("Unchanged owner");
        builder.EndRow();
        builder.EndTable();
        document.Save(input);
        var original = new Document(input);
        Cell originalCell = FirstTable(original).FirstRow.FirstCell;
        string originalText = originalCell.GetText();
        Paragraph retainedParagraph = originalCell.LastParagraph;
        Font expectedFont = retainedParagraph.Runs.Count == 0
            ? retainedParagraph.ParagraphBreakFont
            : retainedParagraph.Runs[0].Font;
        int tableBlock = Assert.Single(_fixture.Engine.GetInfo(input, new DocumentInfoRequest
        {
            Details = ["tables"],
        }).Tables!).Block;

        _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = content == "successive"
                ? [
                    new SetTableCellOp { At = new WordsTarget { Block = tableBlock }, Row = 1, Col = 1, Text = "29 Oct 2026" },
                    new SetTableCellOp { At = new WordsTarget { Block = tableBlock }, Row = 1, Col = 1, Text = "30 Oct 2026" },
                ]
                : [new SetTableCellOp { At = new WordsTarget { Block = tableBlock }, Row = 1, Col = 1, Text = "30 Oct 2026" }],
        }, new WordsEditRequest
        {
            OutputPath = output,
            TrackChanges = trackChanges,
            Author = trackChanges ? "Delivery reviewer" : null,
        });

        var changed = new Document(output);
        if (trackChanges)
        {
            Assert.NotEmpty(changed.Revisions);
            Assert.Equal("30 Oct 2026", string.Concat(
                FirstTable(changed).FirstRow.FirstCell.GetChildNodes(NodeType.Run, true)
                    .Cast<Run>().Where(static run => !run.IsDeleteRevision).Select(static run => run.Text)));
            Assert.All(changed.Revisions.Cast<Revision>(), revision => Assert.Equal("Delivery reviewer", revision.Author));
            Document rejected = changed.Clone();
            rejected.Revisions.RejectAll();
            Assert.Equal(originalText, FirstTable(rejected).FirstRow.FirstCell.GetText());
            AssertFormatting(FirstTable(rejected).FirstRow.FirstCell, originalCell.FirstParagraph);
            changed.AcceptAllRevisions();
        }
        else
        {
            Assert.Empty(changed.Revisions);
        }

        Cell updated = FirstTable(changed).FirstRow.FirstCell;
        Assert.Equal("30 Oct 2026", updated.GetText().Trim('\r', '\a'));
        Assert.Single(updated.Paragraphs.Cast<Paragraph>());
        AssertFormatting(updated, retainedParagraph);
        Run updatedRun = Assert.Single(updated.FirstParagraph.Runs.Cast<Run>());
        Assert.Equal(expectedFont.Name, updatedRun.Font.Name);
        Assert.Equal(expectedFont.Size, updatedRun.Font.Size);
        Assert.Equal(expectedFont.Bold, updatedRun.Font.Bold);
        Assert.Equal(expectedFont.Color.ToArgb(), updatedRun.Font.Color.ToArgb());
        Assert.Equal("Unchanged owner", FirstTable(changed).FirstRow.LastCell.GetText().Trim('\r', '\a'));
    }

    private static Table FirstTable(Document document) =>
        (Table)document.GetChild(NodeType.Table, 0, true);

    private static void AssertFormatting(Cell cell, Paragraph originalParagraph)
    {
        Assert.Equal(originalParagraph.ParagraphFormat.Alignment, cell.FirstParagraph.ParagraphFormat.Alignment);
        Assert.Equal(originalParagraph.ParagraphFormat.SpaceAfter, cell.FirstParagraph.ParagraphFormat.SpaceAfter);
        Assert.Equal(CellVerticalAlignment.Center, cell.CellFormat.VerticalAlignment);
        Assert.Equal(Color.LightCyan.ToArgb(), cell.CellFormat.Shading.BackgroundPatternColor.ToArgb());
    }
}
