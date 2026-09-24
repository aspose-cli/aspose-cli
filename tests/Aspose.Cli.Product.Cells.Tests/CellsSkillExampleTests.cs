using Aspose.Cells;
using Aspose.Cells.Tables;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Every bundled Cells example runs as written and produces what its README promises.</summary>
[Category(TestCategory.Slow)]
public sealed class CellsSkillExampleTests
{
    private const string Skill = "aspose-cli-cells";

    [Fact]
    public void EditExistingSafely_KeepsTheBackupAndRecalculatesTheEdit()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, Skill, "edit-existing-safely").Directory;

        using var edited = new Workbook(Path.Combine(directory, "quarterly.xlsx"));
        Aspose.Cells.Cells q3 = edited.Worksheets["Q3"].Cells;
        Assert.Equal((1240d, 44.5d, 500d), (q3["B1"].DoubleValue, q3["B2"].DoubleValue, q3["B3"].DoubleValue));
        Assert.Equal("=B1*B2-B3", q3["B4"].Formula);
        Assert.Equal(54680d, q3["B4"].DoubleValue);
        using var backup = new Workbook(Path.Combine(directory, "quarterly.backup.xlsx"));
        Assert.Equal(1180d, backup.Worksheets["Q3"].Cells["B1"].DoubleValue);
        Assert.True(File.Exists(Path.Combine(directory, "quarterly.review/review.json")));
    }

    [Fact]
    public void RecalcAndVerify_RecalculatesTheProjection()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, Skill, "recalc-and-verify").Directory;

        using var model = new Workbook(Path.Combine(directory, "model.xlsx"));
        Aspose.Cells.Cells cells = model.Worksheets["Model"].Cells;
        Assert.Equal("=B1*(1+B2)", cells["B3"].Formula);
        Assert.Equal(125000d, cells["B3"].DoubleValue, 6);
        Assert.True(File.Exists(Path.Combine(directory, "model.review/review.json")));
    }

    [Fact]
    public void ReportFromCsv_BuildsTotalsAChartAndAPdf()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, Skill, "report-from-csv").Directory;

        using var report = new Workbook(Path.Combine(directory, "report.xlsx"));
        Worksheet sales = report.Worksheets["sales"];
        Assert.Equal("=SUM(B2:D2)", sales.Cells["E2"].Formula);
        Assert.Equal(4450d, sales.Cells["E2"].DoubleValue);
        Assert.Equal("Total", sales.Cells["A6"].StringValue);
        Assert.True(sales.Cells["A1"].GetStyle().Font.IsBold);
        Assert.Equal("Quarterly Sales", Assert.Single(sales.Charts).Title.Text);
        Assert.True(new FileInfo(Path.Combine(directory, "report.pdf")).Length > 0);
    }

    [Fact]
    public void SalesDashboard_DeliversTheMeasuredOutcome()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, Skill, "sales-dashboard").Directory;

        using var dashboard = new Workbook(Path.Combine(directory, "dashboard.xlsx"));
        Worksheet summary = dashboard.Worksheets["Dashboard"];
        Worksheet data = dashboard.Worksheets["Data"];
        Assert.True(summary.Index < data.Index, "Dashboard is the first deliverable sheet.");
        // An evaluation save activates the engine's own warning sheet.
        Worksheet active = dashboard.Worksheets[dashboard.Worksheets.ActiveSheetIndex];
        Assert.True(active.Name == "Dashboard" || active.Name.StartsWith("Evaluation Warning", StringComparison.Ordinal), active.Name);
        Assert.Equal(156460d, summary.Cells["B5"].DoubleValue);
        Assert.Equal(36d, summary.Cells["E5"].DoubleValue);
        Assert.Equal("North", summary.Cells["K5"].StringValue);
        Assert.Equal(2, summary.Charts.Count);
        Assert.Equal(2, summary.SparklineGroups.Count);
        ListObject orders = Assert.Single(data.ListObjects);
        Assert.Equal(("Orders", TableStyleType.TableStyleMedium2), (orders.DisplayName, orders.TableStyleType));
        Assert.Equal("$1:$1", data.PageSetup.PrintTitleRows);
        Assert.True(File.Exists(Path.Combine(directory, "scratch/dash-2.png")));
        Assert.True(new FileInfo(Path.Combine(directory, "dashboard.pdf")).Length > 0);
    }
}
