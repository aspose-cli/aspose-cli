using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsCliTests : IDisposable
{
    private const string Secret = "test-secret-must-not-leak";
    private readonly TempWorkspace _workspace = new();
    private readonly List<Process> _previewServices = [];
    private bool _previewRequested;

    [Category(TestCategory.Slow)]
    [Fact]
    public void CreateEditAndQuery_RoundTripsThroughTheBuiltCli()
    {
        Assert.Equal(0, _workspace.Run(
            "cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        CliResult inspected = _workspace.Run(
            "cells", "inspect", "book.xlsx", "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        string fingerprint = JsonNode.Parse(inspected.StdOut)!["input"]![
            "fingerprint"]!["sha256"]!.GetValue<string>();
        File.WriteAllText(
            _workspace.File("ops.json"),
            """
            {
              "ops": [
                { "op": "set_values", "sheet": "Data", "range": "A1:B2", "values": [["Value","Double"],[21,42]] },
                { "id": "double", "op": "set_formula", "sheet": "Data", "range": "B3", "formula": "=B2*2" }
              ]
            }
            """);

        CliResult edited = _workspace.Run(
            "cells", "edit", "book.xlsx", "--ops", "ops.json",
            "--if-match", fingerprint, "--in-place", "--output-format", "json");
        CliResult read = _workspace.Run(
            "cells", "read", "book.xlsx", "--sheet", "Data",
            "--range", "B2:B3", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.True(read.ExitCode == 0, read.StdErr);
        JsonNode editPayload = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal(fingerprint, editPayload["input"]!["fingerprint"]!["sha256"]!.GetValue<string>());
        Assert.NotEqual(
            fingerprint,
            editPayload["output"]!["fingerprint"]!["sha256"]!.GetValue<string>());
        Assert.Equal("op-0001", editPayload["applied"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("double", editPayload["applied"]![1]!["id"]!.GetValue<string>());
        Assert.Equal(
            ["cell/Data!A1", "cell/Data!B3"],
            editPayload["applied"]!.AsArray()
                .Select(static item => item!["targets"]![0]!.GetValue<string>()));
        JsonNode payload = JsonNode.Parse(read.StdOut)!;
        Assert.Equal(42, payload["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<double>());
        Assert.Equal(84, payload["sheet"]!["cells"]![1]![0]!["v"]!.GetValue<double>());
    }

    [Fact]
    public void Read_OverTheBudgetReturnsTheFirstWindowAndRunnableNextCommands()
    {
        File.WriteAllLines(
            _workspace.File("grid.csv"),
            Enumerable.Range(1, 10).Select(static row => $"{row},{row * 2},{row * 3}"));

        // A default read and an explicit range over the budget both scan the data page by page.
        foreach (string[] range in new[] { Array.Empty<string>(), ["--range", "A1:C10"] })
        {
            CliResult read = _workspace.Run(["cells", "read", "grid.csv", .. range, "--max-cells", "10", "--output-format", "json"]);
            Assert.True(read.ExitCode == 0, read.StdErr);
            JsonNode payload = JsonNode.Parse(read.StdOut)!;
            Assert.Equal("A1:C3", payload["sheet"]!["range"]!.GetValue<string>());
            JsonNode window = payload["window"]!;
            Assert.Equal(("cell", 9, 30, true), (
                window["unit"]!.GetValue<string>(),
                window["returned"]!.GetValue<int>(),
                window["total"]!.GetValue<int>(),
                window["truncated"]!.GetValue<bool>()));
            Assert.EndsWith(
                " --range A4:C6 --scan-range A1:C10 --content values --max-cells 10 --output-format json",
                window["next"]!.GetValue<string>(),
                StringComparison.Ordinal);
            Assert.True(read.StdErr.Length == 0, read.StdErr);
        }

        CliResult first = _workspace.Run("cells", "read", "grid.csv", "--max-cells", "10", "--output-format", "json");
        CliResult second = _workspace.RunCommandLine(JsonNode.Parse(first.StdOut)!["window"]!["next"]!.GetValue<string>());
        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode page = JsonNode.Parse(second.StdOut)!;
        Assert.Equal("A4:C6", page["sheet"]!["range"]!.GetValue<string>());
        Assert.EndsWith(
            " --range A7:C9 --scan-range A1:C10 --content values --max-cells 10 --output-format json",
            page["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);

        CliResult last = _workspace.Run(
            "cells", "read", "grid.csv", "--range", "A10:C10", "--scan-range", "A1:C10", "--max-cells", "10", "--output-format", "json");
        Assert.True(last.ExitCode == 0, last.StdErr);
        JsonNode lastWindow = JsonNode.Parse(last.StdOut)!["window"]!;
        Assert.False(lastWindow["truncated"]!.GetValue<bool>());
        Assert.Null(lastWindow["next"]);
    }

    [Fact]
    public void Search_TruncatedWindowNextReturnsTheFollowingHits()
    {
        File.WriteAllLines(
            _workspace.File("hits.csv"),
            Enumerable.Range(1, 5).Select(static row => $"hit-{row},other"));

        CliResult first = _workspace.Run(
            "cells", "search", "hits.csv", "--pattern", "HIT-", "--max-hits", "2", "--output-format", "json");

        Assert.True(first.ExitCode == 0, first.StdErr);
        JsonNode firstPayload = JsonNode.Parse(first.StdOut)!;
        Assert.Equal(["A1", "A2"], firstPayload["hits"]!.AsArray().Select(static hit => hit!["cell"]!.GetValue<string>()));
        JsonNode window = firstPayload["window"]!;
        Assert.Equal(("hit", 2, true), (
            window["unit"]!.GetValue<string>(),
            window["returned"]!.GetValue<int>(),
            window["truncated"]!.GetValue<bool>()));
        string next = window["next"]!.GetValue<string>();
        Assert.Contains(" --skip 2 ", next, StringComparison.Ordinal);

        CliResult second = _workspace.RunCommandLine(next);
        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode secondPayload = JsonNode.Parse(second.StdOut)!;
        Assert.Equal(["A3", "A4"], secondPayload["hits"]!.AsArray().Select(static hit => hit!["cell"]!.GetValue<string>()));

        CliResult third = _workspace.RunCommandLine(secondPayload["window"]!["next"]!.GetValue<string>());
        Assert.True(third.ExitCode == 0, third.StdErr);
        JsonNode thirdPayload = JsonNode.Parse(third.StdOut)!;
        Assert.Equal("A5", Assert.Single(thirdPayload["hits"]!.AsArray())!["cell"]!.GetValue<string>());
        Assert.False(thirdPayload["window"]!["truncated"]!.GetValue<bool>());
        Assert.Null(thirdPayload["window"]!["next"]);
    }

    [Fact]
    public void Edit_BestEffortTablePrintsTheFailedOperationErrorAndHint()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);

        CliResult edited = _workspace.Run(
            "cells", "edit", "book.xlsx", "--set", "Data!A1=1", "--set", "Missing!A1=2",
            "--best-effort", "--out", "edited.xlsx", "--output-format", "table");

        Assert.Equal(8, edited.ExitCode);
        Assert.Contains("1 of 2 op(s) applied, 1 failed", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("[op-0002] set_values: failed", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("      SHEET_NOT_FOUND: ", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("      hint: ", edited.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("table")]
    [InlineData("markdown")]
    public void Inspect_HumanOutputShowsTheRequestedDetails(string format)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        File.WriteAllText(
            _workspace.File("ops.json"),
            """
            { "ops": [
              { "op": "set_values", "sheet": "Data", "range": "A1:B3", "values": [["Region","Sales"],["East",10],["West",20]] },
              { "op": "set_formula", "sheet": "Data", "range": "C2", "formula": "=1/0" },
              { "op": "define_name", "name": "SalesTotal", "refersTo": "=Data!$B$2:$B$3" },
              { "op": "insert_chart", "sheet": "Data", "kind": "column", "dataRange": "A1:B3", "at": "E2:J12", "title": "Sales" }
            ] }
            """);
        CliResult edited = _workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place", "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);

        CliResult inspected = _workspace.Run(
            "cells", "inspect", "book.xlsx", "--detail", "names", "errors", "charts", "--output-format", format);

        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        string Heading(string title) => format == "markdown" ? $"### {title}" : $"{title}:";
        Assert.Contains(Heading("charts"), inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("column", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains(Heading("names"), inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("SalesTotal", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("=Data!$B$2:$B$3", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains(Heading("formula errors"), inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("#DIV/0!", inspected.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_RefusesAFormulaTheEngineCannotParseAsAnInvalidOperation()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);

        CliResult refused = _workspace.Run(
            "cells", "edit", "book.xlsx", "--in-place", "--output-format", "json", "--ops",
            """{"ops":[{"op":"set_formula","sheet":"Data","range":"A1:A2","formula":"=SUM(B1;B2)"}]}""");

        Assert.Equal(4, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("=SUM(B1;B2)", error["details"]!["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("commas", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("render", "x.png", "预算, 2026 H1", 1)]
    [InlineData("convert", "x.pdf", "预算, 2026 H1", 1)]
    [InlineData("render", "x.png", "lists", 2)]
    [InlineData("convert", "x.csv", "Lists", 2)]
    public void ASheetSelectionByName_ListsEachSheetWithItsPosition(string verb, string output, string name, int position)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "named.xlsx", "--sheets", "Budget,Lists").ExitCode);
        File.WriteAllText(_workspace.File("rename.json"), """{"ops":[{"op":"rename_sheet","sheet":"Budget","name":"预算, 2026 H1"}]}""");
        Assert.Equal(0, _workspace.Run("cells", "edit", "named.xlsx", "--ops", "rename.json", "--in-place").ExitCode);

        CliResult result = _workspace.Run("cells", verb, "named.xlsx", "--sheets", name, "--out", output, "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        string hint = error["hint"]!.GetValue<string>();
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains($"'{name}'", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["1: 预算, 2026 H1", "2: Lists"], error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()).Take(2));
        Assert.Contains($"--sheets {position}", hint, StringComparison.Ordinal);
        Assert.Contains("details.available", hint, StringComparison.Ordinal);
        Assert.Equal(verb == "render", hint.Contains("--range", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("render", "x.png")]
    [InlineData("convert", "x.csv")]
    public void ASheetNamedLikeAPositionPastTheLastSheet_ListsEachSheetWithItsPosition(string verb, string output)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "years.xlsx", "--sheets", "Budget,Lists,2024").ExitCode);

        CliResult result = _workspace.Run("cells", verb, "years.xlsx", "--sheets", "2024", "--out", output, "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("3: 2024", error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Contains("--sheets 3", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData(" All ")]
    public void TheAllKeyword_SelectsEverySheetInAnyCase(string all)
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "every.xlsx", "--sheets", "Budget,Lists").ExitCode);

        CliResult result = _workspace.Run("cells", "convert", "every.xlsx", "--sheets", all, "--out", "every.pdf", "--output-format", "json");

        Assert.True(result.ExitCode == 0, result.StdErr);
    }

    [Fact]
    public void Inspect_SampleRowsWithoutSample_IsAnOptionError()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "sampled.xlsx").ExitCode);

        CliResult result = _workspace.Run("cells", "inspect", "sampled.xlsx", "--sample-rows", "3", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--sample-rows", error["details"]!["option"]!.GetValue<string>());
        Assert.Equal(0, _workspace.Run("cells", "inspect", "sampled.xlsx", "--sample", "--sample-rows", "3").ExitCode);
    }

    [Fact]
    public void Edit_AnUnknownFieldNamesTheAcceptedFieldsAndTheLikelyOne()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);

        CliResult renamed = _workspace.Run(
            "cells", "edit", "book.xlsx", "--in-place", "--output-format", "json", "--ops",
            """{"ops":[{"op":"rename_sheet","sheet":"Data","to":"Sales"}]}""");
        CliResult sorted = _workspace.Run(
            "cells", "edit", "book.xlsx", "--in-place", "--output-format", "json", "--ops",
            """{"ops":[{"op":"sort_range","sheet":"Data","range":"A1:B4","by":[{"column":"A","direction":"desc"}]}]}""");

        Assert.Equal(4, renamed.ExitCode);
        JsonNode rename = JsonNode.Parse(renamed.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", rename["code"]!.GetValue<string>());
        Assert.Equal("unknown field 'to'", rename["details"]!["reason"]!.GetValue<string>());
        Assert.Equal(["op", "id", "sheet", "name"],
            rename["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(["name"], rename["details"]!["suggestions"]!.AsArray().Select(static item => item!.GetValue<string>()));
        JsonNode sort = JsonNode.Parse(sorted.StdErr)!["error"]!["details"]!;
        Assert.Equal("unknown field 'by[0].direction'", sort["reason"]!.GetValue<string>());
        Assert.Equal(["column", "order"], sort["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Null(sort["suggestions"]);
    }

    [Fact]
    public void Convert_StartsATextOutputWithAByteOrderMarkOnRequest()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--set", "Data!A1=交易").ExitCode);

        Assert.Equal(0, _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv", "--sheets", "1", "--out", "plain.csv").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "convert", "book.xlsx", "--to", "tsv", "--sheets", "1", "--bom", "--out", "excel.tsv").ExitCode);
        CliResult refused = _workspace.Run("cells", "convert", "book.xlsx", "--to", "xlsx", "--bom", "--out", "copy.xlsx", "--output-format", "json");

        Assert.Equal([0xE4, 0xBA, 0xA4], File.ReadAllBytes(_workspace.File("plain.csv"))[..3]);
        Assert.Equal([0xEF, 0xBB, 0xBF, 0xE4, 0xBA, 0xA4], File.ReadAllBytes(_workspace.File("excel.tsv"))[..6]);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("--bom", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_RefusesSheetForAFormatThatConvertsTheWholeWorkbook()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);

        CliResult refused = _workspace.Run("cells", "convert", "book.xlsx", "--to", "xlsx", "--sheets", "1", "--out", "copy.xlsx", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("--sheets", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("copy.xlsx")));
    }

    /// <summary>
    /// Every CLI child runs in evaluation mode, the only place Cells evaluation is tested:
    /// the in-process engine suite needs a license.
    /// </summary>
    [Fact]
    public void Evaluation_DisclosesTheWatermarkAndTheNoticeInDataAndRefusesASilentSheetSubstitution()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Dashboard,Detail").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place",
            "--set", "Dashboard!A1=Overview", "--set", "Detail!A1=SO-001").ExitCode);
        File.WriteAllText(_workspace.File("report.csv"), "existing report");

        CliResult refused = _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv",
            "--sheets", "2", "--out", "report.csv", "--overwrite", "--output-format", "json");
        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv",
            "--out", "first.csv", "--output-format", "json");

        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("EVALUATION_LIMIT", error["code"]!.GetValue<string>());
        Assert.Equal("Dashboard", Assert.Single(error["details"]!["available"]!.AsArray())!.GetValue<string>());
        Assert.Contains("license", error["hint"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(refused.StdOut);
        Assert.Equal("existing report", File.ReadAllText(_workspace.File("report.csv")));
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode result = JsonNode.Parse(converted.StdOut)!;
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(Warning(result, "EVALUATION_MODE")["hint"]?.GetValue<string>()));
        // Without --sheets the first sheet is exported, and the evaluation notice becomes its last row.
        Assert.Contains("Only worksheet 'Dashboard' was exported",
            Warning(result, "SHEETS_DROPPED")["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("Evaluation Only.", File.ReadAllLines(_workspace.File("first.csv"))[^1], StringComparison.Ordinal);
        Assert.Contains("as the last row of the csv output", Warning(result, "EVALUATION_MODE")["message"]!.GetValue<string>(), StringComparison.Ordinal);
        CliResult json = _workspace.Run("cells", "convert", "book.xlsx", "--to", "json", "--out", "book.json", "--output-format", "json");
        Assert.Contains("\"Evaluation Warning\"", File.ReadAllText(_workspace.File("book.json")), StringComparison.Ordinal);
        Assert.Contains("\"watermark\"", Warning(json.Json(), "EVALUATION_MODE")["message"]!.GetValue<string>(), StringComparison.Ordinal);
        CliResult edited = _workspace.Run("cells", "edit", "book.xlsx", "--out", "edited.tsv", "--set", "Dashboard!B1=1", "--output-format", "json");
        Assert.StartsWith("Evaluation Only.", File.ReadAllLines(_workspace.File("edited.tsv"))[^1], StringComparison.Ordinal);
        Assert.Contains("as the last row of the tsv output", Warning(edited.Json(), "EVALUATION_MODE")["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertAndReview_ReportAChartAPdfSplitsAcrossPages()
    {
        CellsReviewTests.CreateWideChartWorkbook(_workspace.File("split.xlsx"), fitToOnePageWide: false);
        CellsReviewTests.CreateWideChartWorkbook(_workspace.File("fitted.xlsx"), fitToOnePageWide: true);

        JsonNode split = _workspace.Run("cells", "convert", "split.xlsx", "--to", "pdf", "--out", "split.pdf", "--output-format", "json").Json();
        JsonNode fitted = _workspace.Run("cells", "convert", "fitted.xlsx", "--to", "pdf", "--out", "fitted.pdf", "--output-format", "json").Json();

        JsonNode warning = Warning(split, "CHART_SPLIT_ACROSS_PAGES");
        Assert.Equal("'Data'", warning["location"]!.GetValue<string>());
        Assert.Contains("'Wide' on sheet 'Data' (2 pages)", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(fitted["warnings"]?.AsArray() ?? [],
            static warning => warning!["code"]!.GetValue<string>() == "CHART_SPLIT_ACROSS_PAGES");

        // Review reports it too, with the image of that sheet alone as evidence.
        CliResult review = _workspace.Run("review", "split.xlsx", "--out", _workspace.File("review"), "--output-format", "json");
        Assert.True(review.ExitCode == 0, review.StdErr);
        JsonNode finding = Assert.Single(JsonNode.Parse(review.StdOut)!["findings"]!.AsArray(),
            static finding => finding!["code"]!.GetValue<string>() == "CELLS_CHART_SPLIT_ACROSS_PAGES")!;
        string evidence = Assert.Single(finding["evidence"]!.AsArray())!.GetValue<string>();
        Assert.EndsWith("sheet-0001.png", evidence, StringComparison.Ordinal);
    }

    /// <summary>
    /// A render without <c>--sheet</c> or <c>--range</c> draws every visible sheet, each as
    /// <c>&lt;stem&gt;.&lt;sheet&gt;.&lt;ext&gt;</c> beside <c>--out</c>, and a workbook of one sheet as
    /// <c>--out</c> itself.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void Render_DrawsEveryVisibleSheetByDefault()
    {
        File.WriteAllText(_workspace.File("ops.json"), """
            {"ops":[
              {"op":"set_values","sheet":"One","range":"A1","values":[["one"]]},
              {"op":"set_values","sheet":"Two","range":"A1","values":[["two"]]}]}
            """);
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "One,Two", "--output-format", "json").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place", "--output-format", "json").ExitCode);
        Directory.CreateDirectory(_workspace.File("every"));

        JsonNode every = _workspace.Run("cells", "render", "book.xlsx", "--out", "every/book.png", "--output-format", "json").Json();

        string[] sheets = [.. every["outputs"]!.AsArray().Select(static output => output!["sheet"]!.GetValue<string>())];
        // An evaluation save adds warning sheets, which the default leaves out.
        Assert.Equal(["One", "Two"], sheets);
        Assert.Equal(["book.One.png", "book.Two.png"], Directory.GetFiles(_workspace.File("every")).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        string names = string.Join(",", Enumerable.Range(1, 21).Select(static sheet => $"S{sheet}"));
        Assert.Equal(0, _workspace.Run("cells", "create", "wide.xlsx", "--sheets", names, "--output-format", "json").ExitCode);
        CliResult wide = _workspace.Run("cells", "render", "wide.xlsx", "--out", "wide/wide.png", "--output-format", "json");
        Assert.Equal(2, wide.ExitCode);
        JsonNode error = JsonNode.Parse(wide.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal("--sheets", error["details"]!["option"]!.GetValue<string>());
        Assert.Equal(20, error["details"]!["limit"]!.GetValue<int>());
        Assert.False(Directory.Exists(_workspace.File("wide")));
    }

    /// <summary>
    /// A default render of a workbook of one sheet, besides any warning sheets an evaluation save
    /// added, writes <c>--out</c> itself.
    /// </summary>
    [Fact]
    public void Render_ByDefaultWritesTheOutItselfForOneSheet()
    {
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_values","sheet":"Only","range":"A1","values":[["only"]]}]}""");
        Assert.Equal(0, _workspace.Run("cells", "create", "only.xlsx", "--sheets", "Only", "--output-format", "json").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "only.xlsx", "--ops", "ops.json", "--in-place", "--output-format", "json").ExitCode);
        Directory.CreateDirectory(_workspace.File("single"));

        JsonNode single = _workspace.Run("cells", "render", "only.xlsx", "--out", "single/only.png", "--output-format", "json").Json();

        Assert.Equal(["only.png"], Directory.GetFiles(_workspace.File("single")).Select(Path.GetFileName));
        Assert.Equal("Only", Assert.Single(single["outputs"]!.AsArray())!["sheet"]!.GetValue<string>());
    }

    /// <summary>
    /// The hint of an empty sheet's <c>RENDER_EMPTY</c> names the <c>--sheets</c> position of a
    /// sheet that has data, and the render it gives succeeds.
    /// </summary>
    [Fact]
    public void Render_OfAnEmptySheet_HintsTheSheetsSelectionOfOneWithData()
    {
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_values","sheet":"Data","range":"A1","values":[["data"]]}]}""");
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Blank,Data", "--output-format", "json").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place", "--output-format", "json").ExitCode);

        CliResult empty = _workspace.Run("cells", "render", "book.xlsx", "--sheets", "1", "--out", "blank.png", "--output-format", "json");

        JsonNode error = JsonNode.Parse(empty.StdErr)!["error"]!;
        Assert.Equal("RENDER_EMPTY", error["code"]!.GetValue<string>());
        System.Text.RegularExpressions.Match hinted = System.Text.RegularExpressions.Regex.Match(error["hint"]!.GetValue<string>(), @"--sheets (\S+) for 'Data'");
        Assert.True(hinted.Success, error["hint"]!.GetValue<string>());
        JsonNode rendered = _workspace.Run("cells", "render", "book.xlsx", "--sheets", hinted.Groups[1].Value, "--out", "data.png", "--output-format", "json").Json();
        Assert.Equal("Data", Assert.Single(rendered["outputs"]!.AsArray())!["sheet"]!.GetValue<string>());
    }

    /// <summary>
    /// Beside an empty sheet, the warning sheet an evaluation save adds holds only the notice, which
    /// is not data: the hint of <c>RENDER_EMPTY</c> says no other sheet has data instead of pointing to it.
    /// </summary>
    [Fact]
    public void Render_OfAnEmptySheet_DoesNotHintTheEvaluationWarningSheet()
    {
        using (var workbook = new Aspose.Cells.Workbook())
        {
            workbook.Worksheets[0].Name = "Blank";
            Aspose.Cells.Worksheet warning = workbook.Worksheets[workbook.Worksheets.Add()];
            warning.Name = "Evaluation Warning";
            warning.Cells["A1"].PutValue("Evaluation Only. Created with Aspose.Cells for .NET.");
            workbook.Save(_workspace.File("book.xlsx"));
        }

        CliResult empty = _workspace.Run("cells", "render", "book.xlsx", "--sheets", "1", "--out", "blank.png", "--output-format", "json");

        JsonNode error = JsonNode.Parse(empty.StdErr)!["error"]!;
        string hint = error["hint"]!.GetValue<string>();
        Assert.Equal("RENDER_EMPTY", error["code"]!.GetValue<string>());
        Assert.StartsWith("No other sheet has data to render", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--sheets", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// When an evaluation save left its warning sheet active, the hint of the
    /// <c>ACTIVE_SHEET_SKIPPED</c> each command warns names the way that command chooses a sheet:
    /// <c>--sheets</c> for a convert, an operation's <c>"sheet"</c> for an edit, and <c>--sheet</c>
    /// for a read.
    /// </summary>
    [Fact]
    public void Evaluation_TheSkippedSheetHintNamesHowEachCommandChoosesASheet()
    {
        _ = _workspace.Run("cells", "create", "book.xlsx", "--sheets", "One,Two", "--output-format", "json").Json();
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_values","range":"A1","values":[["edited"]]}]}""");

        string Hint(params string[] args) =>
            Warning(_workspace.Run([.. args, "--output-format", "json"]).Json(), "ACTIVE_SHEET_SKIPPED")["hint"]!.GetValue<string>();
        string convert = Hint("cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv");
        string edit = Hint("cells", "edit", "book.xlsx", "--ops", "ops.json", "--out", "edited.xlsx");
        string read = Hint("cells", "read", "book.xlsx");

        Assert.Contains("--sheets", convert, StringComparison.Ordinal);
        Assert.Contains("position", convert, StringComparison.Ordinal);
        // Done as the hint says, with the position inspect gives, the convert writes that sheet;
        // evaluation mode exports only the first worksheet to a text format.
        JsonNode sheets = _workspace.Run("cells", "inspect", "book.xlsx", "--output-format", "json").Json()["workbook"]!["sheets"]!;
        string position = sheets.AsArray().Single(static sheet => sheet!["name"]!.GetValue<string>() == "One")!["position"]!.ToJsonString();
        Assert.Equal(0, _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv", "--sheets", position, "--out", "one.csv", "--output-format", "json").ExitCode);
        Assert.Contains("\"sheet\"", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("--sheet", edit, StringComparison.Ordinal);
        Assert.Contains("--sheet ", read, StringComparison.Ordinal);
        Assert.Contains("name", read, StringComparison.Ordinal);
    }

    /// <summary>The one warning of <paramref name="result"/> with <paramref name="code"/>.</summary>
    private static JsonNode Warning(JsonNode result, string code) =>
        Assert.Single(result["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == code)!;

    /// <summary>
    /// An evaluation save adds a warning sheet and activates it; its EVALUATION_MODE says so, and later
    /// commands that default to the active sheet use the workbook's first sheet and say so, while
    /// commands that name their sheet or cover every sheet do not.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void Evaluation_TheAddedWarningSheetIsDisclosedAndSkippedByActiveSheetDefaults()
    {
        JsonNode created = _workspace.Run("cells", "create", "book.xlsx", "--sheets", "One,Two", "--output-format", "json").Json();
        Assert.Contains("the evaluation warning sheet 'Evaluation Warning', which is the active sheet",
            Warning(created, "EVALUATION_MODE")["message"]!.GetValue<string>(), StringComparison.Ordinal);
        File.WriteAllText(_workspace.File("ops.json"), """
            {"ops":[
              {"op":"set_values","sheet":"One","range":"A1","values":[["one"]]},
              {"op":"set_values","sheet":"Two","range":"A1","values":[["two"]]},
              {"op":"set_active_sheet","sheet":"Two"}]}
            """);

        JsonNode edited = _workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place", "--output-format", "json").Json();
        Assert.Contains("the evaluation warning sheet 'Evaluation Warning (1)', which is the active sheet",
            Warning(edited, "EVALUATION_MODE")["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // Every operation named its sheet, so nothing defaulted to the active sheet.
        AssertNoSkippedSheet(edited);

        JsonNode read = _workspace.Run("cells", "read", "book.xlsx", "--output-format", "json").Json();
        JsonNode rendered = _workspace.Run("cells", "render", "book.xlsx", "--out", "book.png", "--output-format", "json").Json();
        JsonNode renderedAll = _workspace.Run("cells", "render", "book.xlsx", "--sheets", "all", "--out", "all.png", "--output-format", "json").Json();
        JsonNode csv = _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv", "--output-format", "json").Json();
        JsonNode pdf = _workspace.Run("cells", "convert", "book.xlsx", "--to", "pdf", "--out", "book.pdf", "--output-format", "json").Json();
        JsonNode chosen = _workspace.Run("cells", "read", "book.xlsx", "--sheet", "Two", "--output-format", "json").Json();
        JsonNode chosenPdf = _workspace.Run("cells", "convert", "book.xlsx", "--to", "pdf", "--sheets", "2", "--out", "two.pdf", "--output-format", "json").Json();

        Assert.Equal("One", read["sheet"]!["name"]!.GetValue<string>());
        JsonNode skipped = Warning(read, "ACTIVE_SHEET_SKIPPED");
        Assert.Equal("Evaluation Warning (1)", skipped["location"]!.GetValue<string>());
        Assert.Contains("uses 'One'", skipped["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // A render covers every visible sheet by default, so it skips no active sheet.
        Assert.Equal("One", rendered["outputs"]![0]!["sheet"]!.GetValue<string>());
        AssertNoSkippedSheet(rendered);
        AssertNoSkippedSheet(renderedAll);
        Assert.StartsWith("one", File.ReadAllText(_workspace.File("book.csv")), StringComparison.Ordinal);
        _ = Warning(csv, "ACTIVE_SHEET_SKIPPED");
        _ = Warning(csv, "EVALUATION_MODE");
        AssertNoSkippedSheet(pdf);
        Assert.True(new FileInfo(_workspace.File("book.pdf")).Length > 0);
        // The warning sheets earlier saves added print as pages of a whole-workbook PDF.
        string pdfDisclosure = Warning(pdf, "EVALUATION_MODE")["message"]!.GetValue<string>();
        Assert.Contains("the pages of the evaluation warning sheet 'Evaluation Warning'", pdfDisclosure, StringComparison.Ordinal);
        Assert.Contains("the pages of the evaluation warning sheet 'Evaluation Warning (1)'", pdfDisclosure, StringComparison.Ordinal);
        Assert.DoesNotContain("evaluation warning sheet", Warning(chosenPdf, "EVALUATION_MODE")["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("Two", chosen["sheet"]!["name"]!.GetValue<string>());
        Assert.Equal("two", chosen["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
        AssertNoSkippedSheet(chosen);
        Assert.Equal(["Two"], chosenPdf["sheets"]!.AsArray().Select(static sheet => sheet!.GetValue<string>()));
        AssertNoSkippedSheet(chosenPdf);

        static void AssertNoSkippedSheet(JsonNode result) =>
            Assert.DoesNotContain(result["warnings"]?.AsArray() ?? [],
                static warning => warning!["code"]!.GetValue<string>() == "ACTIVE_SHEET_SKIPPED");
    }

    [Fact]
    public void PasswordEnvironmentAndStdin_RoundTripEncryptedOutputWithoutLeaks()
    {
        var variables = new Dictionary<string, string?>
        {
            ["ASPOSE_CLI_TEST_PASSWORD"] = Secret,
        };

        CliResult created = _workspace.RunWithEnv(
            variables,
            "cells", "create", "secret.xlsx", "--sheets", "Data",
            "--encrypt-env", "ASPOSE_CLI_TEST_PASSWORD", "--output-format", "json");
        CliResult fromEnvironment = _workspace.RunWithEnv(
            variables,
            "cells", "inspect", "secret.xlsx",
            "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output-format", "json");
        CliResult fromStdin = _workspace.RunWithInput(
            Secret + Environment.NewLine,
            "cells", "inspect", "secret.xlsx", "--password-stdin", "--output-format", "json");
        CliResult withoutPassword = _workspace.Run(
            "cells", "inspect", "secret.xlsx", "--output-format", "json");

        Assert.Equal(0, created.ExitCode);
        Assert.True(JsonNode.Parse(created.StdOut)!["output"]!["encrypted"]!.GetValue<bool>());
        Assert.Equal(0, fromEnvironment.ExitCode);
        Assert.Equal(0, fromStdin.ExitCode);
        Assert.Equal(3, withoutPassword.ExitCode);
        Assert.Contains(
            "PASSWORD_REQUIRED",
            withoutPassword.StdErr,
            StringComparison.Ordinal);
        foreach (string output in new[]
        {
            created.StdOut,
            created.StdErr,
            fromEnvironment.StdOut,
            fromEnvironment.StdErr,
            fromStdin.StdOut,
            fromStdin.StdErr,
            withoutPassword.StdOut,
            withoutPassword.StdErr,
        })
        {
            Assert.DoesNotContain(Secret, output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Review_RecognizesAnEncryptedWorkbookByItsExtension()
    {
        var variables = new Dictionary<string, string?> { ["ASPOSE_CLI_TEST_PASSWORD"] = Secret };
        Assert.Equal(0, _workspace.RunWithEnv(variables,
            "cells", "create", "secret.xlsx", "--sheets", "Data", "--encrypt-env", "ASPOSE_CLI_TEST_PASSWORD").ExitCode);

        CliResult locked = _workspace.Run("review", "secret.xlsx", "--out", _workspace.File("locked"), "--output-format", "json");
        CliResult opened = _workspace.RunWithEnv(variables,
            "review", "secret.xlsx", "--out", _workspace.File("opened"), "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output-format", "json");

        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(opened.ExitCode == 0, opened.StdErr);
        Assert.Equal("cells", JsonNode.Parse(opened.StdOut)!["product"]!.GetValue<string>());
        Assert.True(JsonNode.Parse(opened.StdOut)!["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Fact]
    public void Convert_EncryptsAProtectableOutputAndNamesTheOptionForAnyOther()
    {
        File.WriteAllText(_workspace.File("sales.csv"), "Region,Revenue\nEast,1200\n");
        var variables = new Dictionary<string, string?> { ["ASPOSE_CLI_TEST_PASSWORD"] = Secret };

        CliResult converted = _workspace.RunWithEnv(
            variables,
            "cells", "convert", "sales.csv", "--to", "xlsx", "--encrypt-env", "ASPOSE_CLI_TEST_PASSWORD", "--output-format", "json");
        CliResult locked = _workspace.Run("cells", "inspect", "sales.xlsx", "--output-format", "json");
        CliResult opened = _workspace.RunWithEnv(
            variables, "cells", "inspect", "sales.xlsx", "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output-format", "json");
        CliResult refused = _workspace.Run(
            "cells", "convert", "sales.csv", "--to", "pdf", "--encrypt-env", "ASPOSE_CLI_TEST_UNSET", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.True(JsonNode.Parse(converted.StdOut)!["output"]!["encrypted"]!.GetValue<bool>());
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(opened.ExitCode == 0, opened.StdErr);
        Assert.True(JsonNode.Parse(opened.StdOut)!["input"]!["encrypted"]!.GetValue<bool>());
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--encrypt-env", error["details"]!["option"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("sales.pdf")));
        Assert.DoesNotContain(Secret, converted.StdOut + converted.StdErr + opened.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_ResolvesOperationFilesLikeEveryInputBeforeEditing()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "images.xlsx", "--sheets", "Data").ExitCode);

        CliResult missing = _workspace.Run(
            "cells", "edit", "images.xlsx", "--out", "images.out.xlsx", "--best-effort", "--output-format", "json",
            "--ops", """{"ops":[{"op":"insert_image","sheet":"Data","at":"B2","path":"missing.png"}]}""");

        JsonNode error = JsonNode.Parse(missing.StdErr)!["error"]!;
        Assert.Equal("FILE_NOT_FOUND", error["code"]!.GetValue<string>());
        Assert.Contains("missing.png", error["message"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(_workspace.File("images.out.xlsx")));
    }

    [Fact]
    public void PreviewStatusAndStop_FormARealCliLifecycle()
    {
        File.WriteAllText(
            _workspace.File("sales.csv"),
            "Region,Revenue\nEast,1200\nWest,900\n");
        _previewRequested = true;
        CliResult started = _workspace.RunWithEnv(
            new Dictionary<string, string?>
            {
                ["ASPOSE_CLI_NO_OPEN"] = "1",
            },
            "preview", "open", "sales.csv", "--no-open", "--output-format", "json");

        Assert.True(started.ExitCode == 0, started.StdErr);
        JsonNode json = JsonNode.Parse(started.StdOut)!;
        string id = json["id"]!.GetValue<string>();
        RememberService(json);
        Assert.Equal("cells", json["product"]!.GetValue<string>());

        CliResult status = _workspace.Run(
            "preview", "status", id, "--output-format", "json");
        Assert.True(status.ExitCode == 0, status.StdErr);
        Assert.Contains(id, status.StdOut, StringComparison.Ordinal);

        CliResult stopped = _workspace.Run(
            "preview", "stop", id, "--output-format", "json");
        Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        Assert.Contains(id, stopped.StdOut, StringComparison.Ordinal);
        Assert.Empty(JsonNode.Parse(stopped.StdOut)!["sessions"]!.AsArray());
    }

    [Fact]
    public void PreviewSheetsView_UsesTheRootLifecycle()
    {
        Assert.Equal(0, _workspace.Run(
            "cells", "create", "preview-sheet.xlsx", "--sheets", "Data").ExitCode);
        CliResult seeded = _workspace.Run(
            "cells", "edit", "preview-sheet.xlsx", "--ops",
            "{\"ops\":[{\"op\":\"set_values\",\"sheet\":\"Data\",\"range\":\"A1\",\"values\":[[\"value\"]]}]}",
            "--in-place", "--output-format", "json");
        Assert.True(seeded.ExitCode == 0, seeded.StdErr);

        _previewRequested = true;
        CliResult started = _workspace.Run(
            "preview", "open", "preview-sheet.xlsx", "--no-open", "--view", "sheets",
            "--port", "0", "--output-format", "json");

        Assert.True(started.ExitCode == 0, started.StdErr);
        JsonNode result = JsonNode.Parse(started.StdOut)!;
        string id = result["id"]!.GetValue<string>();
        RememberService(result);
        Assert.Equal("sheets", result["view"]!.GetValue<string>());

        CliResult stopped = _workspace.Run(
            "preview", "stop", id, "--output-format", "json");
        Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        Assert.Empty(JsonNode.Parse(stopped.StdOut)!["sessions"]!.AsArray());
    }

    private void RememberService(JsonNode started)
    {
        int pid = started["pid"]!.GetValue<int>();
        if (_previewServices.Any(process => process.Id == pid)) { return; }
        Process service = Process.GetProcessById(pid);
        _ = service.Handle;
        _previewServices.Add(service);
    }

    public void Dispose()
    {
        try
        {
            if (_previewRequested)
            {
                try
                {
                    CliResult stopped = _workspace.Run("preview", "stop", "--all", "--output-format", "json");
                    Assert.True(stopped.ExitCode == 0, stopped.StdErr);
                }
                finally
                {
                    // Closing a document keeps the service and its worker warm.
                    // End this fixture's service before removing its configuration.
                    foreach (Process service in _previewServices)
                    {
                        if (!service.WaitForExit(10_000))
                        {
                            service.Kill(entireProcessTree: true);
                            Assert.True(service.WaitForExit(5_000), "The fixture's viewer service did not exit.");
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (Process service in _previewServices) { service.Dispose(); }
            _workspace.Dispose();
        }
    }
}
