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
        CliResult capabilities = _workspace.Run(
            "capabilities", "cells", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        Assert.Equal(
            ["cells", "cells compare", "cells convert", "cells create", "cells edit", "cells inspect", "cells query", "cells query range", "cells query search", "cells render"],
            JsonNode.Parse(capabilities.StdOut)!["products"]![0]!["commands"]!
                .AsArray()
                .Select(static command => command!["path"]!.GetValue<string>()));

        Assert.Equal(0, _workspace.Run(
            "cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);
        CliResult inspected = _workspace.Run(
            "cells", "inspect", "book.xlsx", "--output", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        string fingerprint = JsonNode.Parse(inspected.StdOut)!["source"]![
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
            "--if-match", fingerprint, "--in-place", "--output", "json");
        CliResult read = _workspace.Run(
            "cells", "query", "range", "book.xlsx", "--sheet", "Data",
            "--range", "B2:B3", "--output", "json");

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
            ["Data!A1:B2", "Data!B3"],
            editPayload["applied"]!.AsArray()
                .Select(static item => item!["targets"]![0]!.GetValue<string>()));
        JsonNode payload = JsonNode.Parse(read.StdOut)!;
        Assert.Equal(42, payload["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<double>());
        Assert.Equal(84, payload["sheet"]!["cells"]![1]![0]!["v"]!.GetValue<double>());
    }

    [Fact]
    public void QueryRange_ScansAnOverBudgetRegionThroughRunnableNextCommands()
    {
        File.WriteAllLines(
            _workspace.File("grid.csv"),
            Enumerable.Range(1, 10).Select(static row => $"{row},{row * 2},{row * 3}"));

        CliResult summary = _workspace.Run("cells", "query", "range", "grid.csv", "--max-cells", "10", "--output", "json");
        CliResult refused = _workspace.Run(
            "cells", "query", "range", "grid.csv", "--range", "A1:C10", "--max-cells", "10", "--output", "json");

        Assert.True(summary.ExitCode == 0, summary.StdErr);
        JsonNode summaryWindow = JsonNode.Parse(summary.StdOut)!["window"]!;
        Assert.Equal(("cell", 0, 30, true), (
            summaryWindow["unit"]!.GetValue<string>(),
            summaryWindow["returned"]!.GetValue<int>(),
            summaryWindow["total"]!.GetValue<int>(),
            summaryWindow["truncated"]!.GetValue<bool>()));
        string first = summaryWindow["next"]!.GetValue<string>();
        Assert.EndsWith(" --range A1:C3 --scan-range A1:C10 --scope values --max-cells 10 --output json", first, StringComparison.Ordinal);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("RANGE_TOO_LARGE", error["code"]!.GetValue<string>());
        Assert.Contains(" --range A1:C3 --scan-range A1:C10 ", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);

        CliResult second = _workspace.RunCommandLine(first);
        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode page = JsonNode.Parse(second.StdOut)!;
        Assert.Equal("A1:C3", page["sheet"]!["range"]!.GetValue<string>());
        Assert.Equal(9, page["window"]!["returned"]!.GetValue<int>());
        Assert.EndsWith(
            " --range A4:C6 --scan-range A1:C10 --scope values --max-cells 10 --output json",
            page["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);

        CliResult last = _workspace.Run(
            "cells", "query", "range", "grid.csv", "--range", "A10:C10", "--scan-range", "A1:C10", "--max-cells", "10", "--output", "json");
        Assert.True(last.ExitCode == 0, last.StdErr);
        JsonNode lastWindow = JsonNode.Parse(last.StdOut)!["window"]!;
        Assert.False(lastWindow["truncated"]!.GetValue<bool>());
        Assert.Null(lastWindow["next"]);
    }

    [Fact]
    public void QuerySearch_TruncatedWindowNextReturnsTheFollowingHits()
    {
        File.WriteAllLines(
            _workspace.File("hits.csv"),
            Enumerable.Range(1, 5).Select(static row => $"hit-{row},other"));

        CliResult first = _workspace.Run(
            "cells", "query", "search", "hits.csv", "--pattern", "HIT-", "--max-hits", "2", "--output", "json");

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
            "--best-effort", "--out", "edited.xlsx", "--output", "table");

        Assert.Equal(8, edited.ExitCode);
        Assert.Contains("1 of 2 op(s) applied, 1 failed", edited.StdOut, StringComparison.Ordinal);
        Assert.Contains("[op-0002/1] set_values: failed", edited.StdOut, StringComparison.Ordinal);
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
              { "op": "create_chart", "sheet": "Data", "type": "column", "dataRange": "A1:B3", "at": "E2:J12", "title": "Sales" }
            ] }
            """);
        CliResult edited = _workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place", "--output", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);

        CliResult inspected = _workspace.Run(
            "cells", "inspect", "book.xlsx", "--detail", "names", "errors", "charts", "--output", format);

        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        Assert.Contains("charts:", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("column", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("names:", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("SalesTotal", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("=Data!$B$2:$B$3", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("formula errors:", inspected.StdOut, StringComparison.Ordinal);
        Assert.Contains("#DIV/0!", inspected.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_AnUnknownFieldNamesTheAcceptedFieldsAndTheLikelyOne()
    {
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data").ExitCode);

        CliResult renamed = _workspace.Run(
            "cells", "edit", "book.xlsx", "--in-place", "--output", "json", "--ops",
            """{"ops":[{"op":"rename_sheet","sheet":"Data","name":"Sales"}]}""");
        CliResult sorted = _workspace.Run(
            "cells", "edit", "book.xlsx", "--in-place", "--output", "json", "--ops",
            """{"ops":[{"op":"sort_range","sheet":"Data","range":"A1:B4","by":[{"column":"A","direction":"desc"}]}]}""");

        Assert.Equal(4, renamed.ExitCode);
        JsonNode rename = JsonNode.Parse(renamed.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", rename["code"]!.GetValue<string>());
        Assert.Equal("unknown field 'name'; rename_sheet accepts: op, id, sheet, to (did you mean 'to'?)",
            rename["details"]!["reason"]!.GetValue<string>());
        Assert.Equal(["op", "id", "sheet", "to"],
            rename["details"]!["allowedFields"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal("to", rename["details"]!["suggestion"]!.GetValue<string>());
        JsonNode sort = JsonNode.Parse(sorted.StdErr)!["error"]!["details"]!;
        Assert.Equal("unknown field 'by[0].direction'; by[0] accepts: column, order",
            sort["reason"]!.GetValue<string>());
        Assert.Null(sort["suggestion"]);
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
            "--sheet", "Detail", "--out", "report.csv", "--overwrite", "--output", "json");
        CliResult converted = _workspace.Run("cells", "convert", "book.xlsx", "--to", "csv",
            "--out", "first.csv", "--output", "json");

        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("EVALUATION_LIMIT", error["code"]!.GetValue<string>());
        Assert.Equal("Dashboard", error["details"]!["firstSheet"]!.GetValue<string>());
        Assert.Contains("license", error["hint"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(refused.StdOut);
        Assert.Equal("existing report", File.ReadAllText(_workspace.File("report.csv")));
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonNode result = JsonNode.Parse(converted.StdOut)!;
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        JsonNode watermark = Assert.Single(result["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "EVAL_MODE")!;
        Assert.False(string.IsNullOrWhiteSpace(watermark["hint"]?.GetValue<string>()));
        // Without --sheet the first sheet is exported, and the evaluation notice becomes its last row.
        Assert.Contains("Only worksheet 'Dashboard' was exported", Assert.Single(result["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "SHEETS_DROPPED")!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("Evaluation Only.", File.ReadAllLines(_workspace.File("first.csv"))[^1], StringComparison.Ordinal);
        Assert.Contains("last row", Notice(result)["message"]!.GetValue<string>(), StringComparison.Ordinal);
        CliResult json = _workspace.Run("cells", "convert", "book.xlsx", "--to", "json", "--out", "book.json", "--output", "json");
        Assert.Contains("\"Evaluation Warning\"", File.ReadAllText(_workspace.File("book.json")), StringComparison.Ordinal);
        Assert.Contains("\"watermark\"", Notice(JsonNode.Parse(json.StdOut)!)["message"]!.GetValue<string>(), StringComparison.Ordinal);
        CliResult edited = _workspace.Run("cells", "edit", "book.xlsx", "--out", "edited.tsv", "--set", "Dashboard!B1=1", "--output", "json");
        Assert.StartsWith("Evaluation Only.", File.ReadAllLines(_workspace.File("edited.tsv"))[^1], StringComparison.Ordinal);
        _ = Notice(JsonNode.Parse(edited.StdOut)!);
        // A watermarked format carries the notice as a watermark, which EVAL_MODE already discloses.
        Assert.Null(CellsEvaluation.DescribeAddedNotice(Aspose.Cli.Sdk.Licensing.LicenseState.Evaluation, "pdf"));
    }

    [Fact]
    public void ConvertToPdf_WarnsAboutAChartItSplitsAcrossPages()
    {
        CellsReviewTests.CreateWideChartWorkbook(_workspace.File("split.xlsx"), fitToOnePageWide: false);
        CellsReviewTests.CreateWideChartWorkbook(_workspace.File("fitted.xlsx"), fitToOnePageWide: true);

        JsonNode split = _workspace.Run("cells", "convert", "split.xlsx", "--to", "pdf", "--out", "split.pdf", "--output", "json").Json();
        JsonNode fitted = _workspace.Run("cells", "convert", "fitted.xlsx", "--to", "pdf", "--out", "fitted.pdf", "--output", "json").Json();

        JsonNode warning = Assert.Single(split["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "CHART_SPLIT_ACROSS_PAGES")!;
        Assert.Equal("'Data'", warning["location"]!.GetValue<string>());
        Assert.Contains("'Wide' on sheet 'Data' (2 pages)", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(fitted["warnings"]?.AsArray() ?? [],
            static warning => warning!["code"]!.GetValue<string>() == "CHART_SPLIT_ACROSS_PAGES");
    }

    private static JsonNode Notice(JsonNode result) =>
        Assert.Single(result["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_NOTICE_ADDED")!;

    /// <summary>
    /// An evaluation save adds a warning sheet and activates it; the save says so, and later
    /// commands that default to the active sheet use the workbook's first sheet and say so, while
    /// commands that name their sheet or cover every sheet do not.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void Evaluation_TheAddedWarningSheetIsDisclosedAndSkippedByActiveSheetDefaults()
    {
        JsonNode created = Json(_workspace.Run("cells", "create", "book.xlsx", "--sheets", "One,Two", "--output", "json"));
        Assert.Equal("Evaluation Warning", Warning(created, "EVALUATION_SHEET_ADDED")["location"]!.GetValue<string>());
        File.WriteAllText(_workspace.File("ops.json"), """
            {"ops":[
              {"op":"set_values","sheet":"One","range":"A1","values":[["one"]]},
              {"op":"set_values","sheet":"Two","range":"A1","values":[["two"]]},
              {"op":"set_active_sheet","sheet":"Two"}]}
            """);

        JsonNode edited = Json(_workspace.Run("cells", "edit", "book.xlsx", "--ops", "ops.json", "--in-place", "--output", "json"));
        JsonNode added = Warning(edited, "EVALUATION_SHEET_ADDED");
        Assert.Equal("Evaluation Warning (1)", added["location"]!.GetValue<string>());
        Assert.Contains("in place of 'Two'", added["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // Every operation named its sheet, so nothing defaulted to the active sheet.
        AssertNoSkippedSheet(edited);

        JsonNode read = Json(_workspace.Run("cells", "query", "range", "book.xlsx", "--output", "json"));
        JsonNode rendered = Json(_workspace.Run("cells", "render", "book.xlsx", "--out", "book.png", "--output", "json"));
        JsonNode renderedAll = Json(_workspace.Run("cells", "render", "book.xlsx", "--all-sheets", "--out", "all.png", "--output", "json"));
        JsonNode csv = Json(_workspace.Run("cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv", "--output", "json"));
        JsonNode pdf = Json(_workspace.Run("cells", "convert", "book.xlsx", "--to", "pdf", "--out", "book.pdf", "--output", "json"));
        JsonNode chosen = Json(_workspace.Run("cells", "query", "range", "book.xlsx", "--sheet", "Two", "--output", "json"));
        JsonNode chosenPdf = Json(_workspace.Run("cells", "convert", "book.xlsx", "--to", "pdf", "--sheet", "Two", "--out", "two.pdf", "--output", "json"));

        Assert.Equal("One", read["sheet"]!["name"]!.GetValue<string>());
        JsonNode skipped = Warning(read, "EVALUATION_SHEET_SKIPPED");
        Assert.Equal("Evaluation Warning (1)", skipped["location"]!.GetValue<string>());
        Assert.Contains("uses 'One'", skipped["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("One", rendered["sheet"]!.GetValue<string>());
        _ = Warning(rendered, "EVALUATION_SHEET_SKIPPED");
        AssertNoSkippedSheet(renderedAll);
        Assert.StartsWith("one", File.ReadAllText(_workspace.File("book.csv")), StringComparison.Ordinal);
        _ = Warning(csv, "EVALUATION_SHEET_SKIPPED");
        _ = Warning(csv, "EVAL_MODE");
        AssertNoSkippedSheet(pdf);
        Assert.True(new FileInfo(_workspace.File("book.pdf")).Length > 0);
        Assert.Equal("Two", chosen["sheet"]!["name"]!.GetValue<string>());
        Assert.Equal("two", chosen["sheet"]!["cells"]![0]![0]!["v"]!.GetValue<string>());
        AssertNoSkippedSheet(chosen);
        Assert.Equal("Two", chosenPdf["sheet"]!.GetValue<string>());
        AssertNoSkippedSheet(chosenPdf);

        static JsonNode Json(CliResult result)
        {
            Assert.True(result.ExitCode == 0, result.StdErr);
            return JsonNode.Parse(result.StdOut)!;
        }

        static JsonNode Warning(JsonNode result, string code) =>
            Assert.Single(result["warnings"]!.AsArray(), warning => warning!["code"]!.GetValue<string>() == code)!;

        static void AssertNoSkippedSheet(JsonNode result) =>
            Assert.DoesNotContain(result["warnings"]?.AsArray() ?? [],
                static warning => warning!["code"]!.GetValue<string>() == "EVALUATION_SHEET_SKIPPED");
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
            "--encrypt-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");
        CliResult fromEnvironment = _workspace.RunWithEnv(
            variables,
            "cells", "inspect", "secret.xlsx",
            "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");
        CliResult fromStdin = _workspace.RunWithInput(
            Secret + Environment.NewLine,
            "cells", "inspect", "secret.xlsx", "--password-stdin", "--output", "json");
        CliResult withoutPassword = _workspace.Run(
            "cells", "inspect", "secret.xlsx", "--output", "json");

        Assert.Equal(0, created.ExitCode);
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
    public void Convert_EncryptsAProtectableOutputAndNamesTheOptionForAnyOther()
    {
        File.WriteAllText(_workspace.File("sales.csv"), "Region,Revenue\nEast,1200\n");
        var variables = new Dictionary<string, string?> { ["ASPOSE_CLI_TEST_PASSWORD"] = Secret };

        CliResult converted = _workspace.RunWithEnv(
            variables,
            "cells", "convert", "sales.csv", "--to", "xlsx", "--encrypt-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");
        CliResult locked = _workspace.Run("cells", "inspect", "sales.xlsx", "--output", "json");
        CliResult opened = _workspace.RunWithEnv(
            variables, "cells", "inspect", "sales.xlsx", "--password-env", "ASPOSE_CLI_TEST_PASSWORD", "--output", "json");
        CliResult refused = _workspace.Run(
            "cells", "convert", "sales.csv", "--to", "pdf", "--encrypt-env", "ASPOSE_CLI_TEST_UNSET", "--output", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(opened.ExitCode == 0, opened.StdErr);
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
            "cells", "edit", "images.xlsx", "--out", "images.out.xlsx", "--best-effort", "--output", "json",
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
            "preview", "sales.csv", "--output", "json");

        Assert.True(started.ExitCode == 0, started.StdErr);
        JsonNode json = JsonNode.Parse(started.StdOut)!;
        string id = json["id"]!.GetValue<string>();
        RememberService(json);
        Assert.Equal("cells", json["product"]!.GetValue<string>());

        CliResult status = _workspace.Run(
            "preview", "status", id, "--output", "json");
        Assert.True(status.ExitCode == 0, status.StdErr);
        Assert.Contains(id, status.StdOut, StringComparison.Ordinal);

        CliResult stopped = _workspace.Run(
            "preview", "stop", id, "--output", "json");
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
            "--in-place", "--output", "json");
        Assert.True(seeded.ExitCode == 0, seeded.StdErr);

        _previewRequested = true;
        CliResult started = _workspace.Run(
            "preview", "preview-sheet.xlsx", "--view", "sheets",
            "--port", "0", "--output", "json");

        Assert.True(started.ExitCode == 0, started.StdErr);
        JsonNode result = JsonNode.Parse(started.StdOut)!;
        string id = result["id"]!.GetValue<string>();
        RememberService(result);
        Assert.Equal("sheets", result["view"]!.GetValue<string>());

        CliResult stopped = _workspace.Run(
            "preview", "stop", id, "--output", "json");
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
                    CliResult stopped = _workspace.Run("preview", "stop", "--all", "--output", "json");
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
