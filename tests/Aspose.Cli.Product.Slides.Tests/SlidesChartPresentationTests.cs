using System.IO.Compression;
using System.Xml.Linq;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The presentation a chart's own data dictates, against the real engine.
/// A bar or column encodes value as length, so these are correctness
/// properties of the drawing rather than styling preferences.
/// </summary>
public sealed class SlidesChartPresentationTests
{
    private static readonly XNamespace ChartXml = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    private static readonly SlidesRectInput Frame =
        new() { X = 50, Y = 105, Width = 620, Height = 245 };

    private static string Insert(
        SlidesEngineFixture fixture, string name, string kind, params double[] values)
    {
        string output = fixture.File(name);
        fixture.Engine.ApplyOps(
            fixture.CreatePresentation($"seed-{name}"),
            new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertChartOp
                    {
                        Slide = 1,
                        Kind = kind,
                        Rect = Frame,
                        Categories = [.. values.Select((_, index) => $"C{index + 1}")],
                        Series = [new SlidesChartSeriesInput { Name = "Amount", Values = values }],
                    },
                ],
            },
            new PresentationEditRequest { Output = TestOutput.At(output) });
        return output;
    }

    [Theory]
    [InlineData("column", 95, 100, 105)]
    [InlineData("bar", 0, 0, 0)]
    public void NonNegativeValues_UseTheValueAxisZeroBaseline(
        string kind, double first, double second, double third)
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "baseline.pptx", kind, first, second, third);

        Assert.Equal("0", ReadAxisScale(output, "valAx", "min"));
        Assert.Null(ReadAxisScale(output, "catAx", "min"));
        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        IAxis categories = kind == "bar" ? chart.Axes.VerticalAxis : chart.Axes.HorizontalAxis;
        Assert.Equal(CategoryAxisType.Text, categories.CategoryAxisType);
    }

    [Theory]
    [InlineData("column", 95, -100, 105)]
    [InlineData("bar", -95, -100, -105)]
    public void NegativeValues_KeepAutomaticValueAxisScaling(
        string kind, double first, double second, double third)
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "negative.pptx", kind, first, second, third);

        // A forced zero minimum would clip negative data.
        Assert.Null(ReadAxisScale(output, "valAx", "min"));
    }

    [Theory]
    [InlineData("column", 95, 100, 105)]
    [InlineData("bar", 0, 0, 0)]
    public void UpdatingAutomaticAxisToNonNegativeData_UsesZero(
        string kind, double first, double second, double third)
    {
        using var fixture = new SlidesEngineFixture();
        string seeded = Insert(fixture, "automatic-seed.pptx", kind, -95, 100, 105);
        Assert.Null(ReadAxisScale(seeded, "valAx", "min"));
        string output = fixture.File("updated-baseline.pptx");

        fixture.Engine.ApplyOps(seeded, new SlidesOpsBatch
        {
            Ops = [new UpdateChartDataOp
            {
                Slide = 1,
                ShapeId = FindChartShapeId(seeded),
                Series = [new SlidesChartSeriesInput { Name = "Amount", Values = [first, second, third] }],
            }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        Assert.Equal("0", ReadAxisScale(output, "valAx", "min"));
        Assert.Null(ReadAxisScale(output, "catAx", "min"));
    }

    [Theory]
    [InlineData("column")]
    [InlineData("bar")]
    public void DataUpdate_PreservesStoredZeroMinimumAndNegativeSeries(string kind)
    {
        using var fixture = new SlidesEngineFixture();
        string seeded = Insert(fixture, "fixed-zero-seed.pptx", kind, 95, 100, 105);
        Assert.Equal("0", ReadAxisScale(seeded, "valAx", "min"));
        string output = fixture.File("fixed-zero-updated.pptx");

        fixture.Engine.ApplyOps(seeded, new SlidesOpsBatch
        {
            Ops = [new UpdateChartDataOp
            {
                Slide = 1,
                ShapeId = FindChartShapeId(seeded),
                Series = [new SlidesChartSeriesInput { Name = "Amount", Values = [95, -100, 105] }],
            }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        // This verifies stored data and range preservation, not rendered visibility.
        // The fixed range must be reviewed separately when the new data falls outside it.
        Assert.Equal("0", ReadAxisScale(output, "valAx", "min"));
        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        Assert.Equal([95d, -100d, 105d], chart.ChartData.Series[0].DataPoints
            .Select(point => Convert.ToDouble(point.Value.Data, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Line_KeepsAutomaticScaling()
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "line.pptx", "line", 95, 100, 105);

        // A line encodes value by position, so a zoomed axis is legitimate.
        Assert.Null(ReadAxisScale(output, "valAx", "min"));
    }

    [Theory]
    [InlineData("column")]
    [InlineData("bar")]
    public void ValueAxis_KeepsTheRangeTheAuthorSetExplicitly(string kind)
    {
        using var fixture = new SlidesEngineFixture();
        string seeded = Insert(fixture, "explicit-seed.pptx", kind, 95, 100, 105);
        SetValueAxisRange(seeded, 50, 200);
        string output = fixture.File("explicit.pptx");

        fixture.Engine.ApplyOps(
            seeded,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new UpdateChartDataOp
                    {
                        Slide = 1,
                        ShapeId = FindChartShapeId(seeded),
                        Series = [new SlidesChartSeriesInput { Name = "Amount", Values = [96, 101, 106] }],
                    },
                ],
            },
            new PresentationEditRequest { Output = TestOutput.At(output) });

        Assert.Equal("50", ReadAxisScale(output, "valAx", "min"));
        Assert.Equal("200", ReadAxisScale(output, "valAx", "max"));
    }

    [Fact]
    public void GrowingAChartToASecondSeries_EnablesANonOverlayLegend()
    {
        using var fixture = new SlidesEngineFixture();
        string seeded = Insert(fixture, "legend-seed.pptx", "column", 95, 100, 105);
        Assert.False(HasLegend(seeded));
        string output = fixture.File("legend.pptx");

        fixture.Engine.ApplyOps(
            seeded,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new UpdateChartDataOp
                    {
                        Slide = 1,
                        ShapeId = FindChartShapeId(seeded),
                        Series =
                        [
                            new SlidesChartSeriesInput { Name = "Actual", Values = [95, 100, 105] },
                            new SlidesChartSeriesInput { Name = "Target", Values = [100, 110, 120] },
                        ],
                    },
                ],
            },
            new PresentationEditRequest { Output = TestOutput.At(output) });

        // Two series that cannot be told apart are unreadable.
        Assert.True(HasLegend(output));
        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        Assert.False(chart.Legend.Overlay);
        chart.ValidateChartLayout();
        AssertDoNotOverlap(chart.Legend, chart.PlotArea.AsIActualLayout);
    }

    [Theory]
    [InlineData("column", 330, true)]
    [InlineData("line", 330, true)]
    [InlineData("column", 640, false)]
    public void CategoryLabelsWithoutRoomBetweenThem_AreSlanted(string kind, double width, bool slanted)
    {
        // The renderer draws labels that just fit edge to edge, as one run of text.
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File($"{kind}-{width}.pptx");
        fixture.Engine.ApplyOps(fixture.CreatePresentation($"seed-{kind}-{width}.pptx"), new SlidesOpsBatch
        {
            Ops = [new InsertChartOp
            {
                Slide = 1,
                Kind = kind,
                Rect = new SlidesRectInput { X = 40, Y = 95, Width = width, Height = 270 },
                Categories = ["Q4 2025", "Q1 2026", "Q2 2026", "Q3 2026"],
                Series = [new SlidesChartSeriesInput { Name = "Revenue", Values = [10850, 11200, 11960, 12800] }],
            }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        using var reopened = new Presentation(output);
        float angle = Chart(reopened).Axes.HorizontalAxis.TextFormat.TextBlockFormat.RotationAngle;
        Assert.Equal(slanted, angle == -45);
    }

    [Fact]
    public void Pie_VariesSliceColorsAndNamesTheCategoriesInALegend()
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "pie.pptx", "pie", 45, 20, 60, 15);

        // A pie's categories show only through its slice colors and legend.
        Assert.Equal(
            "1",
            ReadChartPart(output).Descendants(ChartXml + "varyColors").Single().Attribute("val")?.Value);
        Assert.True(HasLegend(output));
        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        Assert.False(chart.Legend.Overlay);
        chart.ValidateChartLayout();
        AssertDoNotOverlap(chart.Legend, chart.PlotArea.AsIActualLayout);
    }

    [Theory]
    [InlineData("column")]
    [InlineData("bar")]
    public void NewlyAuthoredTitleAndLegend_ReserveSpaceOutsideThePlot(string kind)
    {
        using var fixture = new SlidesEngineFixture();
        string output = InsertTitledChart(fixture, "titled.pptx", kind);

        using var reopened = new Presentation(output);
        IChart chart = Chart(reopened);
        Assert.True(chart.HasTitle);
        Assert.True(chart.HasLegend);
        Assert.False(chart.ChartTitle.Overlay);
        Assert.False(chart.Legend.Overlay);
        chart.ValidateChartLayout();
        AssertDoNotOverlap(chart.ChartTitle, chart.PlotArea.AsIActualLayout);
        AssertDoNotOverlap(chart.Legend, chart.PlotArea.AsIActualLayout);
        AssertDoNotOverlap(chart.ChartTitle, chart.Legend);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdatingImportedChart_PreservesExplicitTitleAndLegendLayout(bool overlay)
    {
        using var fixture = new SlidesEngineFixture();
        string seeded = InsertTitledChart(fixture, "authored-layout.pptx", "column");
        using (var authored = new Presentation(seeded))
        {
            IChart chart = Chart(authored);
            chart.ChartTitle.Overlay = overlay;
            chart.Legend.Overlay = overlay;
            chart.Legend.Position = LegendPositionType.Right;
            authored.Save(seeded, Aspose.Slides.Export.SaveFormat.Pptx);
        }
        string output = fixture.File("preserved-layout.pptx");

        fixture.Engine.ApplyOps(seeded, new SlidesOpsBatch
        {
            Ops = [new UpdateChartDataOp
            {
                Slide = 1,
                ShapeId = FindChartShapeId(seeded),
                Series =
                [
                    new SlidesChartSeriesInput { Name = "Actual", Values = [40] },
                    new SlidesChartSeriesInput { Name = "Target", Values = [45] },
                ],
            }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        using var reopened = new Presentation(output);
        IChart result = Chart(reopened);
        Assert.Equal(overlay, result.ChartTitle.Overlay);
        Assert.Equal(overlay, result.Legend.Overlay);
        Assert.Equal(LegendPositionType.Right, result.Legend.Position);
    }

    [Fact]
    public void NewChart_TakesTheThemeTextColorThatContrastsWithItsSlide()
    {
        using var fixture = new SlidesEngineFixture();
        // The built-in design's title slide is dark; a content slide is light, and a slide
        // given its own dark background is dark whatever its layout.
        string seed = fixture.File("contrast-seed.pptx");
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(seed) });
        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new AddSlideOp { Layout = "Title and Content" },
                new AddSlideOp { Layout = "Title and Content" },
            ],
        }, new PresentationEditRequest { Output = TestOutput.At(seed, overwrite: true) });
        string output = fixture.File("contrast.pptx");

        fixture.Engine.ApplyOps(seed, new SlidesOpsBatch
        {
            Ops =
            [
                new SetBackgroundOp { Slides = "3", Color = "#1F2A44" },
                .. Enumerable.Range(1, 3).Select(static slide => new InsertChartOp
                {
                    Slide = slide,
                    Kind = "bar",
                    Rect = Frame,
                    Categories = ["Target", "Actual"],
                    Series = [new SlidesChartSeriesInput { Name = "Revenue", Values = [2.4, 1.82] }],
                }),
            ],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        // On the light slide the chart keeps its style's own text color.
        using var deck = new Presentation(output);
        Assert.Equal(
            [(FillType.Solid, SchemeColor.Background1), (FillType.NotDefined, SchemeColor.NotDefined), (FillType.Solid, SchemeColor.Background1)],
            deck.Slides.Select(static slide =>
            {
                IFillFormat fill = slide.Shapes.OfType<IChart>().Single().TextFormat.PortionFormat.FillFormat;
                return (fill.FillType, fill.FillType == FillType.Solid ? fill.SolidFillColor.SchemeColor : SchemeColor.NotDefined);
            }));
    }

    private static long FindChartShapeId(string presentationPath)
    {
        using var presentation = new Presentation(presentationPath);
        return (long)presentation.Slides[0].Shapes
            .First(static shape => shape is IChart).OfficeInteropShapeId;
    }

    private static void SetValueAxisRange(string presentationPath, double minimum, double maximum)
    {
        using var presentation = new Presentation(presentationPath);
        IChart chart = Chart(presentation);
        IAxis values = chart.Type == ChartType.ClusteredBar ? chart.Axes.HorizontalAxis : chart.Axes.VerticalAxis;
        values.IsAutomaticMinValue = false;
        values.MinValue = minimum;
        values.IsAutomaticMaxValue = false;
        values.MaxValue = maximum;
        presentation.Save(presentationPath, Aspose.Slides.Export.SaveFormat.Pptx);
    }

    private static string? ReadAxisScale(string presentationPath, string axis, string bound) =>
        ReadChartPart(presentationPath)
            .Descendants(ChartXml + axis)
            .Single()
            .Element(ChartXml + "scaling")?
            .Element(ChartXml + bound)?
            .Attribute("val")?.Value;

    private static IChart Chart(Presentation presentation) =>
        Assert.Single(presentation.Slides[0].Shapes.OfType<IChart>());

    private static string InsertTitledChart(SlidesEngineFixture fixture, string name, string kind)
    {
        string output = fixture.File(name);
        fixture.Engine.ApplyOps(fixture.CreatePresentation("seed-" + name), new SlidesOpsBatch
        {
            Ops = [new InsertChartOp
            {
                Slide = 1,
                Kind = kind,
                Rect = new SlidesRectInput { X = 40, Y = 106, Width = 305, Height = 238 },
                Categories = ["Service"],
                Series =
                [
                    new SlidesChartSeriesInput { Name = "Actual", Values = [36] },
                    new SlidesChartSeriesInput { Name = "Target", Values = [40] },
                ],
                Title = "Efficiency improvement (%)",
            }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });
        return output;
    }

    private static void AssertDoNotOverlap(IActualLayout first, IActualLayout second)
    {
        const float tolerance = 0.5f;
        Assert.True(first.ActualWidth > 0 && first.ActualHeight > 0);
        Assert.True(second.ActualWidth > 0 && second.ActualHeight > 0);
        Assert.True(
            first.ActualX + first.ActualWidth <= second.ActualX + tolerance
            || second.ActualX + second.ActualWidth <= first.ActualX + tolerance
            || first.ActualY + first.ActualHeight <= second.ActualY + tolerance
            || second.ActualY + second.ActualHeight <= first.ActualY + tolerance,
            "Chart title, legend and plot must occupy separate layout rectangles.");
    }

    private static bool HasLegend(string presentationPath) =>
        ReadChartPart(presentationPath)
            .Descendants()
            .Any(static node => node.Name.LocalName == "legend");

    private static XElement ReadChartPart(string presentationPath)
    {
        using ZipArchive package = ZipFile.OpenRead(presentationPath);
        ZipArchiveEntry entry = package.Entries.Single(
            static item => item.FullName.StartsWith("ppt/charts/chart", StringComparison.Ordinal)
                && item.FullName.EndsWith(".xml", StringComparison.Ordinal));
        using Stream content = entry.Open();
        return XDocument.Load(content).Root!;
    }
}
