using Aspose.Cells;
using Aspose.Cells.Charts;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>The Cells Skill's example, run as its README writes it, from the installed Skill.</summary>
[Category(TestCategory.Slow)]
public sealed class CellsSkillExampleTests
{
    [Fact]
    public void ReportExample_WritesTheTableItsTotalsTheChartAndThePdf()
    {
        using var workspace = new TempWorkspace();
        string directory = InstalledSkillExample.Run(workspace, "aspose-cli-cells", "build-a-report").Directory;

        using var workbook = new Workbook(Path.Combine(directory, "report.2.xlsx"));
        Worksheet sales = workbook.Worksheets["Sales"];
        Assert.Equal("=SUM(B2:E2)", sales.Cells["F2"].Formula);
        Assert.Equal(5515, sales.Cells["F2"].DoubleValue);
        Assert.True(sales.Cells["A1"].GetStyle().Font.IsBold);
        Chart chart = Assert.Single(Enumerable.Range(0, sales.Charts.Count).Select(index => sales.Charts[index]));
        Assert.Equal("Sales by quarter", chart.Title.Text);
        Assert.Equal(4, chart.NSeries.Count);
        Assert.True(new FileInfo(Path.Combine(directory, "report.pdf")).Length > 0);
    }
}
