using System.Drawing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// <c>set_shape_style</c> styles the text a shape shows, whatever kind of shape holds it, and
/// refuses a shape that has none rather than reporting a change it did not make.
/// </summary>
public sealed class SlidesShapeStyleTests
{
    private static readonly SlidesShapeStyleInput TextStyle = new()
    {
        Font = "Microsoft YaHei",
        Size = 12,
        Color = "#FFFFFF",
        Bold = true,
    };

    [Fact]
    public void Table_StylesTheTextFillAndBordersOfEveryCell()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = Seed(fixture, "table-seed.pptx", new SlidesInsertTableOp
        {
            Slide = 1,
            Rect = new SlidesRectInput { X = 40, Y = 120, Width = 400, Height = 120 },
            RowCount = 3,
            ColumnCount = 2,
            Data = [["Metric", "Q3"], ["Revenue", "18,200"], ["Margin", "38.5%"]],
        });

        string output = Style(fixture, seed, "table.pptx", Shape<ITable>(seed), TextStyle with { Fill = "#102030", Line = "#405060" });

        using var deck = new Presentation(output);
        ITable table = deck.Slides[0].Shapes.OfType<ITable>().Single();
        ICell[] cells = table.Rows.SelectMany(static row => row).ToArray();
        Assert.Equal(6, cells.Length);
        Assert.All(cells, static cell =>
        {
            Assert.Equal(Color.FromArgb(0x10, 0x20, 0x30).ToArgb(), cell.CellFormat.FillFormat.SolidFillColor.Color.ToArgb());
            Assert.All(
                new[] { cell.CellFormat.BorderTop, cell.CellFormat.BorderBottom, cell.CellFormat.BorderLeft, cell.CellFormat.BorderRight },
                static border => Assert.Equal(Color.FromArgb(0x40, 0x50, 0x60).ToArgb(), border.FillFormat.SolidFillColor.Color.ToArgb()));
            AssertTextStyle(Assert.Single(cell.TextFrame.Paragraphs.SelectMany(static paragraph => paragraph.Portions)).PortionFormat);
        });
    }

    [Fact]
    public void Chart_StylesItsTitleLegendAndAxisText()
    {
        using var fixture = new SlidesEngineFixture();
        string seed = Seed(fixture, "chart-seed.pptx", new InsertChartOp
        {
            Slide = 1,
            Kind = "column",
            Rect = new SlidesRectInput { X = 40, Y = 100, Width = 500, Height = 280 },
            Categories = ["North", "South"],
            Series =
            [
                new SlidesChartSeriesInput { Name = "2025", Values = [10, 12] },
                new SlidesChartSeriesInput { Name = "2026", Values = [11, 14] },
            ],
            Title = "Revenue",
        });

        string output = Style(fixture, seed, "chart.pptx", Shape<IChart>(seed), TextStyle);

        using var deck = new Presentation(output);
        IChart chart = deck.Slides[0].Shapes.OfType<IChart>().Single();
        AssertTextStyle(chart.TextFormat.PortionFormat);
        AssertTextStyle(chart.ChartTitle.TextFormat.PortionFormat);
        AssertTextStyle(chart.ChartTitle.TextFrameForOverriding.Paragraphs[0].Portions[0].PortionFormat);
        AssertTextStyle(chart.Legend.TextFormat.PortionFormat);
        AssertTextStyle(chart.Axes.HorizontalAxis.TextFormat.PortionFormat);
        AssertTextStyle(chart.Axes.VerticalAxis.TextFormat.PortionFormat);
    }

    [Fact]
    public void ShapeWithoutText_RefusesATextStyle()
    {
        using var fixture = new SlidesEngineFixture();
        string picture = fixture.File("logo.png");
        using (var source = new Presentation())
        using (IImage image = source.Slides[0].GetImage(0.1f, 0.1f))
        {
            image.Save(picture, ImageFormat.Png);
        }

        string seed = Seed(fixture, "picture-seed.pptx", new SlidesInsertImageOp { Slide = 1, Path = picture });
        string output = fixture.File("picture.pptx");

        CliException error = Assert.Throws<CliException>(() => Style(
            fixture, seed, "picture.pptx", Shape<IPictureFrame>(seed), new SlidesShapeStyleInput { Color = "#FFFFFF" }));

        Assert.Equal(ErrorCodes.OpsInvalid, error.Code);
        Assert.Contains("text", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private static void AssertTextStyle(IBasePortionFormat format)
    {
        Assert.Equal("Microsoft YaHei", format.LatinFont.FontName);
        Assert.Equal("Microsoft YaHei", format.EastAsianFont.FontName);
        Assert.Equal(12f, format.FontHeight);
        Assert.Equal(NullableBool.True, format.FontBold);
        Assert.Equal(FillType.Solid, format.FillFormat.FillType);
        Assert.Equal(Color.White.ToArgb(), format.FillFormat.SolidFillColor.Color.ToArgb());
    }

    private static string Seed(SlidesEngineFixture fixture, string name, SlidesOp insert)
    {
        string seed = fixture.File(name);
        fixture.Engine.ApplyOps(
            fixture.CreatePresentation("blank-" + name, slides: 1),
            new SlidesOpsBatch { Ops = [insert] },
            new PresentationEditRequest { OutputPath = seed });
        return seed;
    }

    private static string Style(SlidesEngineFixture fixture, string seed, string name, long shapeId, SlidesShapeStyleInput style)
    {
        string output = fixture.File(name);
        fixture.Engine.ApplyOps(
            seed,
            new SlidesOpsBatch { Ops = [new SetShapeStyleOp { Slide = 1, ShapeId = shapeId, Style = style }] },
            new PresentationEditRequest { OutputPath = output });
        return output;
    }

    private static long Shape<TShape>(string path)
        where TShape : IShape
    {
        using var presentation = new Presentation(path);
        return (long)presentation.Slides[0].Shapes.OfType<TShape>().Single().OfficeInteropShapeId;
    }
}
