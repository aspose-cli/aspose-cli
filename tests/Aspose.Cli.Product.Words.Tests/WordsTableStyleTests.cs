using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Tables;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What <c>set_table_style</c> leaves of a table, read with the SDK from the edited file where a
/// read of the CLI reports nothing: the settings the table made itself stay, while the borders,
/// cell margins and cell spacing set on the whole table give way to the style, so a table
/// restyled is the table built with that style. Tables are found by a marker text in a cell.
/// </summary>
public sealed class WordsTableStyleTests : IClassFixture<WordsFixture>, IDisposable
{
    private readonly TempWorkspace _workspace = new();

    /// <summary>
    /// The fixture applies the Words license in a licensed run before a test builds its input with
    /// the SDK, and the workspace holds the license the CLI then runs under.
    /// </summary>
    public WordsTableStyleTests(WordsFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        Aspose.Cli.TestKit.Scenarios.ScenarioLicense.Project(_workspace.Path);
    }

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// Alignment, indent, both kinds of floating position, title and description, right-to-left
    /// layout, style options, heading rows, row breaks and a cell's vertical alignment are as they
    /// were after the style changes.
    /// </summary>
    [Fact]
    public void RestyledTables_KeepTheSettingsTheyMadeThemselves()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        Table Add(string marker, Action<Table> set)
        {
            builder.Writeln("The next table follows.");
            Table table = Build(builder, marker);
            set(table);
            return table;
        }

        Add("aligned", static table =>
        {
            table.Alignment = TableAlignment.Center;
            table.Bidi = true;
            table.Title = "Revenue";
            table.Description = "Revenue by line";
            table.StyleOptions = TableStyleOptions.LastRow | TableStyleOptions.ColumnBands;
            table.Rows[0].RowFormat.HeadingFormat = true;
            table.Rows[1].RowFormat.AllowBreakAcrossPages = false;
            table.Rows[1].Cells[0].CellFormat.VerticalAlignment = CellVerticalAlignment.Center;
        });
        Add("indented", static table => table.LeftIndent = 36);
        Add("absolute", static table =>
        {
            table.TextWrapping = TextWrapping.Around;
            table.HorizontalAnchor = RelativeHorizontalPosition.Page;
            table.VerticalAnchor = RelativeVerticalPosition.Page;
            table.AbsoluteHorizontalDistance = 72;
            table.AbsoluteVerticalDistance = 90;
            table.DistanceLeft = 10;
            table.DistanceRight = 11;
            table.DistanceTop = 12;
            table.DistanceBottom = 13;
        });
        Add("relative", static table =>
        {
            table.TextWrapping = TextWrapping.Around;
            table.RelativeHorizontalAlignment = HorizontalAlignment.Right;
            table.RelativeVerticalAlignment = VerticalAlignment.Bottom;
        });
        builder.Writeln("After the tables.");
        document.Save(_workspace.File("tables.docx"));
        string[] markers = ["aligned", "indented", "absolute", "relative"];
        Dictionary<string, string[]> before = markers.ToDictionary(static marker => marker, marker => Kept(Find(_workspace.File("tables.docx"), marker)));

        Restyle("tables.docx", "restyled.docx", "Grid Table 4", markers);

        foreach (string marker in markers)
        {
            Table table = Find(_workspace.File("restyled.docx"), marker);
            Assert.Equal("Grid Table 4", table.StyleName);
            Assert.Equal(before[marker], Kept(table));
        }
    }

    /// <summary>
    /// A row hidden as Word hides one, with its text, and a row hidden with its text shown stay
    /// hidden through a verified restyle, and every run and paragraph mark keeps whether its text
    /// is hidden, hidden text in a shown row too. No read of the CLI reports a hidden row, and only
    /// the SDK makes one, so this is no scenario.
    /// </summary>
    [Fact]
    public void RestyledTables_KeepTheirHiddenRowsAndTheTextTheyHide()
    {
        var builder = new DocumentBuilder(new Document());
        builder.Writeln("The next table follows.");
        Build(builder, "hidden");
        builder.Writeln("After the table.");
        builder.Document.Save(_workspace.File("built.docx"));
        var document = new Document(_workspace.File("built.docx"));
        Table table = (Table)document.GetChild(NodeType.Table, 0, true);
        table.Rows[1].Hidden = true;
        table.Rows[2].Hidden = true;
        foreach (Aspose.Words.Font font in Fonts(table.Rows[2]))
        {
            font.Hidden = false;
        }

        table.Rows[0].Cells[1].FirstParagraph.Runs[0].Font.Hidden = true;
        document.Save(_workspace.File("tables.docx"));
        string[] before = Hiding(Find(_workspace.File("tables.docx"), "hidden"));
        File.WriteAllText(_workspace.File("restyle.json"), """{"ops":[{"op":"set_table_style","target":{"find":"hidden"},"tableStyle":"Grid Table 4"}]}""");

        CliResult result = _workspace.Run("words", "edit", "tables.docx", "--ops", "restyle.json", "--out", "restyled.docx", "--verify", "--output-format", "json");

        Require(result);
        Assert.True(result.Json()["verification"]!["ok"]!.GetValue<bool>());
        Table restyled = Find(_workspace.File("restyled.docx"), "hidden");
        Assert.Equal("Grid Table 4", restyled.StyleName);
        Assert.Equal(["False:False,False,False,True", "True:True,True,True,True", "True:False,False,False,False"], before);
        Assert.Equal(before, Hiding(restyled));
    }

    /// <summary>
    /// A table insert_table built without a style and then restyled has the borders, cell margins,
    /// cell spacing, alignment and style options of one it built with that style, and a table
    /// from Markdown takes the style's borders too.
    /// </summary>
    [Fact]
    public void RestyledTables_AreTheTableBuiltWithTheStyle()
    {
        // Paragraphs part the tables, which would otherwise join one table.
        File.WriteAllText(_workspace.File("source.md"), "# Tables\n\nFirst.\n\nSecond.\n\nThird.\n\n| markdown | Revenue |\n|---|---|\n| Retail | 120 |\n\nEnd.\n");
        Require(_workspace.Run("words", "create", "tables.docx", "--from-markdown", "source.md", "--output-format", "json"));
        File.WriteAllText(_workspace.File("insert.json"), """
            {"ops":[
              {"op":"insert_table","at":{"find":"First."},"position":"after","rowCount":2,"columnCount":2,"values":[["plain","b"],["c","d"]]},
              {"op":"insert_table","at":{"find":"Second."},"position":"after","rowCount":2,"columnCount":2,"values":[["styled","b"],["c","d"]],"tableStyle":"Grid Table 4"}]}
            """);
        Require(_workspace.Run("words", "edit", "tables.docx", "--ops", "insert.json", "--out", "inserted.docx", "--output-format", "json"));

        Restyle("inserted.docx", "restyled.docx", "Grid Table 4", ["plain", "markdown"]);

        string output = _workspace.File("restyled.docx");
        Dictionary<string, string> styled = Look(Find(output, "styled"));
        Assert.Equal(styled, Look(Find(output, "plain")));
        Table markdown = Find(output, "markdown");
        Assert.Equal(styled["row0.Borders"], Look(markdown)["row0.Borders"]);
        Assert.Equal(styled["Padding"], Look(markdown)["Padding"]);
    }

    /// <summary>
    /// A table without a style, from Markdown or from insert_table without a tableStyle, reads as
    /// Table Normal: setting that style changes nothing and keeps the borders the table carries.
    /// </summary>
    [Fact]
    public void TableNormal_OnATableWithoutAStyle_ChangesNothing()
    {
        File.WriteAllText(_workspace.File("source.md"), "# Tables\n\nFirst.\n\n| markdown | b |\n|---|---|\n| c | d |\n\nSecond.\n");
        Require(_workspace.Run("words", "create", "tables.docx", "--from-markdown", "source.md", "--output-format", "json"));
        File.WriteAllText(_workspace.File("insert.json"), """{"ops":[{"op":"insert_table","at":{"find":"Second."},"position":"after","rowCount":2,"columnCount":2,"values":[["plain","b"],["c","d"]]}]}""");
        Require(_workspace.Run("words", "edit", "tables.docx", "--ops", "insert.json", "--out", "inserted.docx", "--output-format", "json"));
        File.WriteAllText(_workspace.File("normal.json"), """{"ops":[{"op":"set_table_style","target":{"find":"markdown"},"tableStyle":"Table Normal"},{"op":"set_table_style","target":{"find":"plain"},"tableStyle":"Table Normal"}]}""");

        CliResult result = _workspace.Run("words", "edit", "inserted.docx", "--ops", "normal.json", "--out", "normal.docx", "--output-format", "json");

        Require(result);
        Assert.Equal(2, result.Json()["warnings"]!.AsArray().Count(static warning => warning!["code"]!.GetValue<string>() == "OP_NO_EFFECT"));
        Assert.All(result.Json()["applied"]!.AsArray(), static applied => Assert.Equal(0, applied!["itemsAffected"]!.GetValue<int>()));
        foreach (string marker in new[] { "markdown", "plain" })
        {
            Assert.Equal(Look(Find(_workspace.File("inserted.docx"), marker)), Look(Find(_workspace.File("normal.docx"), marker)));
        }
    }

    /// <summary>
    /// A table style that centers and indents its tables centers a table that set no alignment of
    /// its own, while one the document aligned right itself stays right-aligned.
    /// </summary>
    [Fact]
    public void AStyleThatCentersItsTables_CentersOnlyATableThatSetNoAlignmentItself()
    {
        var document = new Document();
        var style = (TableStyle)document.Styles.Add(StyleType.Table, "Centered Table");
        style.Alignment = TableAlignment.Center;
        style.LeftIndent = 20;
        var builder = new DocumentBuilder(document);
        builder.Writeln("Before the tables.");
        Build(builder, "unaligned");
        builder.Writeln("Between the tables.");
        Build(builder, "right").Alignment = TableAlignment.Right;
        builder.Writeln("After the tables.");
        document.Save(_workspace.File("tables.docx"));

        Restyle("tables.docx", "restyled.docx", "Centered Table", ["unaligned", "right"]);

        Assert.Equal(TableAlignment.Center, Find(_workspace.File("restyled.docx"), "unaligned").Alignment);
        Assert.Equal(TableAlignment.Right, Find(_workspace.File("restyled.docx"), "right").Alignment);
    }

    // Gives each table a cell of whose text holds its marker the style, in one edit.
    private void Restyle(string input, string output, string style, string[] markers)
    {
        string ops = string.Join(",", markers.Select(marker => $$"""{"op":"set_table_style","target":{"find":"{{marker}}"},"tableStyle":"{{style}}"}"""));
        File.WriteAllText(_workspace.File("restyle.json"), $$"""{"ops":[{{ops}}]}""");
        Require(_workspace.Run("words", "edit", input, "--ops", "restyle.json", "--out", output, "--output-format", "json"));
    }

    private static void Require(CliResult result) => Assert.True(result.ExitCode == 0, result.StdErr);

    private static Table Build(DocumentBuilder builder, string marker)
    {
        Table table = builder.StartTable();
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                builder.InsertCell();
                builder.Write(row == 0 && column == 0 ? marker : $"r{row}c{column}");
            }

            builder.EndRow();
        }

        builder.EndTable();
        return table;
    }

    private static Table Find(string path, string marker) =>
        new Document(path).GetChildNodes(NodeType.Table, true).Cast<Table>()
            .Single(table => table.FirstRow.FirstCell.GetText().Contains(marker, StringComparison.Ordinal));

    // The settings set_table_style keeps, as the SDK reads them.
    private static string[] Kept(Table table) =>
    [
        $"{table.Alignment}", $"{table.LeftIndent}", $"{table.TextWrapping}", $"{table.HorizontalAnchor}/{table.VerticalAnchor}",
        $"{table.AbsoluteHorizontalDistance}/{table.AbsoluteVerticalDistance}", $"{table.RelativeHorizontalAlignment}/{table.RelativeVerticalAlignment}",
        $"{table.DistanceLeft}/{table.DistanceRight}/{table.DistanceTop}/{table.DistanceBottom}", $"{table.Bidi}", $"{table.Title}|{table.Description}",
        $"{table.StyleOptions}",
        .. table.Rows.Cast<Row>().Select(static row => $"{row.RowFormat.HeadingFormat}/{row.RowFormat.AllowBreakAcrossPages}"),
        .. table.Rows.Cast<Row>().SelectMany(static row => row.Cells.Cast<Cell>()).Select(static cell => $"{cell.CellFormat.VerticalAlignment}"),
    ];

    // Each row's hidden setting, then whether each of its paragraph marks and runs hides its text.
    private static string[] Hiding(Table table) =>
        [.. table.Rows.Cast<Row>().Select(static row => $"{row.Hidden}:{string.Join(",", Fonts(row).Select(static font => font.Hidden))}")];

    private static IEnumerable<Aspose.Words.Font> Fonts(Row row) =>
        row.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>()
            .SelectMany(static paragraph => paragraph.Runs.Cast<Run>().Select(static run => run.Font).Prepend(paragraph.ParagraphBreakFont));

    // What a table style decides of a table, as the SDK reads it.
    private static Dictionary<string, string> Look(Table table)
    {
        BorderType[] edges = [BorderType.Top, BorderType.Bottom, BorderType.Left, BorderType.Right, BorderType.Horizontal, BorderType.Vertical];
        var look = new Dictionary<string, string>
        {
            ["StyleName"] = table.StyleName,
            ["Alignment"] = $"{table.Alignment}",
            ["StyleOptions"] = $"{table.StyleOptions}",
            ["Padding"] = $"{table.LeftPadding}/{table.RightPadding}/{table.TopPadding}/{table.BottomPadding}",
            ["CellSpacing"] = $"{table.AllowCellSpacing}/{table.CellSpacing}",
        };
        for (int index = 0; index < table.Rows.Count; index++)
        {
            RowFormat format = table.Rows[index].RowFormat;
            look[$"row{index}.Borders"] = string.Join(",", edges.Select(edge => $"{format.Borders[edge].LineStyle}:{format.Borders[edge].LineWidth}:{format.Borders[edge].Color.ToArgb()}"));
            look[$"row{index}.CellPadding"] = string.Join(",", table.Rows[index].Cells.Cast<Cell>().Select(static cell => $"{cell.CellFormat.LeftPadding}/{cell.CellFormat.RightPadding}"));
        }

        return look;
    }
}
