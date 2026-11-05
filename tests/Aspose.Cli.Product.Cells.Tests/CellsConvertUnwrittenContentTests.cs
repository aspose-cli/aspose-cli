using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// A convert output that does not hold some content of the sheets it writes names it in a
/// <c>LOSSY_CONVERSION</c> per kind of content, with how much each sheet has: a pdf or xps
/// prints no comment the page setup leaves unprinted, and an html or mhtml page holds no
/// comment, shows the charts as pictures and keeps the headers and footers only as print data
/// in its styles. A workbook without such content, and an output that keeps it, warn of none.
/// The content stays in the workbook, so no such warning affects completeness. The commands run
/// with the test license when the run has one, and in evaluation mode otherwise.
/// </summary>
public sealed class CellsConvertUnwrittenContentTests : IDisposable
{
    private const string LossyConversion = "LOSSY_CONVERSION";

    private readonly TempWorkspace _workspace = new();

    public CellsConvertUnwrittenContentTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A printed output leaves out the comments the page setup does not print, which by default
    /// is every one: the warning counts them per sheet, names the sheet, and its hint reads them.
    /// The pdf holds the cell text and not the comment.
    /// </summary>
    [Theory]
    [InlineData("pdf")]
    [InlineData("xps")]
    public void PrintedOutput_OfSheetsWithComments_WarnsOfTheCommentsItDoesNotPrint(string format)
    {
        Book("book.xlsx", comments: true);

        JsonNode result = Convert("book.xlsx", format);

        JsonNode lossy = Assert.Single(Lossy(result));
        string message = Text(lossy, "message");
        Assert.Contains("3 comment(s)", message, StringComparison.Ordinal);
        Assert.Contains("2 on worksheet 'Summary'", message, StringComparison.Ordinal);
        Assert.Contains("1 on worksheet 'Notes'", message, StringComparison.Ordinal);
        Assert.Contains("cells inspect <file> --detail comments", Text(lossy, "hint"), StringComparison.Ordinal);
        Assert.Null(lossy["location"]);
        Assert.Null(lossy["affectsCompleteness"]);
        // Aspose.PDF evaluation mode reads only the first 4 pages, fewer than the evaluation pdf has.
        if (format == "pdf" && ScenarioLicense.Licensed)
        {
            string found = Run("pdf", "search", "converted.pdf", "--pattern", "Zqx[A-Za-z]+", "--regex").ToJsonString();
            Assert.Contains("ZqxCell", found, StringComparison.Ordinal);
            Assert.DoesNotContain("ZqxNote", found, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A pdf of one sheet names only that sheet's comments and gives the sheet as the location;
    /// the comments of a sheet it does not print are not counted.
    /// </summary>
    [Fact]
    public void PdfOfOneSheet_CountsOnlyTheCommentsOfThatSheet()
    {
        Book("book.xlsx", comments: true);

        JsonNode lossy = Assert.Single(Lossy(Convert("book.xlsx", "pdf", "--sheets", "2")));

        string message = Text(lossy, "message");
        Assert.Contains("1 comment(s)", message, StringComparison.Ordinal);
        Assert.Contains("'Notes'", message, StringComparison.Ordinal);
        Assert.DoesNotContain("'Summary'", message, StringComparison.Ordinal);
        Assert.Equal("sheet/Notes", Text(lossy, "location"));
    }

    /// <summary>
    /// A sheet whose page setup prints its comments at the end of the sheet keeps them in the
    /// pdf, so that sheet's comments are not counted, while those of a sheet that does not print
    /// them still are.
    /// </summary>
    [Fact]
    public void Pdf_OfASheetThatPrintsItsComments_DoesNotCountThem()
    {
        Book("book.xlsx", comments: true);
        PatchSheet("book.xlsx", 1, static xml => xml.Replace("<pageSetup ", "<pageSetup cellComments=\"atEnd\" ", StringComparison.Ordinal));

        JsonNode result = Convert("book.xlsx", "pdf");

        JsonNode lossy = Assert.Single(Lossy(result));
        string message = Text(lossy, "message");
        Assert.Contains("1 comment(s)", message, StringComparison.Ordinal);
        Assert.DoesNotContain("'Summary'", message, StringComparison.Ordinal);
        // Aspose.PDF evaluation mode reads only the first 4 pages, fewer than the evaluation pdf has.
        if (ScenarioLicense.Licensed)
        {
            string found = Run("pdf", "search", "converted.pdf", "--pattern", "ZqxNoteA", "--regex").ToJsonString();
            Assert.Contains("ZqxNoteA", found, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// An html or mhtml page names each kind of content it does not hold as the workbook does:
    /// the comments, the charts it shows as pictures and the headers and footers it keeps only
    /// in its print styles, each with its sheets.
    /// </summary>
    [Theory]
    [InlineData("html")]
    [InlineData("mhtml")]
    public void WebPage_OfAWorkbookWithCommentsChartsAndHeaders_NamesEachKindItLeavesOut(string format)
    {
        Book("book.xlsx", comments: true, chart: true, header: true);

        JsonNode[] lossy = Lossy(Convert("book.xlsx", format));

        Assert.Equal(3, lossy.Length);
        string comments = Text(Assert.Single(lossy, static warning => Text(warning, "message").Contains("comment", StringComparison.Ordinal)), "message");
        Assert.Contains("3 comment(s)", comments, StringComparison.Ordinal);
        Assert.Contains("2 on worksheet 'Summary', 1 on worksheet 'Notes'", comments, StringComparison.Ordinal);
        JsonNode charts = Assert.Single(lossy, static warning => Text(warning, "message").Contains("chart", StringComparison.Ordinal));
        Assert.Contains("1 chart(s) as pictures", Text(charts, "message"), StringComparison.Ordinal);
        Assert.Equal("sheet/Summary", Text(charts, "location"));
        Assert.Contains("cells inspect <file> --detail charts", Text(charts, "hint"), StringComparison.Ordinal);
        JsonNode headers = Assert.Single(lossy, static warning => Text(warning, "message").Contains("headers and footers", StringComparison.Ordinal));
        Assert.Contains("worksheet 'Summary'", Text(headers, "message"), StringComparison.Ordinal);
        Assert.Contains("cells inspect <file> --detail layout", Text(headers, "hint"), StringComparison.Ordinal);
        Assert.All(lossy, static warning => Assert.Null(warning["affectsCompleteness"]));
        // The parts of an mhtml archive are quoted-printable, whose soft line breaks may split a word.
        string page = File.ReadAllText(_workspace.File($"converted.{format}")).Replace("=\r\n", string.Empty, StringComparison.Ordinal);
        Assert.Contains("ZqxCell", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ZqxNote", page, StringComparison.Ordinal);
    }

    /// <summary>A page of a workbook with only a chart names the chart alone.</summary>
    [Fact]
    public void Html_OfAWorkbookWithOnlyAChart_NamesTheChartAlone()
    {
        Book("book.xlsx", chart: true);

        JsonNode lossy = Assert.Single(Lossy(Convert("book.xlsx", "html")));

        Assert.Contains("1 chart(s) as pictures", Text(lossy, "message"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A printout leaves out the comments and a page the comments, charts and headers; a
    /// workbook without them loses nothing to either, and a workbook output keeps them all, so
    /// neither warns of lost content.
    /// </summary>
    [Theory]
    [InlineData(true, "pdf", 1)]
    [InlineData(false, "pdf", 0)]
    [InlineData(true, "xps", 1)]
    [InlineData(false, "xps", 0)]
    [InlineData(true, "html", 3)]
    [InlineData(false, "html", 0)]
    [InlineData(true, "mhtml", 3)]
    [InlineData(false, "mhtml", 0)]
    [InlineData(true, "xlsx", 0)]
    [InlineData(true, "ods", 0)]
    public void Convert_WarnsOfLostContent_OnlyWhenTheWorkbookHasItAndTheOutputLeavesItOut(bool content, string format, int warnings)
    {
        Book("book.xlsx", comments: content, chart: content, header: content);

        Assert.Equal(warnings, Lossy(Convert("book.xlsx", format)).Length);
    }

    /// <summary>
    /// A printout prints no hidden sheet, so the comments of one are no loss of a pdf or xps:
    /// with comments on the visible sheet too, only those are counted and named, and with
    /// comments on the hidden sheet alone it warns of none. A page holds every sheet, the hidden
    /// one too, so there its comments count.
    /// </summary>
    [Theory]
    [InlineData("pdf", true, 2)]
    [InlineData("pdf", false, 0)]
    [InlineData("xps", true, 2)]
    [InlineData("xps", false, 0)]
    [InlineData("html", true, 3)]
    [InlineData("html", false, 1)]
    [InlineData("mhtml", true, 3)]
    [InlineData("mhtml", false, 1)]
    public void Convert_CountsTheCommentsOfAHiddenSheet_OnlyWhenTheOutputHoldsIt(string format, bool visibleComments, int counted)
    {
        Book("book.xlsx", comments: visibleComments, notesComment: true, hideNotes: true);

        JsonNode[] lossy = Lossy(Convert("book.xlsx", format));

        bool page = format is "html" or "mhtml";
        if (counted == 0)
        {
            Assert.Empty(lossy);
            return;
        }

        string message = Text(Assert.Single(lossy), "message");
        Assert.Contains($"{counted} comment(s)", message, StringComparison.Ordinal);
        Assert.Equal(visibleComments, message.Contains("on worksheet 'Summary'", StringComparison.Ordinal));
        Assert.Equal(page, message.Contains("1 on worksheet 'Notes'", StringComparison.Ordinal));
        if (page)
        {
            // The page holds the hidden sheet: its cell text is in it.
            Assert.Contains("ZqxCellB", File.ReadAllText(_workspace.File($"converted.{format}")).Replace("=\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A page has no pages, so a header or footer that prints only the page number or count, in
    /// whatever font and size, is no loss of it; one with text of its own, as in "Page &amp;P",
    /// or a doubled ampersand, which prints one, is.
    /// </summary>
    [Theory]
    [InlineData("Page &P", 1)]
    [InlineData("&&P", 1)]
    [InlineData("&P", 0)]
    [InlineData("&\"Arial,Bold\"&12&P&N", 0)]
    public void WebPage_NamesAHeaderOrFooter_OnlyWhenItPrintsMoreThanThePageNumber(string footer, int warnings)
    {
        Book("book.xlsx", footerCenter: footer);

        Assert.Equal(warnings, Lossy(Convert("book.xlsx", "html")).Length);
    }

    /// <summary>
    /// A workbook of two sheets: Summary holds a small table and, on request, comments on two
    /// cells, a chart and a center header and footer; Notes holds one cell and, with comments or
    /// <paramref name="notesComment"/>, a comment of its own, and with
    /// <paramref name="hideNotes"/> is hidden. <paramref name="footerCenter"/> sets the center
    /// footer of Summary alone. Cell text starts with ZqxCell and comment text with ZqxNote.
    /// </summary>
    private void Book(string name, bool comments = false, bool chart = false, bool header = false,
        bool notesComment = false, bool hideNotes = false, string? footerCenter = null)
    {
        Assert.Equal(0, Cli("cells", "create", name, "--sheet-names", "Summary,Notes").ExitCode);
        var ops = new List<object>
        {
            new { op = "set_values", sheet = "Summary", range = "A1", values = new object[][] { ["ZqxCellA", "Amount"], ["East", 700], ["North", 580] } },
            new { op = "set_values", sheet = "Notes", range = "A1", values = new object[][] { ["ZqxCellB"] } },
            new { op = "set_page_setup", sheet = "Summary", orientation = "portrait" },
        };
        if (comments)
        {
            ops.Add(new { op = "add_comment", sheet = "Summary", cell = "B2", text = "ZqxNoteA" });
            ops.Add(new { op = "add_comment", sheet = "Summary", cell = "B3", text = "ZqxNoteB" });
        }

        if (comments || notesComment)
        {
            ops.Add(new { op = "add_comment", sheet = "Notes", cell = "A1", text = "ZqxNoteC" });
        }

        if (hideNotes)
        {
            ops.Add(new { op = "set_sheet_visibility", sheet = "Notes", visibility = "hidden" });
        }

        if (footerCenter is not null)
        {
            ops.Add(new { op = "set_page_setup", sheet = "Summary", footerCenter });
        }

        if (chart)
        {
            ops.Add(new { op = "insert_chart", sheet = "Summary", kind = "column", dataRange = "A1:B3", at = "D2:I12", title = "Revenue" });
        }

        if (header)
        {
            ops.Add(new { op = "set_page_setup", sheet = "Summary", headerCenter = "Quarterly", footerCenter = "Page &P" });
        }

        CliResult edited = Cli("cells", "edit", name, "--in-place", "--ops", JsonSerializer.Serialize(new { ops }), "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
    }

    /// <summary>Rewrites the XML of the <paramref name="sheet"/>th worksheet of an xlsx, for a setting no operation writes.</summary>
    private void PatchSheet(string name, int sheet, Func<string, string> edit)
    {
        using ZipArchive package = ZipFile.Open(_workspace.File(name), ZipArchiveMode.Update);
        ZipArchiveEntry entry = package.GetEntry($"xl/worksheets/sheet{sheet}.xml")!;
        string xml;
        using (var reader = new StreamReader(entry.Open()))
        {
            xml = reader.ReadToEnd();
        }

        string edited = edit(xml);
        Assert.NotEqual(xml, edited);
        entry.Delete();
        using var writer = new StreamWriter(package.CreateEntry($"xl/worksheets/sheet{sheet}.xml").Open());
        writer.Write(edited);
    }

    private JsonNode Convert(string input, string format, params string[] options)
    {
        CliResult converted = Cli(["cells", "convert", input, "--to", format, "--out", $"converted.{format}", .. options, "--output-format", "json"]);
        Assert.True(converted.ExitCode == 0, $"exit {converted.ExitCode}: {converted.StdErr}{converted.StdOut}");
        return JsonNode.Parse(converted.StdOut)!;
    }

    private JsonNode Run(params string[] args)
    {
        CliResult result = Cli([.. args, "--output-format", "json"]);
        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.StdErr}{result.StdOut}");
        return JsonNode.Parse(result.StdOut)!;
    }

    private CliResult Cli(params string[] args) => _workspace.Run(args);

    private static JsonNode[] Lossy(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!).Where(static warning => Text(warning, "code") == LossyConversion)];

    private static string Text(JsonNode node, string member) => node[member]!.GetValue<string>();
}
