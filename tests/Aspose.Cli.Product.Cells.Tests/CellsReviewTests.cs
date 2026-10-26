using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Rendering;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

public sealed class CellsReviewTests
{
    [Fact]
    public void Review_ReportsLayoutProblemsOnlyThroughDeclaredChecks()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("layout.xlsx");
        CreateLayoutProblemWorkbook(input);
        var adapter = new CellsViewAdapter();
        var request = new ViewRenderRequest
        {
            View = CellsViews.Sheets,
            MaxPartCount = 10,
            Purpose = ViewPurpose.Evidence,
        };
        var sink = new MemoryArtifactSink();

        ViewManifest rendered = adapter.Render(fixture.Session, input, request, sink);
        ProductReviewAssessment assessment = adapter.Assess(fixture.Session, input, request, rendered);
        Assert.NotNull(assessment.Findings);
        IReadOnlyList<ReviewFinding> findings = assessment.Findings;
        // The formula error is a finding, not a check that failed to run, so review --code
        // can leave it out without the coverage turning incomplete.
        Assert.True(assessment.Complete);

        Dictionary<string, string> declared = adapter.Checks.ToDictionary(
            static check => check.Code,
            static check => check.Severity,
            StringComparer.Ordinal);
        Assert.All(findings, finding =>
            Assert.Equal(declared[finding.Code], finding.Severity));
        Assert.Equal(
            [
                "CELLS_CHART_ANCHORED_IN_HIDDEN_CELLS",
                "CELLS_CHART_HIDDEN",
                "CELLS_CHART_TOO_SMALL",
                "CELLS_CHART_WITHOUT_SERIES",
                "CELLS_FORMULA_ERROR",
                "CELLS_POPULATED_COLUMNS_HIDDEN",
                "CELLS_POPULATED_COLUMNS_NARROW",
                "CELLS_POPULATED_COLUMNS_WIDE",
                "CELLS_POPULATED_ROWS_HIDDEN",
                "CELLS_POPULATED_ROWS_SHORT",
                "CELLS_POPULATED_ROWS_TALL",
                "CELLS_PRINT_AREA_EXCLUDES_CHART",
                "CELLS_PRINT_AREA_EXCLUDES_CONTENT",
                "CELLS_SHEET_EMPTY",
                "CELLS_SHEET_HIDDEN",
                "CELLS_USED_RANGE_SPARSE",
                "CELLS_VALUES_CLIPPED",
            ],
            findings
                .Select(static finding => finding.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        // Each finding about one sheet names its image; the hidden sheet has none to name.
        Assert.All(findings, static finding => Assert.Equal(
            finding.Code switch
            {
                "CELLS_SHEET_EMPTY" => "Blank",
                "CELLS_SHEET_HIDDEN" => "Secret",
                _ => "Data",
            },
            Assert.Single(finding.Parts!)));
        ReviewFinding hiddenColumns = Assert.Single(
            findings,
            static finding => finding.Code == "CELLS_POPULATED_COLUMNS_HIDDEN");
        Assert.Equal("1 populated column(s) are hidden; sample: B.", hiddenColumns.Message);
        ReviewFinding hiddenRows = Assert.Single(
            findings,
            static finding => finding.Code == "CELLS_POPULATED_ROWS_HIDDEN");
        Assert.Equal("1 populated row(s) are hidden; sample: 2.", hiddenRows.Message);
    }

    /// <summary>
    /// A hidden sheet's finding gives the command that renders it on its own, not the advice to
    /// adjust a layout that every other finding gives.
    /// </summary>
    [Fact]
    public void Review_HiddenSheet_GivesTheCommandThatRendersIt()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("hidden.xlsx");
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Data";
            workbook.Worksheets[0].Cells["A1"].PutValue("Visible");
            Worksheet secret = workbook.Worksheets[workbook.Worksheets.Add()];
            secret.Name = "透视 Pivot";
            secret.Cells["A1"].PutValue("Hidden");
            secret.IsVisible = false;
            workbook.Save(input);
        }

        var adapter = new CellsViewAdapter();
        var request = new ViewRenderRequest { View = CellsViews.Sheets, MaxPartCount = 10, Purpose = ViewPurpose.Evidence };
        ViewManifest rendered = adapter.Render(fixture.Session, input, request, new MemoryArtifactSink());
        ProductReviewAssessment assessment = adapter.Assess(fixture.Session, input, request, rendered);

        ReviewFinding hidden = Assert.Single(assessment.Findings!, static finding => finding.Code == "CELLS_SHEET_HIDDEN");
        Assert.Contains($"aspose-cli cells render \"{input}\" --sheets 2", hidden.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("Adjust only", hidden.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_ClippedEastAsianText_AlsoSuggestsAnExplicitWidth()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("clipped-scripts.xlsx");
        using (var workbook = new Workbook())
        {
            // Two-character columns clip either text in any font, so no East Asian font is needed.
            Worksheet latin = workbook.Worksheets[0];
            latin.Name = "Latin";
            latin.Cells["A1"].PutValue("Accessories");
            latin.Cells["B1"].PutValue(10600);
            latin.Cells.SetColumnWidth(0, 2);
            Worksheet mixed = workbook.Worksheets[workbook.Worksheets.Add()];
            mixed.Name = "Mixed";
            mixed.Cells["A1"].PutValue("配件 Accessories");
            mixed.Cells["B1"].PutValue(10600);
            mixed.Cells.SetColumnWidth(0, 2);
            workbook.Save(input);
        }

        var adapter = new CellsViewAdapter();
        var request = new ViewRenderRequest { View = CellsViews.Sheets, MaxPartCount = 10, Purpose = ViewPurpose.Evidence };
        ViewManifest rendered = adapter.Render(fixture.Session, input, request, new MemoryArtifactSink());
        ProductReviewAssessment assessment = adapter.Assess(fixture.Session, input, request, rendered);

        Dictionary<string, string> clipped = assessment.Findings!
            .Where(static finding => finding.Code == "CELLS_VALUES_CLIPPED")
            .ToDictionary(static finding => finding.Location!, static finding => finding.Message, StringComparer.Ordinal);
        Assert.Equal(["sheet/Latin", "sheet/Mixed"], clipped.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("explicit width", clipped["sheet/Latin"], StringComparison.Ordinal);
        Assert.Contains("explicit width", clipped["sheet/Mixed"], StringComparison.Ordinal);
    }

    /// <summary>
    /// A formula error is located at its cell as a target address, with the sheet name escaped
    /// as an operation's target escapes it.
    /// </summary>
    [Fact]
    public void Review_LocatesAFormulaErrorAtItsCellOnASheetWhoseNameIsEscaped()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("formula-error.xlsx");
        using (var workbook = new Workbook())
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Net 5%!";
            sheet.Cells["A1"].Formula = "=1/0";
            workbook.CalculateFormula();
            workbook.Save(input);
        }

        ReviewFinding error = Assert.Single(Review(fixture, input), static finding => finding.Code == "CELLS_FORMULA_ERROR");

        Assert.Equal("cell/Net 5%25%21!A1", error.Location);
    }
    /// <summary>
    /// Text that spills over empty cells and ends close to a column edge, where page layout can
    /// cut its last character (known issue CELLS-OVERFLOW-EDGE), is listed for a look at the
    /// image; the same text ending far from an edge, and text the next cell cuts off, is not.
    /// </summary>
    [Fact]
    public void Review_ListsSpillingTextThatEndsNearAColumnEdge()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("overflow.xlsx");
        const string text = "查询条件：日期 2026-09-01 至 2026-09-30；部门：全部";
        using (var workbook = new Workbook())
        {
            Style normal = workbook.DefaultStyle;
            normal.Font.Name = "Microsoft YaHei";
            normal.Font.Size = 10;
            workbook.DefaultStyle = normal;
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Report";
            sheet.Cells["A1"].PutValue(text);
            sheet.Cells["A2"].PutValue(text);
            sheet.Cells["B2"].PutValue(1200);
            sheet.Cells["E1"].PutValue(text);
            foreach ((int column, double width) in new[] { (0, 14d), (1, 26d), (2, 12d), (4, 14d), (5, 60d) })
            {
                sheet.Cells.SetColumnWidth(column, width);
            }
            workbook.Save(input);
        }

        ReviewFinding overflow = Assert.Single(Review(fixture, input), static finding => finding.Code == "CELLS_TEXT_OVERFLOWS");

        Assert.Equal("sheet/Report", overflow.Location);
        Assert.StartsWith("1 text value(s) spill over empty cells to their right and end close to a column edge; sample: A1.", overflow.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A workbook an evaluation save marked keeps its warning sheets when a licensed review opens
    /// it; review reports each one, and nothing on an unmarked workbook.
    /// </summary>
    [Fact]
    public void Review_ReportsTheEvaluationWarningSheetsOfAMarkedWorkbook()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "marked.xlsx", "--sheet-names", "Data", "--license-mode", "evaluation").ExitCode);
        Assert.Equal(0, workspace.Run("cells", "edit", "marked.xlsx", "--set", "Data!A1=7", "--in-place", "--license-mode", "evaluation").ExitCode);
        string unmarked = fixture.CreateSalesWorkbook("unmarked.xlsx");

        Assert.Equal(
            ["sheet/Evaluation Warning", "sheet/Evaluation Warning (1)"],
            Review(fixture, workspace.File("marked.xlsx"))
                .Where(static finding => finding.Code == "CELLS_EVALUATION_SHEET")
                .Select(static finding => finding.Location!));
        Assert.DoesNotContain(Review(fixture, unmarked), static finding => finding.Code == "CELLS_EVALUATION_SHEET");
    }

    /// <summary>
    /// A CSV an evaluation export wrote ends with the evaluation notice row; review reports it, with
    /// or without a license, and nothing on a CSV without it.
    /// </summary>
    [Fact]
    public void Review_ReportsTheEvaluationNoticeRowOfAnExportedCsv()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        using var workspace = new TempWorkspace();
        string source = fixture.CreateSalesWorkbook("sales.xlsx");
        Assert.Equal(0, workspace.Run("cells", "convert", source, "--to", "csv", "--out", "marked.csv", "--license-mode", "evaluation").ExitCode);
        File.WriteAllText(workspace.File("plain.csv"), "Region,Q1\nEast,1200\nNote: Evaluation Only. Created with Aspose.Cells\n");

        ReviewFinding notice = Assert.Single(Review(fixture, workspace.File("marked.csv")), static finding => finding.Code == "CELLS_EVALUATION_NOTICE");
        Assert.Equal("row/marked!4", notice.Location);
        Assert.DoesNotContain(Review(fixture, workspace.File("plain.csv")), static finding => finding.Code == "CELLS_EVALUATION_NOTICE");
    }

    /// <summary>
    /// A date or number too wide for its column shows as #### or rounded, as the engine's display
    /// text says; review reports it like cut off text. A date that fits is not reported.
    /// </summary>
    [Fact]
    public void Review_ReportsADateThatShowsAsHashes()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("dates.xlsx");
        using (var workbook = new Workbook())
        {
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Dates";
            sheet.Cells["A2"].Formula = "=DATE(2026,3,1)";
            sheet.Cells["C2"].PutValue(new DateTime(2026, 3, 1));
            foreach (string cell in new[] { "A2", "C2" })
            {
                Style style = sheet.Cells[cell].GetStyle();
                style.Custom = "yyyy-mm-dd";
                sheet.Cells[cell].SetStyle(style);
            }
            sheet.Cells.SetColumnWidth(2, 14);
            workbook.CalculateFormula();
            workbook.Save(input);
        }

        ReviewFinding clipped = Assert.Single(Review(fixture, input), static finding => finding.Code == "CELLS_VALUES_CLIPPED");

        Assert.StartsWith("1 cell value(s) are wider than their columns; sample: A2.", clipped.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A print area of several areas draws one image per area, and a sheet's image holds the
    /// first: review and render say so, as incomplete evidence, with the command for each other area.
    /// </summary>
    [Fact]
    public void ASheetWithSeveralPrintAreas_SaysItsImageShowsTheFirstOnly()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "areas.xlsx", "--sheet-names", "Data").ExitCode);
        File.WriteAllText(workspace.File("ops.json"), """
            {"ops":[
              {"op":"set_values","sheet":"Data","range":"A1","values":[["Left",1],["Rows",2]]},
              {"op":"set_values","sheet":"Data","range":"E1","values":[["Right",3],["Rows",4]]},
              {"op":"set_print_area","sheet":"Data","range":"A1:B2,E1:F2"}
            ]}
            """);
        Assert.Equal(0, workspace.Run("cells", "edit", "areas.xlsx", "--ops", "ops.json", "--in-place").ExitCode);

        CliResult reviewed = workspace.Run("review", "areas.xlsx", "--out", "areas-review", "--output-format", "json");
        CliResult rendered = workspace.Run("cells", "render", "areas.xlsx", "--out", "areas.png", "--output-format", "json");

        // Incomplete evidence is review's own exit code.
        Assert.True(reviewed.ExitCode == 8, reviewed.StdErr);
        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        JsonNode review = JsonNode.Parse(reviewed.StdOut)!;
        Assert.False(review["coverage"]!["complete"]!.GetValue<bool>());
        foreach (JsonNode result in new[] { review, JsonNode.Parse(rendered.StdOut)! })
        {
            JsonNode warning = Assert.Single(result["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "SHEET_PARTIALLY_RENDERED")!;
            Assert.Contains("shows only the first, A1:B2", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("--range \"'Data'!E1:F2\"", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A render taller than an image viewer or a model reads in full warns RENDER_IMAGE_LARGE with
    /// a window of the sheet to render instead; a render of that window does not.
    /// </summary>
    [Fact]
    public void Render_WarnsWhenItsImageIsTooLargeToReadAndNamesAWindow()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "long.xlsx", "--sheet-names", "Data").ExitCode);
        string rows = string.Join(",", Enumerable.Range(1, 1200).Select(static row => $"[\"Item {row}\",{row}]"));
        File.WriteAllText(workspace.File("ops.json"), $$"""{"ops":[{"op":"set_values","sheet":"Data","range":"A1","values":[{{rows}}]}]}""");
        Assert.Equal(0, workspace.Run("cells", "edit", "long.xlsx", "--ops", "ops.json", "--in-place").ExitCode);

        CliResult whole = workspace.Run("cells", "render", "long.xlsx", "--sheets", "1", "--out", "long.png", "--output-format", "json");
        CliResult window = workspace.Run("cells", "render", "long.xlsx", "--range", "Data!A1:B200", "--out", "window.png", "--output-format", "json");

        Assert.True(whole.ExitCode == 0, whole.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(whole.StdOut)!["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "RENDER_IMAGE_LARGE")!;
        Assert.Contains("more than 10000 on a side", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("--range \"'Data'!A1:B", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(window.ExitCode == 0, window.StdErr);
        Assert.DoesNotContain("RENDER_IMAGE_LARGE", window.StdOut, StringComparison.Ordinal);
    }

    /// <summary>
    /// The warning sheets an evaluation save added are the evaluation's marks: an evaluation
    /// review leaves their layout unchecked and counts them; a licensed review checks every sheet.
    /// </summary>
    [Fact]
    public void Review_ExcludesEvaluationWarningSheetsFromLayoutChecksOnlyInEvaluationMode()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "marked.xlsx", "--sheet-names", "Data", "--license-mode", "evaluation").ExitCode);
        Assert.Equal(0, workspace.Run("cells", "edit", "marked.xlsx", "--set", "Data!A1=7", "--in-place", "--license-mode", "evaluation").ExitCode);

        JsonNode evaluation = ReviewJson(workspace, "evaluation", "--license-mode", "evaluation");

        Assert.Equal(2, Metric(evaluation, "excludedEvaluationMarks"));
        Assert.All(Findings(evaluation), static finding => Assert.Equal("CELLS_EVALUATION_SHEET", finding.Code));
        if (TestLicense.Path is not null)
        {
            JsonNode licensed = ReviewJson(workspace, "licensed", "--license", TestLicense.Path);
            Assert.Null(Metric(licensed, "excludedEvaluationMarks"));
            Assert.Contains(Findings(licensed), static finding => finding.Code != "CELLS_EVALUATION_SHEET" && finding.Location!.StartsWith("sheet/Evaluation Warning", StringComparison.Ordinal));
        }

        static JsonNode ReviewJson(TempWorkspace workspace, string name, params string[] mode)
        {
            CliResult result = workspace.Run(["review", "marked.xlsx", "--out", $"{name}-review", "--output-format", "json", .. mode]);
            Assert.True(result.ExitCode == 0, result.StdErr);
            return JsonNode.Parse(result.StdOut)!;
        }

        static long? Metric(JsonNode review, string name) =>
            review["coverage"]!["metrics"]!.AsArray().FirstOrDefault(metric => metric!["name"]!.GetValue<string>() == name)?["value"]!.GetValue<long>();

        static IEnumerable<(string Code, string? Location)> Findings(JsonNode review) =>
            review["findings"]!.AsArray().Select(static finding => (finding!["code"]!.GetValue<string>(), finding["location"]?.GetValue<string>()));
    }

    [Fact]
    public void ChartPages_DoNotCountTheCellAChartEndsExactlyAt()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        using var workbook = new Workbook();
        Worksheet sheet = workbook.Worksheets[0];
        sheet.Cells[150, 40].PutValue("far corner");
        CellArea first = sheet.GetPrintingPageBreaks(new ImageOrPrintOptions())[0];
        Chart flush = sheet.Charts[sheet.Charts.Add(ChartType.Column, 1, 1, first.EndRow + 1, first.EndColumn + 1)];
        Chart over = sheet.Charts[sheet.Charts.Add(ChartType.Column, 1, 1, first.EndRow + 1, first.EndColumn + 1)];
        over.ChartObject.LowerDeltaX = 512;
        over.ChartObject.LowerDeltaY = 128;
        Assert.Equal(0, flush.ChartObject.LowerDeltaX);
        Assert.Equal(0, flush.ChartObject.LowerDeltaY);

        IReadOnlyList<(Chart Chart, int Pages)> pages = PrintedPages.ChartPages(sheet);

        Assert.Equal(1, pages[0].Pages);
        Assert.Equal(4, pages[1].Pages);
    }

    /// <summary>
    /// A print area is excessive past both bounds its check states: 20 times the cells of the
    /// populated content and 1000 cells, so one cell printed with 1000 cells is not, and with
    /// 1010 is; the check's summary and the finding say both.
    /// </summary>
    [Fact]
    public void Review_ReportsAPrintAreaPastBothItsBounds()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();

        Assert.DoesNotContain(Review(fixture, OneCellPrinting(fixture, "A1:J100")),
            static finding => finding.Code == "CELLS_PRINT_AREA_EXCESSIVE");
        ReviewFinding excessive = Assert.Single(Review(fixture, OneCellPrinting(fixture, "A1:J101")),
            static finding => finding.Code == "CELLS_PRINT_AREA_EXCESSIVE");
        foreach (string text in new[] { CellsReviewChecks.PrintAreaExcessive.Summary, excessive.Message })
        {
            Assert.Contains("20 times", text, StringComparison.Ordinal);
            Assert.Contains("1000 cells", text, StringComparison.Ordinal);
        }
    }

    private static string OneCellPrinting(CellsFixture fixture, string printArea)
    {
        string path = fixture.Temp.File($"print-{printArea.Replace(':', '-')}.xlsx");
        using var workbook = new Workbook();
        Worksheet data = workbook.Worksheets[0];
        data.Cells["A1"].PutValue("Total");
        data.PageSetup.PrintArea = printArea;
        workbook.Save(path);
        return path;
    }

    /// <summary>
    /// A row too short for its text on a sheet that is not the active one gets a resize_rows
    /// example that names that sheet, since an operation without one acts on the active sheet.
    /// </summary>
    [Fact]
    public void Review_TextTallerThanRow_OnAnotherSheet_NamesThatSheetInTheExample()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("tall-title.xlsx");
        using (var workbook = new Workbook())
        {
            workbook.Worksheets[0].Name = "Cover";
            workbook.Worksheets[0].Cells["A1"].PutValue("Contents");
            Worksheet data = workbook.Worksheets[workbook.Worksheets.Add()];
            data.Name = "汇总 \"Q1\"";
            data.Cells["A1"].PutValue("2026 年销售汇总 Sales Summary");
            Style style = data.Cells["A1"].GetStyle();
            style.Font.Size = 28;
            data.Cells["A1"].SetStyle(style);
            workbook.Worksheets.ActiveSheetIndex = 0;
            workbook.Save(input);
        }

        ReviewFinding tall = Assert.Single(Review(fixture, input), static finding => finding.Code == "CELLS_TEXT_TALLER_THAN_ROW");

        Assert.Equal("row/汇总 \"Q1\"!1", tall.Location);
        Assert.Contains("""{"op":"resize_rows","sheet":"汇总 \"Q1\"","first":1}""", tall.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_ReportsAChartThatPrintingSplitsAcrossPages()
    {
        Requires.Windows();
        using var fixture = new CellsFixture();
        string input = fixture.Temp.File("split-chart.xlsx");
        CreateWideChartWorkbook(input, fitToOnePageWide: false);
        string fitted = fixture.Temp.File("fitted-chart.xlsx");
        CreateWideChartWorkbook(fitted, fitToOnePageWide: true);

        ReviewFinding split = Assert.Single(Review(fixture, input),
            static finding => finding.Code == "CELLS_CHART_SPLIT_ACROSS_PAGES");
        Assert.Equal("chart/Data/1", split.Location);
        Assert.Contains("2 printed pages", split.Message, StringComparison.Ordinal);
        Assert.Contains("fitToWidth", split.Hint, StringComparison.Ordinal);
        Assert.Contains("pageSize", split.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain(Review(fixture, fitted),
            static finding => finding.Code == "CELLS_CHART_SPLIT_ACROSS_PAGES");
    }

    /// <summary>A sheet whose chart spans columns D to Q, past the first portrait page, and a sheet of notes.</summary>
    internal static void CreateWideChartWorkbook(string path, bool fitToOnePageWide)
    {
        using var workbook = new Workbook();
        Worksheet data = workbook.Worksheets[0];
        data.Name = "Data";
        data.Cells["A1"].PutValue("Month");
        data.Cells["B1"].PutValue("Sales");
        for (int row = 1; row <= 4; row++)
        {
            data.Cells[row, 0].PutValue("M" + row);
            data.Cells[row, 1].PutValue(row * 10);
        }
        Chart chart = data.Charts[data.Charts.Add(ChartType.Column, 1, 3, 20, 16)];
        chart.Name = "Wide";
        chart.NSeries.Add("Data!B2:B5", true);
        if (fitToOnePageWide)
        {
            data.PageSetup.FitToPagesWide = 1;
            data.PageSetup.FitToPagesTall = 0;
        }
        workbook.Worksheets.Add("Notes").Cells["A1"].PutValue("Monthly sales");
        workbook.Save(path);
    }

    private static IReadOnlyList<ReviewFinding> Review(CellsFixture fixture, string input)
    {
        var adapter = new CellsViewAdapter();
        var request = new ViewRenderRequest { View = CellsViews.Sheets, MaxPartCount = 10, Purpose = ViewPurpose.Evidence };
        ViewManifest rendered = adapter.Render(fixture.Session, input, request, new MemoryArtifactSink());
        return adapter.Assess(fixture.Session, input, request, rendered).Findings!;
    }

    private static void CreateLayoutProblemWorkbook(string path)
    {
        using var workbook = new Workbook();
        Worksheet data = workbook.Worksheets[0];
        data.Name = "Data";
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 5; column++)
            {
                data.Cells[row, column].PutValue(row * 10 + column);
            }
        }
        data.Cells["F1"].Formula = "=1/0";
        data.Cells["Z200"].PutValue("far away");
        data.Cells.HideColumn(1);
        data.Cells.SetColumnWidth(2, 1);
        data.Cells.SetColumnWidth(4, 100);
        data.Cells.HideRow(1);
        data.Cells.SetRowHeight(2, 5);
        data.Cells.SetRowHeight(3, 150);
        data.PageSetup.PrintArea = "A1:A1";
        Chart chart = data.Charts[data.Charts.Add(ChartType.Column, 1, 7, 2, 8)];
        chart.ChartObject.IsHidden = true;

        workbook.Worksheets.Add("Blank");
        workbook.Worksheets.Add("Secret").IsVisible = false;
        workbook.CalculateFormula();
        workbook.Save(path);
    }
}
