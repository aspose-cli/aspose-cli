using System.Drawing;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
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
        string originalRunText = string.Concat(originalCell.GetChildNodes(NodeType.Run, true).Cast<Run>().Select(static run => run.Text));
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
            Assert.Equal(originalRunText, string.Concat(
                FirstTable(changed).FirstRow.FirstCell.GetChildNodes(NodeType.Run, true)
                    .Cast<Run>().Where(static run => run.IsDeleteRevision).Select(static run => run.Text)));
            Assert.Equal("30 Oct 2026", string.Concat(
                FirstTable(changed).FirstRow.FirstCell.GetChildNodes(NodeType.Run, true)
                    .Cast<Run>().Where(static run => run.IsInsertRevision && !run.IsDeleteRevision).Select(static run => run.Text)));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatTableRow_CopiesTheTemplateRowPerItemKeepingItsFormatting(bool trackChanges)
    {
        string input = CreateTemplate($"repeat-{trackChanges}.docx");
        string output = _fixture.Temp.File($"repeat-{trackChanges}-changed.docx");
        Table original = FirstTable(new Document(input));
        double[] widths = CellWidths(original.Rows[1]);

        WordsEditResult result = _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new RepeatTableRowOp
                {
                    At = new WordsTarget { Find = "{{code}}" },
                    Items =
                    [
                        new Dictionary<string, string> { ["code"] = "A-100", ["name"] = "Widget", ["price"] = "12.50" },
                        new Dictionary<string, string> { ["code"] = "B-200", ["name"] = "Gadget", ["price"] = "7", ["unused"] = "x" },
                        new Dictionary<string, string> { ["code"] = "C-300", ["name"] = "Gizmo", ["price"] = "" },
                    ],
                },
            ],
        }, new WordsEditRequest
        {
            OutputPath = output,
            TrackChanges = trackChanges,
            Author = trackChanges ? "Contract desk" : null,
        });

        Assert.Equal(3, Assert.Single(result.Applied).ItemsAffected);
        var changed = new Document(output);
        if (trackChanges)
        {
            Assert.All(changed.Revisions.Cast<Revision>(), revision => Assert.Equal("Contract desk", revision.Author));
            // Each copy is one tracked row insertion: nothing inside it is a deletion.
            Assert.All(FirstTable(changed).Rows.Cast<Row>().Skip(1).Take(3), static row => Assert.DoesNotContain(
                row.GetChildNodes(NodeType.Run, true).Cast<Run>(), static run => run.IsDeleteRevision));
            Document rejected = changed.Clone();
            rejected.Revisions.RejectAll();
            Assert.Equal(original.GetText(), FirstTable(rejected).GetText());
            changed.AcceptAllRevisions();
        }
        else
        {
            Assert.Empty(changed.Revisions);
        }

        Table table = FirstTable(changed);
        Assert.Equal(
            [["Code", "Name", "Price"], ["A-100", "Widget", "12.50 USD"], ["B-200", "Gadget", "7 USD"], ["C-300", "Gizmo", " USD"], ["Total", "", "19.50"]],
            CellTexts(table));
        foreach (Row row in table.Rows.Cast<Row>().Skip(1).Take(3))
        {
            Assert.Equal(widths, CellWidths(row));
            Assert.Equal(21, row.RowFormat.Height);
            Assert.Equal(HeightRule.Exactly, row.RowFormat.HeightRule);
            Assert.False(row.RowFormat.AllowBreakAcrossPages);
            Assert.Equal(Color.LightYellow.ToArgb(), row.FirstCell.CellFormat.Shading.BackgroundPatternColor.ToArgb());
            Run code = row.Cells[0].FirstParagraph.Runs[0];
            Assert.Equal("Courier New", code.Font.Name);
            Assert.True(code.Font.Bold);
            Run name = row.Cells[1].FirstParagraph.Runs[0];
            Assert.True(name.Font.Italic);
            Assert.Equal(Color.DarkBlue.ToArgb(), name.Font.Color.ToArgb());
        }

        Assert.DoesNotContain("{{", table.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatTableRow_WithRow_ExpandsThatRowAndLeavesTheOthers()
    {
        string input = CreateTemplate("repeat-row.docx", secondTemplateRow: true);
        string output = _fixture.Temp.File("repeat-row-changed.docx");
        var at = new WordsTarget { Find = "{{code}}" };
        CliException ambiguous = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new RepeatTableRowOp { At = at, Items = [] }],
        }, new WordsEditRequest { OutputPath = output }));
        Assert.Equal(ErrorCodes.OpsInvalid, ambiguous.Code);
        Assert.Contains("rows 2, 3", ambiguous.Message, StringComparison.Ordinal);
        Assert.Contains("Pass row", ambiguous.Hint, StringComparison.Ordinal);
        Assert.False(File.Exists(output));

        _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new RepeatTableRowOp
                {
                    At = at,
                    Row = 3,
                    Items =
                    [
                        new Dictionary<string, string> { ["note"] = "First" },
                        new Dictionary<string, string> { ["note"] = "Second" },
                    ],
                },
            ],
        }, new WordsEditRequest { OutputPath = output });

        string[][] cells = CellTexts(FirstTable(new Document(output)));
        Assert.Equal(["{{code}}", "{{ name }}", "{{price}} USD"], cells[1]);
        Assert.Equal(["Note: First"], cells[2]);
        Assert.Equal(["Note: Second"], cells[3]);
        Assert.Equal(["Total", "", "19.50"], cells[4]);
    }

    [Fact]
    public void RepeatTableRow_WithoutAValueForAPlaceholder_ChangesNothing()
    {
        string input = CreateTemplate("repeat-missing.docx");
        string output = _fixture.Temp.File("repeat-missing-changed.docx");

        CliException error = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new RepeatTableRowOp
                {
                    At = new WordsTarget { Find = "{{code}}" },
                    Items =
                    [
                        new Dictionary<string, string> { ["code"] = "A-100", ["name"] = "Widget", ["price"] = "1" },
                        new Dictionary<string, string> { ["code"] = "B-200" },
                    ],
                },
            ],
        }, new WordsEditRequest { OutputPath = output }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("item 2 has no value for {{name}}, {{price}}", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void RepeatTableRow_WithNoItems_RemovesTheTemplateRow()
    {
        string input = CreateTemplate("repeat-none.docx");
        string output = _fixture.Temp.File("repeat-none-changed.docx");

        WordsEditResult result = _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new RepeatTableRowOp { At = new WordsTarget { Find = "{{code}}" }, Items = [] }],
        }, new WordsEditRequest { OutputPath = output });

        Assert.Equal(0, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal([["Code", "Name", "Price"], ["Total", "", "19.50"]], CellTexts(FirstTable(new Document(output))));
    }

    [Fact]
    public void RepeatTableRow_ReadsItemsFromACsvFile()
    {
        string input = CreateTemplate("repeat-csv.docx");
        string output = _fixture.Temp.File("repeat-csv-changed.docx");
        string items = _fixture.Temp.File("repeat-items.csv");
        File.WriteAllText(items, "code,name,price\nA-100,\"Widget, large\",12.50\nB-200,Gadget,7\n");

        WordsEditResult result = _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new RepeatTableRowOp { At = new WordsTarget { Find = "{{code}}" }, Path = items }],
        }, new WordsEditRequest { OutputPath = output });

        Assert.Equal(2, Assert.Single(result.Applied).ItemsAffected);
        Assert.Equal(
            [["Code", "Name", "Price"], ["A-100", "Widget, large", "12.50 USD"], ["B-200", "Gadget", "7 USD"], ["Total", "", "19.50"]],
            CellTexts(FirstTable(new Document(output))));
    }

    [Fact]
    public void RepeatTableRow_InsertsValuesLiterally()
    {
        string input = CreateTemplate("repeat-literal.docx");
        string output = _fixture.Temp.File("repeat-literal-changed.docx");
        const string code = @"$1 $$ ${name} \d+ (.*) [a-z]";
        const string name = "R&D &p &l && {{price}}";

        _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops =
            [
                new RepeatTableRowOp
                {
                    At = new WordsTarget { Find = "{{code}}" },
                    Items = [new Dictionary<string, string> { ["code"] = code, ["name"] = name, ["price"] = "{{code}}" }],
                },
            ],
        }, new WordsEditRequest { OutputPath = output });

        Assert.Equal([code, name, "{{code}} USD"], CellTexts(FirstTable(new Document(output)))[1]);
    }

    /// <summary>
    /// A product table: a header, a template row whose placeholders carry their own formatting
    /// (the code placeholder split across two runs, as Word often stores it), and a total row.
    /// </summary>
    private string CreateTemplate(string fileName, bool secondTemplateRow = false)
    {
        string path = _fixture.Temp.File(fileName);
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Products");
        builder.StartTable();
        Cell(builder, 60, "Code");
        Cell(builder, 200, "Name");
        Cell(builder, 90, "Price");
        builder.EndRow();

        builder.RowFormat.Height = 21;
        builder.RowFormat.HeightRule = HeightRule.Exactly;
        builder.RowFormat.AllowBreakAcrossPages = false;
        builder.CellFormat.Shading.BackgroundPatternColor = Color.LightYellow;
        Cell(builder, 60, string.Empty);
        builder.Font.Name = "Courier New";
        builder.Font.Bold = true;
        builder.Write("{{");
        builder.Write("code}}");
        builder.Font.ClearFormatting();
        Cell(builder, 200, string.Empty);
        builder.Font.Italic = true;
        builder.Font.Color = Color.DarkBlue;
        builder.Write("{{ name }}");
        builder.Font.ClearFormatting();
        Cell(builder, 90, "{{price}} USD");
        builder.EndRow();
        builder.RowFormat.ClearFormatting();
        builder.CellFormat.ClearFormatting();

        if (secondTemplateRow)
        {
            Cell(builder, 350, "Note: {{note}}");
            builder.EndRow();
        }

        Cell(builder, 60, "Total");
        Cell(builder, 200, string.Empty);
        Cell(builder, 90, "19.50");
        builder.EndRow();
        builder.EndTable();
        builder.Writeln("End of schedule");
        document.Save(path);
        return path;

        static void Cell(DocumentBuilder builder, double width, string text)
        {
            builder.InsertCell();
            builder.CellFormat.Width = width;
            builder.CellFormat.PreferredWidth = PreferredWidth.FromPoints(width);
            builder.Write(text);
        }
    }

    private static string[][] CellTexts(Table table) =>
        table.Rows.Cast<Row>()
            .Select(static row => row.Cells.Cast<Cell>().Select(static cell => cell.GetText().TrimEnd('\a', '\r')).ToArray())
            .ToArray();

    private static double[] CellWidths(Row row) =>
        row.Cells.Cast<Cell>().Select(static cell => cell.CellFormat.PreferredWidth.Value).ToArray();

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
