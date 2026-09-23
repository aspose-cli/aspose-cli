using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesPartialChartTests
{
    [Theory]
    [InlineData("column")]
    [InlineData("line")]
    public void CategoriesOnlyUpdate_PreservesExistingSeriesCoordinates(string kind)
    {
        using var fixture = new SlidesEngineFixture();
        string seed = CreateChart(fixture, kind);
        long id = ChartId(seed);
        string output = fixture.File("categories.pptx");
        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops = [new UpdateChartDataOp { Slide = 1, Shape = id, Categories = ["C", "D"] }],
        }, new PresentationEditRequest { OutputPath = output });
        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        Assert.Equal(["C", "D"], chart.ChartData.Categories.Select(category => category.AsCell.Value.ToString()!));
        Assert.Equal(["One", "Two"], chart.ChartData.Series.Select(series => series.Name.AsCells[0].Value.ToString()));
        Assert.Equal([10d, 20d], Values(chart.ChartData.Series[0], scatter: false));
        Assert.Equal([30d, 40d], Values(chart.ChartData.Series[1], scatter: false));
    }

    [Fact]
    public void ScatterCategoriesUpdate_IsRejectedBecauseScatterChartsHaveNoCategories()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = CreateChart(fixture, "scatter");
        string output = fixture.File("scatter-categories.pptx");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops = [new UpdateChartDataOp { Slide = 1, Shape = ChartId(seed), Categories = ["C", "D"] }],
        }, new PresentationEditRequest { OutputPath = output }));

        Assert.Equal(SlidesDiagnostics.ChartDataInvalid, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ScatterSeriesOnlyUpdate_PreservesCategoriesAndReplacesBothCoordinates()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = CreateChart(fixture, "scatter");
        string output = fixture.File("series.pptx");
        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new UpdateChartDataOp
                {
                    Slide = 1, Shape = ChartId(seed),
                    Series = [new SlidesChartSeriesInput { Name = "New", Values = [50, 60], XValues = [5, 6] }],
                },
            ],
        }, new PresentationEditRequest { OutputPath = output });
        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        Assert.Equal(["A", "B"], RowLabels(chart));
        Assert.Single(chart.ChartData.Series);
        Assert.Equal([50d, 60d], Values(chart.ChartData.Series[0], scatter: true));
        Assert.Equal([5d, 6d], XValues(chart.ChartData.Series[0]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitIncompleteScatterSeries_RemainsInvalid(bool bestEffort)
    {
        using var fixture = new SlidesEngineFixture();
        string seed = CreateChart(fixture, "scatter");
        byte[] original = File.ReadAllBytes(seed);
        string output = fixture.File("invalid.pptx");
        var batch = new SlidesOpsBatch
        {
            Ops =
            [
                new UpdateChartDataOp
                {
                    Slide = 1, Shape = ChartId(seed),
                    Series = [new SlidesChartSeriesInput { Name = "Bad", Values = [50, 60] }],
                },
            ],
        };
        var request = new PresentationEditRequest { OutputPath = output, Options = new EditCommandOptions { BestEffort = bestEffort } };
        if (bestEffort)
        {
            SlidesEditResult result = fixture.Engine.ApplyOps(seed, batch, request);
            Assert.Equal("failed", Assert.Single(result.Applied).Status);
            using var reopened = new Presentation(output);
            Assert.Equal([10d, 20d], Values(Chart(reopened).ChartData.Series[0], scatter: true));
            Assert.Equal([1d, 2d], XValues(Chart(reopened).ChartData.Series[0]));
        }
        else
        {
            Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(seed, batch, request));
            Assert.False(File.Exists(output));
        }
        Assert.Equal(original, File.ReadAllBytes(seed));
    }

    private static string CreateChart(SlidesEngineFixture fixture, string kind)
    {
        string input = fixture.CreatePresentation(slides: 1);
        string output = fixture.File("chart.pptx");
        fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops =
            [
                new InsertChartOp
                {
                    Slide = 1, Kind = kind,
                    Rect = new SlidesRectInput { X = 50, Y = 120, Width = 500, Height = 250 },
                    Categories = ["A", "B"],
                    Series =
                    [
                        new SlidesChartSeriesInput { Name = "One", Values = [10, 20], XValues = kind == "scatter" ? [1, 2] : null },
                        new SlidesChartSeriesInput { Name = "Two", Values = [30, 40], XValues = kind == "scatter" ? [3, 4] : null },
                    ],
                },
            ],
        }, new PresentationEditRequest { OutputPath = output });
        return output;
    }

    private static string[] RowLabels(IChart chart) => Enumerable.Range(1, 2)
        .Select(row => chart.ChartData.ChartDataWorkbook.GetCell(0, row, 0).Value?.ToString() ?? string.Empty).ToArray();
    private static IChart Chart(Presentation presentation) => presentation.Slides[0].Shapes.OfType<IChart>().Single();
    private static long ChartId(string path)
    {
        using var presentation = new Presentation(path);
        return Chart(presentation).OfficeInteropShapeId;
    }
    private static double[] Values(IChartSeries series, bool scatter) => series.DataPoints
        .Select(point => Convert.ToDouble(scatter ? point.YValue.Data : point.Value.Data, CultureInfo.InvariantCulture)).ToArray();
    private static double[] XValues(IChartSeries series) => series.DataPoints
        .Select(point => Convert.ToDouble(point.XValue.Data, CultureInfo.InvariantCulture)).ToArray();
}
