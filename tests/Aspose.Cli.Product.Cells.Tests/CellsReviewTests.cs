using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Rendering;
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

        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, sink);
        ProductReviewAssessment assessment = adapter.Assess(fixture.Engine, input, request, rendered);
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
            finding.Part));
        ReviewFinding hiddenColumns = Assert.Single(
            findings,
            static finding => finding.Code == "CELLS_POPULATED_COLUMNS_HIDDEN");
        Assert.Equal("1 populated column(s) are hidden; sample: B.", hiddenColumns.Message);
        ReviewFinding hiddenRows = Assert.Single(
            findings,
            static finding => finding.Code == "CELLS_POPULATED_ROWS_HIDDEN");
        Assert.Equal("1 populated row(s) are hidden; sample: 2.", hiddenRows.Message);
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
        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, new MemoryArtifactSink());
        ProductReviewAssessment assessment = adapter.Assess(fixture.Engine, input, request, rendered);

        Dictionary<string, string> clipped = assessment.Findings!
            .Where(static finding => finding.Code == "CELLS_VALUES_CLIPPED")
            .ToDictionary(static finding => finding.Location!, static finding => finding.Message, StringComparer.Ordinal);
        Assert.Equal(["Latin", "Mixed"], clipped.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("explicit width", clipped["Latin"], StringComparison.Ordinal);
        Assert.Contains("explicit width", clipped["Mixed"], StringComparison.Ordinal);
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

        Assert.Equal("Report", overflow.Location);
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
        Assert.Equal(0, workspace.Run("cells", "create", "marked.xlsx", "--sheets", "Data", "--license-mode", "evaluation").ExitCode);
        Assert.Equal(0, workspace.Run("cells", "edit", "marked.xlsx", "--set", "Data!A1=7", "--in-place", "--license-mode", "evaluation").ExitCode);
        string unmarked = fixture.CreateSalesWorkbook("unmarked.xlsx");

        Assert.Equal(
            ["Evaluation Warning", "Evaluation Warning (1)"],
            Review(fixture, workspace.File("marked.xlsx"))
                .Where(static finding => finding.Code == "CELLS_EVALUATION_SHEET")
                .Select(static finding => finding.Location!));
        Assert.DoesNotContain(Review(fixture, unmarked), static finding => finding.Code == "CELLS_EVALUATION_SHEET");
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
        Assert.Equal("Data chart 'Wide'", split.Location);
        Assert.Contains("2 printed pages", split.Message, StringComparison.Ordinal);
        Assert.Contains("fitToWidth", split.Hint, StringComparison.Ordinal);
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
        ViewManifest rendered = adapter.Render(fixture.Engine, input, request, new MemoryArtifactSink());
        return adapter.Assess(fixture.Engine, input, request, rendered).Findings!;
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
