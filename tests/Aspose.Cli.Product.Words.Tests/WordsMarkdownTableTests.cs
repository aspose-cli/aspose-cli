using Aspose.Words;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// A Markdown table's first row is its header, the same through create and insert_markdown: it
/// repeats across pages, its text is bold, and every cell is vertically centered. A template's
/// table style keeps its look, and tables the document already held stay as they were.
/// </summary>
public sealed class WordsMarkdownTableTests
{
    private const string Table = "| Name | Qty |\n|---|---:|\n| A **b** | 1 |\n| C | 2 |\n";

    [Theory]
    [InlineData("create")]
    [InlineData("insert_markdown")]
    public void MarkdownTable_FirstRowIsARepeatingBoldHeaderAndCellsAreCentered(string path)
    {
        using var fixture = new WordsFixture();
        string output = Imported(fixture, path, template: null);

        Table table = Assert.Single(new Document(output).GetChildNodes(NodeType.Table, true).OfType<Table>());
        Row[] rows = [.. table.Rows.Cast<Row>()];
        Assert.True(rows[0].RowFormat.HeadingFormat);
        Assert.All(rows.Skip(1), static row => Assert.False(row.RowFormat.HeadingFormat));
        Assert.All(RunsOf(rows[0]), static run => Assert.True(run.Font.Bold, run.Text));
        // Only the emphasis the Markdown wrote is bold in the data rows.
        Assert.Equal(["b"], rows.Skip(1).SelectMany(RunsOf).Where(static run => run.Font.Bold).Select(static run => run.Text));
        Assert.All(table.GetChildNodes(NodeType.Cell, true).OfType<Cell>(),
            static cell => Assert.Equal(CellVerticalAlignment.Center, cell.CellFormat.VerticalAlignment));

        WordsTableData reported = Assert.Single(WordsInspect.Run(fixture.Session, new WordsInspectRequest { Input = output, Details = ["tables"] }).Tables!);
        Assert.Equal(1, reported.HeaderRowCount);
    }

    /// <summary>
    /// The table style the template gives the table aligns its cells: the table keeps that style,
    /// the style stays as it is, its cells are not centered over it, and the first row still is
    /// the header.
    /// </summary>
    [Theory]
    [InlineData("create")]
    [InlineData("insert_markdown")]
    public void TemplateTableStyleThatAlignsItsCells_IsNotOverridden(string path)
    {
        using var fixture = new WordsFixture();
        string template = fixture.Temp.File("branded.docx");
        var source = new Document();
        new DocumentBuilder(source).Write("Anchor");
        var style = (TableStyle)source.Styles["Table Normal"]!;
        style.VerticalAlignment = CellVerticalAlignment.Bottom;
        source.Save(template, SaveFormat.Docx);

        string output = Imported(fixture, path, template);

        var document = new Document(output);
        Table table = Assert.Single(document.GetChildNodes(NodeType.Table, true).OfType<Table>());
        Assert.Equal("Table Normal", table.StyleName);
        Assert.Single(document.Styles.Cast<Style>(), static style => style.Type == StyleType.Table);
        var kept = (TableStyle)document.Styles["Table Normal"]!;
        Assert.Equal(CellVerticalAlignment.Bottom, kept.VerticalAlignment);
        Assert.True(table.FirstRow.RowFormat.HeadingFormat);
        Assert.All(table.GetChildNodes(NodeType.Cell, true).OfType<Cell>(),
            static cell => Assert.NotEqual(CellVerticalAlignment.Center, cell.CellFormat.VerticalAlignment));
    }

    /// <summary>A plain template table style does not align its cells, so the header is bold and the cells are centered under it.</summary>
    [Fact]
    public void TemplateWithAPlainTableStyle_StillGetsTheHeaderAndCenteredCells()
    {
        using var fixture = new WordsFixture();
        string template = fixture.Temp.File("plain.docx");
        var source = new Document();
        new DocumentBuilder(source).Write("Anchor");
        source.Save(template, SaveFormat.Docx);

        Table table = Assert.Single(new Document(Imported(fixture, "create", template)).GetChildNodes(NodeType.Table, true).OfType<Table>());

        Assert.All(RunsOf(table.FirstRow), static run => Assert.True(run.Font.Bold, run.Text));
        Assert.All(table.GetChildNodes(NodeType.Cell, true).OfType<Cell>(),
            static cell => Assert.Equal(CellVerticalAlignment.Center, cell.CellFormat.VerticalAlignment));
    }

    /// <summary>A table the document already held keeps its own rows, text and alignment when Markdown is inserted beside it.</summary>
    [Fact]
    public void InsertMarkdown_LeavesATableTheDocumentAlreadyHeldAlone()
    {
        using var fixture = new WordsFixture();
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Writeln("Anchor");
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Held");
        builder.EndRow();
        builder.EndTable();
        string input = fixture.Temp.File("held.docx");
        source.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("held-out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new InsertMarkdownOp { At = new WordsTarget { Find = "Anchor" }, Position = "after", Markdown = Table }] },
            Output = TestOutput.At(output),
        });

        Table held = Assert.Single(new Document(output).GetChildNodes(NodeType.Table, true).OfType<Table>(), static table => table.GetText().Contains("Held", StringComparison.Ordinal));
        Assert.False(held.FirstRow.RowFormat.HeadingFormat);
        Assert.All(RunsOf(held.FirstRow), static run => Assert.False(run.Font.Bold, run.Text));
        Assert.Equal(CellVerticalAlignment.Top, held.FirstRow.FirstCell.CellFormat.VerticalAlignment);
    }

    /// <summary>Markdown without a table imports no header formatting: no row, no bold run beyond what it wrote.</summary>
    [Fact]
    public void MarkdownWithoutATable_ImportsNoHeaderFormatting()
    {
        using var fixture = new WordsFixture();
        string markdown = fixture.Temp.File("plain.md");
        File.WriteAllText(markdown, "# Brief\n\nPlain **bold** text.\n");
        string output = fixture.Temp.File("plain.docx");

        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) });

        var document = new Document(output);
        Assert.Empty(document.GetChildNodes(NodeType.Table, true));
        // Evaluation mode may add a banner paragraph; select the authored paragraph by text.
        Paragraph plain = Assert.Single(document.FirstSection.Body.Paragraphs.Cast<Paragraph>(), static paragraph => paragraph.GetText().StartsWith("Plain", StringComparison.Ordinal));
        Assert.Equal(["bold"], plain.Runs.Cast<Run>().Where(static run => run.Font.Bold).Select(static run => run.Text));
    }

    private static string Imported(WordsFixture fixture, string path, string? template)
    {
        string markdown = fixture.Temp.File("table.md");
        File.WriteAllText(markdown, "Before\n\n" + Table + "\nAfter\n");
        string created = fixture.Temp.File("created.docx");
        if (path == "create")
        {
            WordsCreate.Run(fixture.Session, new WordsCreateRequest
            {
                Output = TestOutput.At(created),
                Markdown = new SourceFile("--from-markdown", markdown),
                Template = template is null ? null : new SourceFile("--template", template),
            });
            return created;
        }

        if (template is not null)
        {
            File.Copy(template, created);
        }
        else
        {
            WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(created) });
        }

        string output = fixture.Temp.File("inserted.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch { Ops = [new InsertMarkdownOp { At = new WordsTarget { Block = 1 }, Position = "after", Markdown = "Before\n\n" + Table }] },
            Output = TestOutput.At(output),
        });
        return output;
    }

    private static IEnumerable<Run> RunsOf(Row row) => row.GetChildNodes(NodeType.Run, true).Cast<Run>();
}
