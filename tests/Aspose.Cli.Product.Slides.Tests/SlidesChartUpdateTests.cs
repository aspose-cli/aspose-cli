using System.Drawing;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>update_chart_data changes data in place and keeps what the author styled.</summary>
public sealed class SlidesChartUpdateTests
{
    [Fact]
    public void CategoryChartUpdate_KeepsSeriesFormattingWhileGrowingAndShrinking()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = SeedChart(fixture, ChartType.ClusteredColumn, chart =>
        {
            IChartSeries first = chart.ChartData.Series[0];
            first.Format.Fill.FillType = FillType.Solid;
            first.Format.Fill.SolidFillColor.Color = Color.FromArgb(255, 200, 0, 0);
            first.Labels.DefaultDataLabelFormat.ShowValue = true;
        });
        string output = fixture.File("updated.pptx");

        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new UpdateChartDataOp
                {
                    Slide = 1,
                    ShapeId = ChartId(seed),
                    Categories = ["N", "S", "E", "W", "C"],
                    Series = [new SlidesChartSeriesInput { Name = "Rev", Values = [5, 4, 3, 2, 1] }],
                },
            ],
        }, new PresentationEditRequest { OutputPath = output });

        using var deck = new Presentation(output);
        IChart chart = Chart(deck);
        IChartSeries series = Assert.Single(chart.ChartData.Series);
        Assert.Equal("Rev", series.Name.AsCells[0].Value.ToString());
        Assert.Equal(["N", "S", "E", "W", "C"], chart.ChartData.Categories.Select(static item => item.AsCell.Value.ToString()));
        Assert.Equal([5d, 4d, 3d, 2d, 1d], series.DataPoints.Select(static point => Number(point.Value.Data)));
        Assert.Equal(FillType.Solid, series.Format.Fill.FillType);
        Assert.Equal(Color.FromArgb(255, 200, 0, 0).ToArgb(), series.Format.Fill.SolidFillColor.Color.ToArgb());
        Assert.True(series.Labels.DefaultDataLabelFormat.ShowValue);
    }

    [Fact]
    public void LineChartUpdate_KeepsMarkersAndAddsASeriesBesideTheExistingOnes()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = SeedChart(fixture, ChartType.LineWithMarkers, chart =>
        {
            chart.ChartData.Series[0].Marker.Symbol = MarkerStyleType.Diamond;
            chart.ChartData.Series[0].Marker.Size = 11;
        });
        string output = fixture.File("line.pptx");

        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new UpdateChartDataOp
                {
                    Slide = 1,
                    ShapeId = ChartId(seed),
                    Series =
                    [
                        new SlidesChartSeriesInput { Name = "One", Values = [7, 8, 9] },
                        new SlidesChartSeriesInput { Name = "Two", Values = [1, 2, 3] },
                        new SlidesChartSeriesInput { Name = "Three", Values = [4, 5, 6] },
                    ],
                },
            ],
        }, new PresentationEditRequest { OutputPath = output });

        using var deck = new Presentation(output);
        IChart chart = Chart(deck);
        Assert.Equal(3, chart.ChartData.Series.Count);
        Assert.Equal(MarkerStyleType.Diamond, chart.ChartData.Series[0].Marker.Symbol);
        Assert.Equal(11, chart.ChartData.Series[0].Marker.Size);
        Assert.Equal([7d, 8d, 9d], chart.ChartData.Series[0].DataPoints.Select(static point => Number(point.Value.Data)));
        Assert.Equal([4d, 5d, 6d], chart.ChartData.Series[2].DataPoints.Select(static point => Number(point.Value.Data)));
        Assert.Equal("Three", chart.ChartData.Series[2].Name.AsCells[0].Value.ToString());
        Assert.All(chart.ChartData.Series, static series => Assert.Equal(ChartType.LineWithMarkers, series.Type));
    }

    [Fact]
    public void ScatterUpdate_KeepsMarkersAndExtendsTheSeriesCells()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 1);
        string seed = fixture.File("scatter.pptx");
        fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops =
            [
                new InsertChartOp
                {
                    Slide = 1, Kind = "scatter",
                    Rect = new SlidesRectInput { X = 50, Y = 100, Width = 500, Height = 250 },
                    Categories = ["A", "B"],
                    Series = [new SlidesChartSeriesInput { Name = "One", Values = [10, 20], XValues = [1, 2] }],
                },
            ],
        }, new PresentationEditRequest { OutputPath = seed });
        using (var styled = new Presentation(seed))
        {
            Chart(styled).ChartData.Series[0].Marker.Symbol = MarkerStyleType.Triangle;
            styled.Save(seed, SaveFormat.Pptx);
        }

        string output = fixture.File("scatter.updated.pptx");
        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new UpdateChartDataOp
                {
                    Slide = 1, ShapeId = ChartId(seed),
                    Series = [new SlidesChartSeriesInput { Name = "One", Values = [11, 21, 31], XValues = [1.5, 2.5, 3.5] }],
                },
            ],
        }, new PresentationEditRequest { OutputPath = output });

        using var deck = new Presentation(output);
        IChartSeries series = Assert.Single(Chart(deck).ChartData.Series);
        Assert.Equal(MarkerStyleType.Triangle, series.Marker.Symbol);
        Assert.Equal([1.5, 2.5, 3.5], series.DataPoints.Select(static point => Number(point.XValue.Data)));
        Assert.Equal([11d, 21d, 31d], series.DataPoints.Select(static point => Number(point.YValue.Data)));
    }

    [Fact]
    public void UnsupportedChartType_IsRejectedWithoutChangingTheDeck()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = SeedChart(fixture, ChartType.Bubble, static _ => { });
        byte[] original = File.ReadAllBytes(seed);
        string output = fixture.File("bubble.pptx");

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new UpdateChartDataOp
                {
                    Slide = 1, ShapeId = ChartId(seed),
                    Series = [new SlidesChartSeriesInput { Name = "One", Values = [1, 2, 3], XValues = [1, 2, 3] }],
                },
            ],
        }, new PresentationEditRequest { OutputPath = output }));

        Assert.Equal(SlidesDiagnostics.ChartDataInvalid, error.Code);
        Assert.Contains("insert_chart", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.Equal(original, File.ReadAllBytes(seed));
    }

    // A chart authored with the engine's own sample data, like one made in PowerPoint:
    // three categories and three series, laid out with series in columns.
    private static string SeedChart(SlidesEngineFixture fixture, ChartType type, Action<IChart> style)
    {
        string path = fixture.File($"seed-{type}.pptx");
        using var presentation = new Presentation();
        IChart chart = presentation.Slides[0].Shapes.AddChart(type, 40, 40, 500, 300, initWithSample: true);
        if (!ChartTypeCharacterizer.IsChartTypeBubble(type))
        {
            while (chart.ChartData.Categories.Count > 3)
            {
                chart.ChartData.Categories.RemoveAt(chart.ChartData.Categories.Count - 1);
                foreach (IChartSeries series in chart.ChartData.Series)
                {
                    series.DataPoints.RemoveAt(series.DataPoints.Count - 1);
                }
            }

            chart.ChartData.Series[0].Name.AsCells[0].Value = "One";
        }

        style(chart);
        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }

    private static IChart Chart(Presentation presentation) =>
        presentation.Slides[0].Shapes.OfType<IChart>().Single();

    private static long ChartId(string path)
    {
        using var presentation = new Presentation(path);
        return Chart(presentation).OfficeInteropShapeId;
    }

    private static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
}
