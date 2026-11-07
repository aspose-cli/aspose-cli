using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cli.Product.Cells.Engine;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// replace_text changes the text of cells only. When the header, footer, a comment, a chart
/// title, an axis title or a text box of its sheet holds a match, matched as the operation
/// matches a cell (regex, caseSensitive, wholeWord), and a header on the text it prints, it
/// leaves that text as it was and warns <c>TEXT_NOT_REPLACED</c> once for each of these parts,
/// with its kind in <c>details.part</c>, its location and a hint that runs as written; the edit
/// still exits 0 and its verification is what it is without that text. A title linked to a cell
/// follows the cell and is not named. The commands run with the test license when the run has
/// one, and in evaluation mode otherwise.
/// </summary>
public sealed class CellsReplaceTextOutsideCellsTests : IDisposable
{
    private const string Code = "TEXT_NOT_REPLACED";

    private readonly TempWorkspace _workspace = new();

    public CellsReplaceTextOutsideCellsTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// The text is replaced in the cell and left in the header, the footer, the comment and the
    /// chart title, and one warning names each of these parts with the sheet and its location,
    /// without repeating the find text.
    /// </summary>
    [Fact]
    public void ReplaceText_WithMatchesOutsideTheCells_ReplacesTheCellsAndNamesEachPartItLeft()
    {
        Book("book.xlsx", outside: true);

        JsonNode result = Edit("book.xlsx", Replace("Acme"));

        Assert.Equal("Globex report", Cell("out.xlsx", "Data", "A1"));
        string state = Inspect("out.xlsx");
        Assert.Equal(4, Count(state, "Acme report"));
        JsonNode[] warnings = Outside(result);
        Assert.Equal(3, warnings.Length);
        JsonNode page = Assert.Single(warnings, static warning => Text(warning, "location") == "page-setup/Data");
        Assert.Contains("the header (center) and the footer (center) on worksheet 'Data'", Text(page, "message"), StringComparison.Ordinal);
        Assert.Contains("set_page_setup", Text(page, "hint"), StringComparison.Ordinal);
        JsonNode comment = Assert.Single(warnings, static warning => Text(warning, "location") == "comment/Data!B2");
        Assert.Contains("the comment of cell B2 on worksheet 'Data'", Text(comment, "message"), StringComparison.Ordinal);
        Assert.Contains("set_comment", Text(comment, "hint"), StringComparison.Ordinal);
        JsonNode chart = Assert.Single(warnings, static warning => Text(warning, "location") == "chart/Data/1");
        Assert.Contains("the title of chart 1 on worksheet 'Data'", Text(chart, "message"), StringComparison.Ordinal);
        Assert.Contains("set_chart", Text(chart, "hint"), StringComparison.Ordinal);
        Assert.All(warnings, static warning =>
        {
            Assert.DoesNotContain("Acme", Text(warning, "message"), StringComparison.Ordinal);
            Assert.Equal("replace_text", warning["details"]!["op"]!.GetValue<string>());
            Assert.Null(warning["affectsCompleteness"]);
        });
    }

    /// <summary>
    /// A verified edit of the workbook with the text outside its cells exits as the same edit of
    /// the workbook without it does, with the same cells, applied outcome and verification: it
    /// adds only its warnings.
    /// </summary>
    [Fact]
    public void VerifiedReplaceText_WithMatchesOutsideTheCells_VerifiesAsWithoutThem()
    {
        Book("outside.xlsx", outside: true);
        Book("plain.xlsx", outside: false);

        CliResult outside = Cli("cells", "edit", "outside.xlsx", "--ops", Replace("Acme"), "--out", "outside-out.xlsx", "--verify", "--output-format", "json");
        CliResult plain = Cli("cells", "edit", "plain.xlsx", "--ops", Replace("Acme"), "--out", "plain-out.xlsx", "--verify", "--output-format", "json");

        Assert.True(outside.ExitCode == 0 && plain.ExitCode == 0, $"exit {outside.ExitCode}, without the outside text {plain.ExitCode}: {outside.StdErr}{outside.StdOut}");
        JsonNode result = JsonNode.Parse(outside.StdOut)!;
        JsonNode baseline = JsonNode.Parse(plain.StdOut)!;
        Assert.Equal(3, Outside(result).Length);
        Assert.Empty(Outside(baseline));
        foreach (string member in new[] { "applied", "verification" })
        {
            Assert.True(JsonNode.DeepEquals(baseline[member], result[member]),
                $"'{member}' differs from the edit without the outside text:\n{result[member]?.ToJsonString()}\n{baseline[member]?.ToJsonString()}");
        }
    }

    /// <summary>
    /// The text outside the cells is matched by the same rules as the cells: a case, whole-word
    /// or regular-expression find that matches neither leaves the cell as it is and warns of
    /// nothing outside it, and one that matches both replaces the cell and warns of all three
    /// parts.
    /// </summary>
    [Theory]
    [InlineData("ACME", false, true, false, false)]
    [InlineData("ACME", false, false, false, true)]
    [InlineData("Acm", false, false, true, false)]
    [InlineData("Acm", false, false, false, true)]
    [InlineData("A[a-z]{2}e", true, true, true, true)]
    [InlineData("^report", true, false, false, false)]
    public void ReplaceText_MatchesOutsideTheCells_AsItMatchesTheCells(string find, bool regex, bool caseSensitive, bool wholeWord, bool matches)
    {
        Book("book.xlsx", outside: true);

        JsonNode result = Edit("book.xlsx", Replace(find, regex, caseSensitive, wholeWord));

        Assert.Equal(matches ? 3 : 0, Outside(result).Length);
        Assert.Equal(matches, Cell("out.xlsx", "Data", "A1") != "Acme report");
    }

    /// <summary>
    /// A find that matches only cell text replaces it and warns of nothing outside the cells,
    /// though the header, footer, comment and chart title hold other text; one that matches
    /// them too names all three parts.
    /// </summary>
    [Theory]
    [InlineData("Second", "A2", "Globex row", 0)]
    [InlineData("report", "A1", "Acme Globex", 3)]
    public void ReplaceText_WarnsOnlyWhenTheTextOutsideTheCellsMatches(string find, string cell, string replaced, int warnings)
    {
        Book("book.xlsx", outside: true);

        JsonNode result = Edit("book.xlsx", Replace(find));

        Assert.Equal(replaced, Cell("out.xlsx", "Data", cell));
        Assert.Equal(warnings, Outside(result).Length);
    }

    /// <summary>
    /// A range reaches the comments of its cells only: a comment outside it, the header, the
    /// footer and the chart title belong to no range and are not named.
    /// </summary>
    [Fact]
    public void ReplaceText_InARange_NamesOnlyTheCommentsOfItsCells()
    {
        Book("book.xlsx", outside: true);

        JsonNode inRange = Edit("book.xlsx", Replace("Acme", range: "A1:B2"));
        JsonNode outOfRange = Edit("book.xlsx", Replace("Acme", range: "A1:A2"));

        Assert.Equal("comment/Data!B2", Text(Assert.Single(Outside(inRange)), "location"));
        Assert.Empty(Outside(outOfRange));
    }

    /// <summary>
    /// A match in the left section of the header is named with its section, and the hint says
    /// that no operation changes it, where set_page_setup changes the center section.
    /// </summary>
    [Fact]
    public void ReplaceText_MatchingTheLeftHeader_SaysNoOperationChangesIt()
    {
        Book("book.xlsx", outside: true);
        MoveTheHeaderLeft("book.xlsx");

        JsonNode result = Edit("book.xlsx", Replace("Acme"));

        JsonNode page = Assert.Single(Outside(result), static warning => Text(warning, "location") == "page-setup/Data");
        Assert.Contains("the header (left) and the footer (center)", Text(page, "message"), StringComparison.Ordinal);
        Assert.Contains("no operation changes the left and right sections", Text(page, "hint"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Each warning names the kind of part it left in <c>details.part</c>, beside the operation's
    /// <c>id</c> and <c>op</c>, at the location the part had before: the header or footer, with the
    /// match in the left section of the header, a comment and a chart title.
    /// </summary>
    [Fact]
    public void ReplaceText_NamesTheKindOfEachPartItLeft()
    {
        Book("book.xlsx", outside: true);
        MoveTheHeaderLeft("book.xlsx");

        JsonNode result = Edit("book.xlsx", Replace("Acme"));

        JsonNode[] warnings = Outside(result);
        Assert.Equal(
            ["chart/Data/1 chart-title", "comment/Data!B2 comment", "page-setup/Data header-footer"],
            warnings.Select(static warning => $"{Text(warning, "location")} {Text(warning["details"]!, "part")}").Order(StringComparer.Ordinal));
        Assert.All(warnings, static warning =>
        {
            Assert.Equal(["id", "op", "part"], warning["details"]!.AsObject().Select(static member => member.Key));
            Assert.Equal("replace_text", Text(warning["details"]!, "op"));
        });
    }

    /// <summary>
    /// The header is matched on the text it prints: its font, style and size codes are no text,
    /// so a find for them matches nothing there, while one for the printed text does.
    /// </summary>
    [Theory]
    [InlineData("Arial", false)]
    [InlineData("12", false)]
    [InlineData("Bold", false)]
    [InlineData("Quarterly", true)]
    public void ReplaceText_MatchesTheHeaderOnTheTextItPrints(string find, bool named)
    {
        HeaderBook("book.xlsx", "&\"Arial,Bold\"&12Quarterly");

        JsonNode[] warnings = Outside(Edit("book.xlsx", Replace(find)));

        if (!named)
        {
            Assert.Empty(warnings);
            return;
        }

        JsonNode page = Assert.Single(warnings);
        Assert.Equal("page-setup/Data header-footer", $"{Text(page, "location")} {Text(page["details"]!, "part")}");
        Assert.Contains("found a match in the header (center) on worksheet 'Data'", Text(page, "message"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A find that matches no cell says it matched no text in the cells of the sheet, beside the
    /// warning for the header that holds it.
    /// </summary>
    [Fact]
    public void ReplaceText_ThatMatchesOnlyTheHeader_SaysItMatchedNoTextInTheCells()
    {
        HeaderBook("book.xlsx", "Quarterly");

        JsonNode result = Edit("book.xlsx", Replace("Quarterly"));

        JsonNode noEffect = Assert.Single(result["warnings"]!.AsArray(), static warning => Text(warning!, "code") == "OP_NO_EFFECT")!;
        Assert.Equal("Operation op-0001 (replace_text) matched no text in the cells of the sheet, so nothing was replaced.", Text(noEffect, "message"));
        Assert.Single(Outside(result));
    }

    /// <summary>
    /// The first page's own header and the even pages' own footer print only when the sheet uses
    /// them: then a match there is named with its part and section, and the hint does not send
    /// the caller to a read that does not report them; with the default header holding a match
    /// too, the hint gives both. A sheet that does not use them keeps their text unprinted, so a
    /// match there is not named.
    /// </summary>
    [Theory]
    [InlineData("Quarterly", true, "the first-page header (left) and the even-page footer (right)", false)]
    [InlineData("Acme report", true, "the header (center) and the first-page header (left) and the even-page footer (right)", true)]
    [InlineData("Quarterly", false, null, false)]
    public void ReplaceText_NamesTheFirstPageAndEvenPageHeaders_OnlyWhenTheSheetUsesThem(string header, bool used, string? named, bool readable)
    {
        Author("book.xlsx", sheet =>
        {
            PageSetup setup = sheet.PageSetup;
            setup.SetHeader(1, header);
            setup.IsHFDiffFirst = true;
            setup.IsHFDiffOddEven = true;
            setup.SetFirstPageHeader(0, "Acme cover");
            setup.SetEvenFooter(2, "Acme even");
            setup.IsHFDiffFirst = used;
            setup.IsHFDiffOddEven = used;
        });

        JsonNode[] warnings = Outside(Edit("book.xlsx", Replace("Acme")));

        if (named is null)
        {
            Assert.Empty(warnings);
            return;
        }

        JsonNode page = Assert.Single(warnings);
        Assert.Equal("page-setup/Data header-footer", $"{Text(page, "location")} {Text(page["details"]!, "part")}");
        Assert.Contains($"found a match in {named} on worksheet 'Data' and left them as they were", Text(page, "message"), StringComparison.Ordinal);
        string hint = Text(page, "hint");
        Assert.Equal(readable, hint.Contains("cells inspect <file> --detail layout", StringComparison.Ordinal));
        Assert.Contains("No operation reads or changes the header and footer of the first page and of the even pages", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// An axis title that holds a match is named with its chart and axis, at the chart, with a
    /// hint that reads and sets it; the chart title, which holds none, is not named.
    /// </summary>
    [Theory]
    [InlineData("Amount", "the axis title of chart 1 (category)", "it as it was")]
    [InlineData("Acme amount", "the axis titles of chart 1 (category, value)", "them as they were")]
    public void ReplaceText_NamesTheAxisTitlesThatHoldAMatch(string valueAxis, string named, string left)
    {
        Assert.Equal(0, Cli("cells", "create", "book.xlsx", "--sheet-names", "Data").ExitCode);
        object[] ops =
        [
            new { op = "set_values", sheet = "Data", range = "A1", values = new object[][] { ["Region", "Amount"], ["East", 700], ["North", 580] } },
            new
            {
                op = "insert_chart", sheet = "Data", kind = "column", dataRange = "A1:B3", at = "D2:I12", title = "Revenue",
                axisTitles = new { category = "Acme regions", value = valueAxis },
            },
        ];
        CliResult edited = Cli("cells", "edit", "book.xlsx", "--in-place", "--ops", JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);

        JsonNode axis = Assert.Single(Outside(Edit("book.xlsx", Replace("Acme"))));

        Assert.Equal("chart/Data/1 axis-title", $"{Text(axis, "location")} {Text(axis["details"]!, "part")}");
        Assert.Contains($"found a match in {named} on worksheet 'Data' and left {left}", Text(axis, "message"), StringComparison.Ordinal);
        string hint = Text(axis, "hint");
        Assert.Contains("cells inspect <file> --detail charts", hint, StringComparison.Ordinal);
        Assert.Contains("set_chart, its axisTitles", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// A text box whose text holds a match is named by its name, at its sheet, with a hint that
    /// says no operation changes it; one whose text holds none is not named.
    /// </summary>
    [Theory]
    [InlineData("Acme note", true)]
    [InlineData("Quarterly note", false)]
    public void ReplaceText_NamesATextBoxThatHoldsAMatch(string text, bool named)
    {
        Author("book.xlsx", sheet =>
        {
            TextBox textBox = sheet.TextBoxes[sheet.TextBoxes.Add(6, 1, 60, 200)];
            textBox.Text = text;
            textBox.Name = "Note box";
        });

        JsonNode[] warnings = Outside(Edit("book.xlsx", Replace("Acme")));

        if (!named)
        {
            Assert.Empty(warnings);
            return;
        }

        JsonNode box = Assert.Single(warnings);
        Assert.Equal("sheet/Data text-box", $"{Text(box, "location")} {Text(box["details"]!, "part")}");
        Assert.Contains("found a match in the text of drawing 'Note box' on worksheet 'Data' and left it as it was", Text(box, "message"), StringComparison.Ordinal);
        Assert.Contains("No operation reads or changes the text of a text box", Text(box, "hint"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A chart title or an axis title linked to a cell follows the cell, so a replacement in that
    /// cell changes it and it is not named; the same titles written as text are left as they
    /// were and named.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplaceText_NamesAChartOrAxisTitle_OnlyWhenItIsNotLinkedToACell(bool linked)
    {
        Author("book.xlsx", sheet =>
        {
            sheet.Cells["A1"].PutValue("Acme revenue");
            sheet.Cells["D1"].PutValue("Acme regions");
            Chart chart = sheet.Charts[sheet.Charts.Add(ChartType.Column, 4, 4, 18, 10)];
            chart.NSeries.Add("Data!B2:B3", true);
            chart.NSeries.CategoryData = "Data!A2:A3";
            if (linked)
            {
                chart.Title.LinkedSource = "Data!$A$1";
                chart.CategoryAxis.Title.LinkedSource = "Data!$D$1";
            }
            else
            {
                chart.Title.Text = "Acme revenue";
                chart.CategoryAxis.Title.Text = "Acme regions";
            }
        });

        JsonNode[] warnings = Outside(Edit("book.xlsx", Replace("Acme")));

        string[] parts = linked ? [] : ["axis-title", "chart-title"];
        Assert.Equal(parts,
            warnings.Select(static warning => Text(warning["details"]!, "part")).Order(StringComparer.Ordinal));
        if (linked)
        {
            JsonNode chart = JsonNode.Parse(Inspect("out.xlsx"))!["workbook"]!["charts"]![0]!;
            Assert.Equal("Globex revenue", Text(chart, "title"));
            Assert.Equal("Globex regions", Text(chart["axisTitles"]!, "category"));
        }
    }

    /// <summary>
    /// A workbook whose sheet Data holds a small table and the header <paramref name="header"/>,
    /// written by set_page_setup as it is, formatting codes included.
    /// </summary>
    private void HeaderBook(string name, string header)
    {
        Assert.Equal(0, Cli("cells", "create", name, "--sheet-names", "Data").ExitCode);
        object[] ops =
        [
            new { op = "set_values", sheet = "Data", range = "A1", values = new object[][] { ["Region", "Amount"], ["East", 700] } },
            new { op = "set_page_setup", sheet = "Data", headerCenter = header },
        ];
        CliResult edited = Cli("cells", "edit", name, "--in-place", "--ops", JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    /// <summary>
    /// A workbook whose sheet Data holds a small table and what <paramref name="build"/> adds to
    /// it, saved by the engine itself, for content no operation writes; the run's license, when
    /// it has one, is applied first, so the workbook is written as the CLI reads it.
    /// </summary>
    private void Author(string name, Action<Worksheet> build)
    {
        TestLicense.Apply(CellsActivation.ApplyLicense);
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Name = "Data";
        sheet.Cells["A1"].PutValue("Region");
        sheet.Cells["B1"].PutValue("Amount");
        sheet.Cells["A2"].PutValue("East");
        sheet.Cells["B2"].PutValue(700);
        sheet.Cells["A3"].PutValue("North");
        sheet.Cells["B3"].PutValue(580);
        build(sheet);
        workbook.Save(_workspace.File(name), SaveFormat.Xlsx);
    }

    /// <summary>Moves the center header "Acme report" of the first sheet to the header's left section.</summary>
    private void MoveTheHeaderLeft(string name)
    {
        using ZipArchive package = ZipFile.Open(_workspace.File(name), ZipArchiveMode.Update);
        ZipArchiveEntry entry = Assert.Single(package.Entries, static entry => entry.FullName == "xl/worksheets/sheet1.xml");
        string xml;
        using (var reader = new StreamReader(entry.Open()))
        {
            xml = reader.ReadToEnd();
        }

        Assert.Contains("<oddHeader>&amp;CAcme report</oddHeader>", xml, StringComparison.Ordinal);
        entry.Delete();
        using var writer = new StreamWriter(package.CreateEntry("xl/worksheets/sheet1.xml").Open());
        writer.Write(xml.Replace("<oddHeader>&amp;CAcme report</oddHeader>", "<oddHeader>&amp;LAcme report</oddHeader>", StringComparison.Ordinal));
    }

    /// <summary>
    /// A workbook whose sheet Data holds "Acme report" in A1 and "Second row" in A2 and, with
    /// <paramref name="outside"/>, "Acme report" in its center header and footer, in the comment
    /// of B2 and in the title of its one chart; without it, those hold "Quarterly".
    /// </summary>
    private void Book(string name, bool outside)
    {
        Assert.Equal(0, Cli("cells", "create", name, "--sheet-names", "Data").ExitCode);
        string text = outside ? "Acme report" : "Quarterly";
        object[] ops =
        [
            new { op = "set_values", sheet = "Data", range = "A1", values = new object[][] { ["Acme report", "Amount"], ["Second row", 2], ["Third row", 3] } },
            new { op = "add_comment", sheet = "Data", cell = "B2", text },
            new { op = "set_page_setup", sheet = "Data", headerCenter = text, footerCenter = text },
            new { op = "insert_chart", sheet = "Data", kind = "column", dataRange = "A1:B3", at = "D2:I12", title = text },
        ];
        CliResult edited = Cli("cells", "edit", name, "--in-place", "--ops", JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    private static string Replace(string find, bool regex = false, bool caseSensitive = false, bool wholeWord = false, string? range = null)
    {
        var operation = new JsonObject { ["op"] = "replace_text", ["sheet"] = "Data", ["find"] = find, ["replace"] = "Globex" };
        if (regex) { operation["regex"] = true; }
        if (caseSensitive) { operation["caseSensitive"] = true; }
        if (wholeWord) { operation["wholeWord"] = true; }
        if (range is not null) { operation["range"] = range; }
        return new JsonObject { ["ops"] = new JsonArray(operation) }.ToJsonString();
    }

    private JsonNode Edit(string input, string ops)
    {
        CliResult edited = Cli("cells", "edit", input, "--ops", ops, "--out", "out.xlsx", "--overwrite", "--output-format", "json");
        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        return JsonNode.Parse(edited.StdOut)!;
    }

    private string? Cell(string file, string sheet, string cell)
    {
        CliResult read = Cli("cells", "read", file, "--sheet", sheet, "--range", cell, "--output-format", "json");
        Assert.True(read.ExitCode == 0, read.StdErr);
        return JsonNode.Parse(read.StdOut)!["sheet"]!["cells"]![0]![0]!["v"]?.GetValue<string>();
    }

    /// <summary>The comments, charts and layout an inspect reports, as JSON text.</summary>
    private string Inspect(string file)
    {
        CliResult inspected = Cli("cells", "inspect", file, "--detail", "comments", "--detail", "charts", "--detail", "layout", "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        return inspected.StdOut;
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private CliResult Cli(params string[] args) => _workspace.Run(args);

    private static JsonNode[] Outside(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!).Where(static warning => Text(warning, "code") == Code)];

    private static string Text(JsonNode node, string member) => node[member]!.GetValue<string>();
}
