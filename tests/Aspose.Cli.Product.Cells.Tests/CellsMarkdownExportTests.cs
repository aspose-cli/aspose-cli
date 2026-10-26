using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// <c>cells convert --to md</c> writes a table another tool can take as it is: every row has the
/// columns of its header, each cell reads as the sheet shows it, and the drawings a Markdown table
/// cannot hold are left out with a warning that counts them.
/// </summary>
public sealed class CellsMarkdownExportTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void MarkdownExport_OfATableWithAChartAndAPicture_WritesDisplayedValuesInEvenRowsWithoutImages()
    {
        File.WriteAllBytes(_workspace.File("logo.png"), ResourceHttpServer.Image);
        Book(withDrawings: true);

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "md", "--out", "book.md", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string markdown = File.ReadAllText(_workspace.File("book.md"));
        string[] rows = TableRows(markdown);
        Assert.True(rows.Length >= 5, markdown);
        Assert.All(rows, row => Assert.True(Columns(row) == Columns(rows[0]), $"Row '{row}' does not have the header's columns:\n{markdown}"));
        Assert.Contains("|700,000|38.6%|", markdown, StringComparison.Ordinal);
        Assert.Contains("|580,000|38.3%|", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("700000", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("0.3857", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("![", markdown, StringComparison.Ordinal);
        Assert.Contains("Check the margin", markdown, StringComparison.Ordinal);

        JsonNode warning = Assert.Single(JsonNode.Parse(converted.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!;
        string message = warning["message"]!.GetValue<string>();
        Assert.Contains("1 chart(s)", message, StringComparison.Ordinal);
        Assert.Contains("1 picture(s)", message, StringComparison.Ordinal);
        Assert.Contains("'Summary'", message, StringComparison.Ordinal);
        Assert.True(warning["affectsCompleteness"]!.GetValue<bool>());
    }

    [Fact]
    public void EvaluationMarkdownExport_OfATableWithAChart_KeepsTheEvaluationHeadingAfterAnEvenTable()
    {
        File.WriteAllBytes(_workspace.File("logo.png"), ResourceHttpServer.Image);
        Book(withDrawings: true);

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "md", "--out", "book.md", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string markdown = File.ReadAllText(_workspace.File("book.md"));
        string[] rows = TableRows(markdown);
        Assert.All(rows, row => Assert.True(Columns(row) == Columns(rows[0]), $"Row '{row}' does not have the header's columns:\n{markdown}"));
        Assert.DoesNotContain("data:image", markdown, StringComparison.Ordinal);
        Assert.StartsWith("# Evaluation Only.", markdown.Split('\n').Select(static line => line.Trim()).Last(static line => line.Length > 0), StringComparison.Ordinal);
        JsonNode[] warnings = [.. JsonNode.Parse(converted.StdOut)!["warnings"]!.AsArray().Select(static warning => warning!)];
        Assert.Contains(warnings, static warning => warning["code"]!.GetValue<string>() == "EVALUATION_MODE");
        Assert.Contains(warnings, static warning => warning["code"]!.GetValue<string>() == "LOSSY_CONVERSION");
    }

    /// <summary>
    /// A group of two rectangles is one drawing: it is left out as a whole and counted once,
    /// beside the chart, and the shapes in it are not counted again.
    /// </summary>
    [Fact]
    public void MarkdownExport_OfASheetWithAGroupOfShapes_LeavesTheGroupOutAsOneOtherDrawing()
    {
        File.WriteAllBytes(_workspace.File("logo.png"), ResourceHttpServer.Image);
        Book(withDrawings: true);
        AddGroupOfTwoRectangles(_workspace.File("book.xlsx"));

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "md", "--out", "book.md", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string markdown = File.ReadAllText(_workspace.File("book.md"));
        string[] rows = TableRows(markdown);
        Assert.All(rows, row => Assert.True(Columns(row) == Columns(rows[0]), $"Row '{row}' does not have the header's columns:\n{markdown}"));
        Assert.Contains("|700,000|38.6%|", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("![", markdown, StringComparison.Ordinal);
        string message = Assert.Single(JsonNode.Parse(converted.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!["message"]!.GetValue<string>();
        Assert.Contains("1 chart(s)", message, StringComparison.Ordinal);
        Assert.Contains("1 picture(s)", message, StringComparison.Ordinal);
        Assert.Contains(" 1 other drawing(s)", message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownExport_OfATableWithoutDrawings_WarnsOfNoLostDrawings()
    {
        Book(withDrawings: false);

        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "md", "--out", "book.md", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        string markdown = File.ReadAllText(_workspace.File("book.md"));
        Assert.Contains("|East|700,000|38.6%|", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(JsonNode.Parse(converted.StdOut)!["warnings"]?.AsArray() ?? [],
            static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION");
    }

    private void Book(bool withDrawings)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheet-names", "Summary").ExitCode);
        var ops = new List<object>
        {
            new
            {
                op = "set_values", sheet = "Summary", range = "A1",
                values = new object[][]
                {
                    ["Dept", "Revenue", "Margin"],
                    ["East", 700000, 0.38571428571428573],
                    ["North", 580000, 0.38275862068965516],
                    ["South", 510000, 0.3627450980392157],
                    ["Total", 1790000, 0.3770949720670391],
                },
            },
            new { op = "format_range", sheet = "Summary", range = "B2:B5", style = new { numberFormat = "#,##0" } },
            new { op = "format_range", sheet = "Summary", range = "C2:C5", style = new { numberFormat = "0.0%" } },
        };
        if (withDrawings)
        {
            ops.Add(new { op = "insert_chart", sheet = "Summary", kind = "column", dataRange = "A1:B5", at = "E2:J12", title = "Revenue" });
            ops.Add(new { op = "insert_image", sheet = "Summary", at = "E14", path = "logo.png" });
            ops.Add(new { op = "add_comment", sheet = "Summary", cell = "C2", text = "Check the margin" });
        }

        CliResult edited = _workspace.Run("cells", "edit", "book.xlsx", "--in-place",
            "--ops", System.Text.Json.JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    /// <summary>Adds a group of two rectangles to the first drawing part of the package, as Excel writes one.</summary>
    private static void AddGroupOfTwoRectangles(string path)
    {
        static string Rectangle(int id, int x) =>
            $"""<xdr:sp macro="" textlink=""><xdr:nvSpPr><xdr:cNvPr id="{id}" name="Box {id}"/><xdr:cNvSpPr/></xdr:nvSpPr><xdr:spPr><a:xfrm><a:off x="{x}" y="0"/><a:ext cx="500000" cy="300000"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr><xdr:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>box{id}</a:t></a:r></a:p></xdr:txBody></xdr:sp>""";
        string group =
            """<xdr:twoCellAnchor><xdr:from><xdr:col>5</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>20</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>"""
            + """<xdr:to><xdr:col>9</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>24</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>"""
            + """<xdr:grpSp><xdr:nvGrpSpPr><xdr:cNvPr id="900" name="Group 1"/><xdr:cNvGrpSpPr/></xdr:nvGrpSpPr>"""
            + """<xdr:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="1200000" cy="300000"/><a:chOff x="0" y="0"/><a:chExt cx="1200000" cy="300000"/></a:xfrm></xdr:grpSpPr>"""
            + Rectangle(901, 0) + Rectangle(902, 700000) + "</xdr:grpSp><xdr:clientData/></xdr:twoCellAnchor>";
        using System.IO.Compression.ZipArchive package = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        System.IO.Compression.ZipArchiveEntry drawing = package.Entries
            .Where(static entry => entry.FullName.StartsWith("xl/drawings/drawing", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal))
            .OrderBy(static entry => entry.FullName, StringComparer.Ordinal).First();
        string xml;
        using (var reader = new StreamReader(drawing.Open()))
        {
            xml = reader.ReadToEnd();
        }

        Assert.Contains("</xdr:wsDr>", xml, StringComparison.Ordinal);
        using var writer = new StreamWriter(drawing.Open());
        writer.BaseStream.SetLength(0);
        writer.Write(xml.Replace("</xdr:wsDr>", group + "</xdr:wsDr>", StringComparison.Ordinal));
    }

    private static string[] TableRows(string markdown) =>
        [.. markdown.Split('\n').Select(static line => line.TrimEnd('\r')).Where(static line => line.StartsWith('|'))];

    private static int Columns(string row) => row.Count(static character => character == '|');
}
