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
            new PresentationEditRequest { OutputPath = output });
        return output;
    }

    [Fact]
    public void Column_OfNonNegativeValues_IsDrawnFromZero()
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "baseline.pptx", "column", 95, 100, 105);

        // Without a zero baseline the engine scales 95..105 onto roughly 90..106,
        // which draws a 10.5% spread as columns of 5, 10 and 15 units.
        Assert.Equal("0", ReadValueAxisMinimum(output));
    }

    [Fact]
    public void Column_WithANegativeValue_KeepsAutomaticScaling()
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "negative.pptx", "column", 95, -100, 105);

        // Pinning zero here would clip the negative column off the chart.
        Assert.Null(ReadValueAxisMinimum(output));
    }

    [Fact]
    public void Line_KeepsAutomaticScaling()
    {
        using var fixture = new SlidesEngineFixture();

        string output = Insert(fixture, "line.pptx", "line", 95, 100, 105);

        // A line encodes value by position, so a zoomed axis is legitimate.
        Assert.Null(ReadValueAxisMinimum(output));
    }

    [Fact]
    public void ValueAxis_KeepsAMinimumTheAuthorSetExplicitly()
    {
        using var fixture = new SlidesEngineFixture();
        string seeded = Insert(fixture, "explicit-seed.pptx", "column", 95, 100, 105);
        SetValueAxisMinimum(seeded, 50);
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
                        Shape = FindChartShapeId(seeded),
                        Series = [new SlidesChartSeriesInput { Name = "Amount", Values = [96, 101, 106] }],
                    },
                ],
            },
            new PresentationEditRequest { OutputPath = output });

        Assert.Equal("50", ReadValueAxisMinimum(output));
    }

    [Fact]
    public void GrowingAChartToASecondSeries_EnablesItsLegend()
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
                        Shape = FindChartShapeId(seeded),
                        Series =
                        [
                            new SlidesChartSeriesInput { Name = "Actual", Values = [95, 100, 105] },
                            new SlidesChartSeriesInput { Name = "Target", Values = [100, 110, 120] },
                        ],
                    },
                ],
            },
            new PresentationEditRequest { OutputPath = output });

        // Two series that cannot be told apart are unreadable.
        Assert.True(HasLegend(output));
    }

    private static long FindChartShapeId(string presentationPath)
    {
        using var presentation = new Presentation(presentationPath);
        return (long)presentation.Slides[0].Shapes
            .First(static shape => shape is IChart).OfficeInteropShapeId;
    }

    private static void SetValueAxisMinimum(string presentationPath, double minimum)
    {
        using var presentation = new Presentation(presentationPath);
        var chart = (IChart)presentation.Slides[0].Shapes.First(static shape => shape is IChart);
        chart.Axes.VerticalAxis.IsAutomaticMinValue = false;
        chart.Axes.VerticalAxis.MinValue = minimum;
        presentation.Save(presentationPath, Aspose.Slides.Export.SaveFormat.Pptx);
    }

    private static string? ReadValueAxisMinimum(string presentationPath) =>
        ReadChartPart(presentationPath)
            .Descendants()
            .FirstOrDefault(static node => node.Name.LocalName == "min")
            ?.Attribute("val")?.Value;

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
