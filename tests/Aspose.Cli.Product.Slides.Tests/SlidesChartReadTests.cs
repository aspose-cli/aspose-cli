using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// <c>slides read --content full</c> reports what a chart stores under its own type: a bubble
/// series' y values, x values and sizes, a scatter series' y and x values, a category series'
/// values, and a series name whether it lives in a cell or is literal text. The CLI cannot author
/// a bubble chart or a literal series name, so the engine does. Evaluation mode reads text longer
/// than five characters cut short, so every text here is at most five characters long.
/// </summary>
public sealed class SlidesChartReadTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public SlidesChartReadTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A bubble point has no value: the read reports each point's y value as its value, with its
    /// x value and bubble size beside it, and leaves no other number for it.
    /// </summary>
    [Fact]
    public void BubbleChart_ReadsTheYValuesXValuesAndSizesItStores()
    {
        using var fixture = new SlidesEngineFixture();
        Deck(static shapes =>
        {
            IChart chart = shapes.AddChart(ChartType.Bubble, 40, 40, 400, 300, initWithSample: true);
            IChartDataPointCollection points = chart.ChartData.Series[0].DataPoints;
            (double X, double Y, double Size)[] stored = [(1.5, 20, 3), (2.5, 30, 6), (3.5, 40, 9)];
            for (int index = 0; index < stored.Length; index++)
            {
                points[index].XValue.AsCell.Value = stored[index].X;
                points[index].YValue.AsCell.Value = stored[index].Y;
                points[index].BubbleSize.AsCell.Value = stored[index].Size;
            }
        });

        JsonNode chart = ReadChart();
        JsonNode series = chart["series"]![0]!;

        Assert.Equal([20, 30, 40], Numbers(series["values"]));
        Assert.Equal([1.5, 2.5, 3.5], Numbers(series["xValues"]));
        Assert.Equal(chart["series"]!.AsArray().Count, chart["bubbleSizes"]!.AsArray().Count);
        Assert.Equal([3, 6, 9], Numbers(chart["bubbleSizes"]![0]));
    }

    /// <summary>
    /// A chart that combines a column series and a scatter series reads each by its own type, so
    /// the scatter series reports its y and x values and the column series its values alone.
    /// </summary>
    [Fact]
    public void AChartOfColumnAndScatterSeries_ReadsEachSeriesByItsOwnType()
    {
        using var fixture = new SlidesEngineFixture();
        Deck(static shapes =>
        {
            IChart chart = shapes.AddChart(ChartType.ClusteredColumn, 40, 40, 400, 300, initWithSample: true);
            IChartDataWorkbook book = chart.ChartData.ChartDataWorkbook;
            while (chart.ChartData.Series.Count > 1)
            {
                chart.ChartData.Series.RemoveAt(1);
            }

            IChartSeries scatter = chart.ChartData.Series.Add(book.GetCell(0, 0, 6, "XY"), ChartType.ScatterWithMarkers);
            scatter.DataPoints.AddDataPointForScatterSeries(book.GetCell(0, 1, 5, 1.5), book.GetCell(0, 1, 6, 25));
            scatter.DataPoints.AddDataPointForScatterSeries(book.GetCell(0, 2, 5, 2.5), book.GetCell(0, 2, 6, 35));
        });

        JsonNode chart = ReadChart();
        JsonArray series = chart["series"]!.AsArray();

        Assert.Null(series[0]!["xValues"]);
        Assert.Contains(Numbers(series[0]!["values"]), static value => value != 0);
        Assert.Equal([25, 35], Numbers(series[1]!["values"]));
        Assert.Equal([1.5, 2.5], Numbers(series[1]!["xValues"]));
        Assert.Null(chart["bubbleSizes"]);
    }

    /// <summary>A scatter series reads as before: its y values, its x values and no bubble sizes.</summary>
    [Fact]
    public void ScatterChart_ReadsItsYValuesAndXValuesWithoutBubbleSizes()
    {
        using var fixture = new SlidesEngineFixture();
        Deck(static shapes =>
        {
            IChart chart = shapes.AddChart(ChartType.ScatterWithStraightLinesAndMarkers, 40, 40, 400, 300, initWithSample: true);
            IChartDataPointCollection points = chart.ChartData.Series[0].DataPoints;
            points[0].XValue.AsCell.Value = 1.5;
            points[0].YValue.AsCell.Value = 20;
            points[1].XValue.AsCell.Value = 2.5;
            points[1].YValue.AsCell.Value = 30;
        });

        JsonNode chart = ReadChart();
        JsonNode series = chart["series"]![0]!;

        Assert.Equal("scatter", chart["kind"]!.GetValue<string>());
        Assert.Equal([20, 30], Numbers(series["values"]).Take(2));
        Assert.Equal([1.5, 2.5], Numbers(series["xValues"]).Take(2));
        Assert.Null(chart["bubbleSizes"]);
    }

    /// <summary>A category series reads as before: its values and neither x values nor bubble sizes.</summary>
    [Fact]
    public void ColumnChart_ReadsItsValuesWithoutXValuesOrBubbleSizes()
    {
        using var fixture = new SlidesEngineFixture();
        Deck(static shapes =>
        {
            IChart chart = shapes.AddChart(ChartType.ClusteredColumn, 40, 40, 400, 300, initWithSample: true);
            chart.ChartData.Categories[0].AsCell.Value = "Q1";
            chart.ChartData.Series[0].Name.AsCells[0].Value = "S1";
            chart.ChartData.Series[0].DataPoints[0].Value.AsCell.Value = 42;
        });

        JsonNode chart = ReadChart();
        JsonNode series = chart["series"]![0]!;

        Assert.Equal("column", chart["kind"]!.GetValue<string>());
        Assert.Equal("Q1", chart["categories"]![0]!.GetValue<string>());
        Assert.Equal("S1", series["name"]!.GetValue<string>());
        Assert.Equal(42, Numbers(series["values"])[0]);
        Assert.Null(series["xValues"]);
        Assert.Null(chart["bubbleSizes"]);
    }

    /// <summary>
    /// A series named by literal text reads that text, beside series named by their cells, where
    /// the read used to stop with FEATURE_UNSUPPORTED and exit 6.
    /// </summary>
    [Fact]
    public void ASeriesNamedByLiteralText_IsReadWithThatName()
    {
        using var fixture = new SlidesEngineFixture();
        Deck(static shapes => LiteralNamedChart(shapes, "Lit"));

        JsonArray series = ReadChart()["series"]!.AsArray();

        Assert.Equal(["S1", "Lit", "S3"], series.Select(static item => item!["name"]!.GetValue<string>()));
    }

    /// <summary>
    /// An edit reads the chart's state before and after the batch, so a deck with a literal series
    /// name can be edited, with its read-back, where the edit used to stop with
    /// FEATURE_UNSUPPORTED and exit 6; the literal name stays as it was.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADeckWithALiteralSeriesName_CanBeEdited(bool verify)
    {
        using var fixture = new SlidesEngineFixture();
        Deck(static shapes =>
        {
            shapes.AddAutoShape(ShapeType.Rectangle, 40, 340, 200, 60).TextFrame.Text = "Old";
            LiteralNamedChart(shapes, "Lit");
        });

        CliResult edit = verify
            ? _workspace.Run("slides", "edit", "deck.pptx", "--out", "out.pptx", "--verify", "--output-format", "json",
                "--ops", """[{"op":"replace_text","find":"Old","replace":"New"}]""")
            : _workspace.Run("slides", "edit", "deck.pptx", "--out", "out.pptx", "--output-format", "json",
                "--ops", """[{"op":"replace_text","find":"Old","replace":"New"}]""");

        Assert.True(edit.ExitCode == 0, edit.StdErr);
        using var output = new Presentation(_workspace.File("out.pptx"));
        IChart chart = output.Slides[0].Shapes.OfType<IChart>().Single();
        Assert.Equal(DataSourceType.StringLiterals, chart.ChartData.Series[1].Name.DataSourceType);
        Assert.Equal("Lit", chart.ChartData.Series[1].Name.AsLiteralString);
        Assert.Contains(output.Slides[0].Shapes.OfType<IAutoShape>(), static shape => shape.TextFrame?.Text == "New");
    }

    private void Deck(Action<IShapeCollection> add)
    {
        using var presentation = new Presentation();
        add(presentation.Slides[0].Shapes);
        presentation.Save(_workspace.File("deck.pptx"), SaveFormat.Pptx);
    }

    // A column chart whose second series is named by the literal text and the others by their cells.
    private static void LiteralNamedChart(IShapeCollection shapes, string name)
    {
        IChart chart = shapes.AddChart(ChartType.ClusteredColumn, 40, 40, 400, 300, initWithSample: true);
        chart.ChartData.Series[0].Name.AsCells[0].Value = "S1";
        chart.ChartData.Series[2].Name.AsCells[0].Value = "S3";
        IStringChartValue literal = chart.ChartData.Series[1].Name;
        literal.DataSourceType = DataSourceType.StringLiterals;
        literal.AsLiteralString = name;
    }

    private JsonNode ReadChart()
    {
        CliResult read = _workspace.Run("slides", "read", "deck.pptx", "--content", "full", "--output-format", "json");
        Assert.True(read.ExitCode == 0, read.StdErr);
        return JsonNode.Parse(read.StdOut)!["slides"]![0]!["shapes"]!.AsArray()
            .Single(static shape => shape!["kind"]!.GetValue<string>() == "chart")!["chart"]!;
    }

    private static double[] Numbers(JsonNode? values) =>
        [.. values!.AsArray().Select(static value => value!.GetValue<double>())];
}
