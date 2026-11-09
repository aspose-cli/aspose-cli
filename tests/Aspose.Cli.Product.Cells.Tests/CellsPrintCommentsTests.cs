using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// <c>set_page_setup.printComments</c> chooses whether a printed output holds a sheet's comments,
/// and the <c>LOSSY_CONVERSION</c> of a pdf or xps follows it; <c>cells inspect --detail layout</c>
/// reads it back. The commands run with the test license when the run has one, and in evaluation
/// mode otherwise.
/// </summary>
public sealed class CellsPrintCommentsTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public CellsPrintCommentsTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>With sheet-end on every sheet the output holds the comments, after the cells, and warns of none.</summary>
    [Theory]
    [InlineData("pdf")]
    [InlineData("xps")]
    public void SheetEnd_OnEverySheet_PrintsTheCommentsAndWarnsOfNone(string format)
    {
        Book();
        SetPrintComments("sheet-end", "Summary", "Notes");

        JsonNode converted = Convert(format);

        Assert.Empty(Lossy(converted));
        if (format == "xps")
        {
            string pages = XpsPages();
            Assert.Contains("ZqxCellA", pages, StringComparison.Ordinal);
            Assert.Contains("ZqxNoteA", pages, StringComparison.Ordinal);
            Assert.Contains("ZqxNoteC", pages, StringComparison.Ordinal);
        }
        else if (ScenarioLicense.Licensed)
        {
            // Aspose.PDF evaluation mode reads only the first 4 pages, fewer than the evaluation pdf has.
            string found = PdfWords();
            Assert.Contains("ZqxCellA", found, StringComparison.Ordinal);
            Assert.Contains("ZqxNoteA", found, StringComparison.Ordinal);
            Assert.Contains("ZqxNoteC", found, StringComparison.Ordinal);
        }
    }

    /// <summary>The setting is the sheet's own: a sheet without it still warns of its comments, and only it does.</summary>
    [Fact]
    public void SheetEnd_OnOneSheet_WarnsOnlyOfTheOtherSheet()
    {
        Book();
        SetPrintComments("sheet-end", "Summary");

        JsonNode lossy = Assert.Single(Lossy(Convert("pdf")));

        string message = Text(lossy, "message");
        Assert.Contains("1 comment(s)", message, StringComparison.Ordinal);
        Assert.Contains("'Notes'", message, StringComparison.Ordinal);
        Assert.DoesNotContain("'Summary'", message, StringComparison.Ordinal);
        Assert.Equal("sheet/Notes", Text(lossy, "location"));
    }

    /// <summary>Setting none takes sheet-end back: the comments are out of the pdf again and the warning is back.</summary>
    [Fact]
    public void None_AfterSheetEnd_WarnsAgainAndPrintsNoComment()
    {
        Book();
        SetPrintComments("sheet-end", "Summary", "Notes");
        SetPrintComments("none", "Summary", "Notes");

        JsonNode lossy = Assert.Single(Lossy(Convert("pdf")));

        Assert.Contains("3 comment(s)", Text(lossy, "message"), StringComparison.Ordinal);
        if (ScenarioLicense.Licensed)
        {
            string found = PdfWords();
            Assert.Contains("ZqxCellA", found, StringComparison.Ordinal);
            Assert.DoesNotContain("ZqxNote", found, StringComparison.Ordinal);
        }
    }

    /// <summary>The setting decides what is printed: an output with no pages to print on leaves the comments out as before.</summary>
    [Theory]
    [InlineData("html")]
    [InlineData("mhtml")]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    public void SheetEnd_DoesNotKeepTheCommentsOfAnOutputThatIsNotPrinted(string format)
    {
        Book();
        SetPrintComments("sheet-end", "Summary", "Notes");

        JsonNode[] lossy = Lossy(Convert(format));

        Assert.Contains(lossy, static warning => warning["details"]?["content"]?.ToJsonString() == "[\"comments\"]");
    }

    /// <summary>A read reports the setting of each sheet: none by default, and what the operation last wrote.</summary>
    [Fact]
    public void InspectLayout_ReadsTheSettingBack()
    {
        Book();
        Assert.Equal("none", PrintComments("Summary"));

        SetPrintComments("sheet-end", "Summary");
        Assert.Equal("sheet-end", PrintComments("Summary"));
        Assert.Equal("none", PrintComments("Notes"));

        SetPrintComments("none", "Summary");
        Assert.Equal("none", PrintComments("Summary"));
    }

    /// <summary>A read also reports the setting that only Excel makes, under the name the schema describes.</summary>
    [Fact]
    public void InspectLayout_ReadsTheSettingOnlyExcelMakes()
    {
        Book();
        PatchFirstSheet(static xml => xml.Replace("<pageSetup ", "<pageSetup cellComments=\"asDisplayed\" ", StringComparison.Ordinal));

        Assert.Equal("as-displayed", PrintComments("Summary"));
    }

    /// <summary>The warning of the comments a pdf leaves out names the field that keeps them, as it is written.</summary>
    [Theory]
    [InlineData("pdf")]
    [InlineData("xps")]
    public void Hint_NamesTheFieldThatPrintsTheComments(string format)
    {
        Book();

        string hint = Text(Assert.Single(Lossy(Convert(format))), "hint");

        Assert.Contains("set printComments to sheet-end with set_page_setup", hint, StringComparison.Ordinal);
        Assert.Contains("cells inspect <file> --detail comments", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("no operation does", hint, StringComparison.Ordinal);
    }

    /// <summary>A value outside the two the operation writes is refused with the two named, as-displayed included.</summary>
    [Theory]
    [InlineData("as-displayed")]
    [InlineData("atEnd")]
    public void AValueOutsideTheList_IsRefusedAndNamesTheValues(string value)
    {
        Book();

        CliResult refused = Cli("cells", "edit", "book.xlsx", "--in-place", "--output-format", "json",
            "--ops", Ops(new { op = "set_page_setup", sheet = "Summary", printComments = value }));

        Assert.Equal(4, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", Text(error, "code"));
        Assert.Equal(["none", "sheet-end"], error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
    }

    /// <summary>An edit counts a change of the setting as one page setup, and writing the value it has as nothing.</summary>
    [Fact]
    public void Edit_CountsOnlyARealChangeOfTheSetting()
    {
        Book();

        JsonNode changed = SetPrintComments("sheet-end", "Summary")["applied"]![0]!;
        JsonNode same = SetPrintComments("sheet-end", "Summary")["applied"]![0]!;

        Assert.Equal(1, changed["itemsAffected"]!.GetValue<int>());
        Assert.Equal(["page-setup/Summary"], changed["targets"]!.AsArray().Select(static target => target!.GetValue<string>()));
        Assert.Equal(0, same["itemsAffected"]!.GetValue<int>());
    }

    /// <summary>A verified edit reports the operation among those it did not read back, as every page setup is.</summary>
    [Fact]
    public void VerifiedEdit_NamesTheOperationAsNotReadBack()
    {
        Book();

        CliResult edited = Cli("cells", "edit", "book.xlsx", "--out", "verified.xlsx", "--verify", "--output-format", "json",
            "--ops", Ops(new { id = "print", op = "set_page_setup", sheet = "Summary", printComments = "sheet-end" }));

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        JsonNode verification = JsonNode.Parse(edited.StdOut)!["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.Equal("print", Text(Assert.Single(verification["uncheckedOps"]!.AsArray())!, "id"));
    }

    /// <summary>A page setup that does not name the field leaves the setting as Excel made it.</summary>
    [Fact]
    public void PageSetupWithoutTheField_KeepsTheSetting()
    {
        Book();
        PatchFirstSheet(static xml => xml.Replace("<pageSetup ", "<pageSetup cellComments=\"atEnd\" ", StringComparison.Ordinal));

        CliResult edited = Cli("cells", "edit", "book.xlsx", "--in-place", "--output-format", "json",
            "--ops", Ops(new { op = "set_page_setup", sheet = "Summary", orientation = "landscape" }));

        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        Assert.Contains("cellComments=\"atEnd\"", FirstSheet(), StringComparison.Ordinal);
        Assert.DoesNotContain("'Summary'", Text(Assert.Single(Lossy(Convert("pdf"))), "message"), StringComparison.Ordinal);
    }

    /// <summary>Set as displayed, a sheet prints the comments it shows, so only the hidden ones are left out.</summary>
    [Fact]
    public void AsDisplayed_LeavesOutOnlyTheCommentsTheSheetDoesNotShow()
    {
        using (var workbook = new Workbook())
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Data";
            sheet.Cells["A1"].PutValue("ZqxCellA");
            Comment shown = sheet.Comments[sheet.Comments.Add("B2")];
            shown.Note = "ZqxNoteA";
            shown.IsVisible = true;
            sheet.Comments[sheet.Comments.Add("B3")].Note = "ZqxNoteB";
            sheet.PageSetup.PrintComments = PrintCommentsType.PrintInPlace;
            workbook.Save(_workspace.File("book.xlsx"), SaveFormat.Xlsx);
        }

        JsonNode lossy = Assert.Single(Lossy(Convert("pdf")));

        Assert.Contains("1 comment(s)", Text(lossy, "message"), StringComparison.Ordinal);
        Assert.Equal("sheet/Data", Text(lossy, "location"));
    }

    /// <summary>
    /// Summary holds cells, a page setup that stores a pageSetup element and comments on two cells;
    /// Notes holds a cell and one comment. Cell text starts with ZqxCell and comment text with ZqxNote.
    /// </summary>
    private void Book()
    {
        Assert.Equal(0, Cli("cells", "create", "book.xlsx", "--sheet-names", "Summary,Notes").ExitCode);
        CliResult edited = Cli("cells", "edit", "book.xlsx", "--in-place", "--output-format", "json", "--ops", Ops(
            new { op = "set_values", sheet = "Summary", range = "A1", values = new object[][] { ["ZqxCellA", "Amount"], ["East", 700], ["North", 580] } },
            new { op = "set_page_setup", sheet = "Summary", orientation = "portrait" },
            new { op = "add_comment", sheet = "Summary", cell = "B2", text = "ZqxNoteA" },
            new { op = "add_comment", sheet = "Summary", cell = "B3", text = "ZqxNoteB" },
            new { op = "set_values", sheet = "Notes", range = "A1", values = new object[][] { ["ZqxCellB"] } },
            new { op = "add_comment", sheet = "Notes", cell = "A1", text = "ZqxNoteC" }));
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    private JsonNode SetPrintComments(string value, params string[] sheets)
    {
        CliResult edited = Cli("cells", "edit", "book.xlsx", "--in-place", "--output-format", "json",
            "--ops", Ops([.. sheets.Select(sheet => (object)new { op = "set_page_setup", sheet, printComments = value })]));
        Assert.True(edited.ExitCode == 0, $"exit {edited.ExitCode}: {edited.StdErr}{edited.StdOut}");
        return JsonNode.Parse(edited.StdOut)!;
    }

    private static string Ops(params object[] ops) => JsonSerializer.Serialize(new { ops });

    private string PrintComments(string sheet) =>
        Text(Run("cells", "inspect", "book.xlsx", "--detail", "layout")["workbook"]!["layouts"]!.AsArray()
            .Single(layout => Text(layout!, "sheet") == sheet)!["pageSetup"]!, "printComments");

    private JsonNode Convert(string format) => Run("cells", "convert", "book.xlsx", "--to", format, "--out", $"converted.{format}");

    /// <summary>The words starting with Zqx that the pdf holds.</summary>
    private string PdfWords() => Run("pdf", "search", "converted.pdf", "--pattern", "Zqx[A-Za-z]+", "--regex").ToJsonString();

    private string XpsPages()
    {
        using ZipArchive package = ZipFile.OpenRead(_workspace.File("converted.xps"));
        return string.Concat(package.Entries.Where(static entry => entry.Name.EndsWith(".fpage", StringComparison.Ordinal)).Select(static entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }));
    }

    private const string FirstSheetPart = "xl/worksheets/sheet1.xml";

    private string FirstSheet()
    {
        using ZipArchive package = ZipFile.OpenRead(_workspace.File("book.xlsx"));
        using var reader = new StreamReader(package.GetEntry(FirstSheetPart)!.Open());
        return reader.ReadToEnd();
    }

    /// <summary>Rewrites the XML of the first worksheet, for a setting only Excel makes.</summary>
    private void PatchFirstSheet(Func<string, string> edit)
    {
        string xml = FirstSheet();
        string edited = edit(xml);
        Assert.NotEqual(xml, edited);
        using ZipArchive package = ZipFile.Open(_workspace.File("book.xlsx"), ZipArchiveMode.Update);
        package.GetEntry(FirstSheetPart)!.Delete();
        using var writer = new StreamWriter(package.CreateEntry(FirstSheetPart).Open());
        writer.Write(edited);
    }

    private JsonNode Run(params string[] args)
    {
        CliResult result = Cli([.. args, "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.StdErr}{result.StdOut}");
        return JsonNode.Parse(result.StdOut)!;
    }

    private CliResult Cli(params string[] args) => _workspace.Run(args);

    private static JsonNode[] Lossy(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!).Where(static warning => Text(warning, "code") == "LOSSY_CONVERSION")];

    private static string Text(JsonNode node, string member) => node[member]!.GetValue<string>();
}
