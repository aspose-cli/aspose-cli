using Aspose.Cells;
using Aspose.Cells.Charts;
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
            MaxParts = 10,
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
            ],
            findings
                .Select(static finding => finding.Code)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        ReviewFinding hiddenColumns = Assert.Single(
            findings,
            static finding => finding.Code == "CELLS_POPULATED_COLUMNS_HIDDEN");
        Assert.Equal("1 populated column(s) are hidden; sample: B.", hiddenColumns.Message);
        ReviewFinding hiddenRows = Assert.Single(
            findings,
            static finding => finding.Code == "CELLS_POPULATED_ROWS_HIDDEN");
        Assert.Equal("1 populated row(s) are hidden; sample: 2.", hiddenRows.Message);
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
