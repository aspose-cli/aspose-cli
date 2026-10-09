using System.Security.Cryptography;
using Aspose.Cli.Product.Pdf.Engine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Operators;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

public sealed class PdfRenderGridTests
{
    [Fact]
    public void Grid_DrawsLinesEverySpacingAtDpiOver72AndLeavesTheSourceUnchanged()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreatePage(fixture, "blank.pdf", Rotation.None, marker: null);
        byte[] before = SHA256.HashData(File.ReadAllBytes(input));
        string plainPath = fixture.File("plain.png");
        string gridPath = fixture.File("grid.png");

        PdfRenderResult plain = Render(fixture, input, plainPath, grid: null);
        PdfRenderResult gridded = Render(fixture, input, gridPath, grid: 50);

        Assert.Null(plain.Grid);
        PdfRenderGrid note = Assert.IsType<PdfRenderGrid>(gridded.Grid);
        Assert.Equal((50, 100, "pt", "top-left"), (note.Spacing, note.LabelSpacing, note.Unit, note.Origin));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(input)));

        using SKBitmap withoutGrid = SKBitmap.Decode(plainPath);
        using SKBitmap withGrid = SKBitmap.Decode(gridPath);
        Assert.Equal((withoutGrid.Width, withoutGrid.Height), (withGrid.Width, withGrid.Height));
        const double scale = 144 / 72d;
        int y = (int)(275 * scale);
        foreach (int points in new[] { 150, 200, 250 })
        {
            int x = (int)Math.Round(points * scale);
            Assert.True(IsGridBlue(withGrid.GetPixel(x, y)), $"No grid line at {points} pt (x = {x} px).");
            Assert.False(IsGridBlue(withoutGrid.GetPixel(x, y)), $"The plain render is blue at x = {x} px.");
            Assert.True(IsGridBlue(withGrid.GetPixel(y, x)), $"No grid line at {points} pt (y = {x} px).");
        }

        int between = (int)Math.Round(175 * scale);
        Assert.Equal(withoutGrid.GetPixel(between, y), withGrid.GetPixel(between, y));
    }

    [Fact]
    public void Grid_OnARotatedPageStartsAtTheVisibleTopLeftInRedactAreaCoordinates()
    {
        using var fixture = new PdfEngineFixture();
        // Visible (rotated) box: 280 x 360 pt; the marker is the rectangle redact_area names as
        // x 30, y 40, width 50, height 20.
        string input = CreatePage(
            fixture,
            "rotated.pdf",
            Rotation.on90,
            new PdfRectInput { X = 30, Y = 40, Width = 50, Height = 20 });
        string output = fixture.File("rotated.png");

        Render(fixture, input, output, grid: 10);

        using SKBitmap bitmap = SKBitmap.Decode(output);
        Assert.Equal((560, 720), (bitmap.Width, bitmap.Height));
        Assert.True(IsDark(bitmap.GetPixel(70, 90)), "The marker is not at 35,45 pt of the visible page.");
        Assert.True(IsDark(bitmap.GetPixel(150, 110)), "The marker is not at 75,55 pt of the visible page.");
        Assert.False(IsDark(bitmap.GetPixel(56, 90)), "The marker starts before 30 pt.");
        Assert.False(IsDark(bitmap.GetPixel(70, 76)), "The marker starts above 40 pt.");
        Assert.True(IsGridBlue(bitmap.GetPixel(200, 300)), "No labelled line at x = 100 pt.");
        Assert.True(IsGridBlue(bitmap.GetPixel(300, 200)), "No labelled line at y = 100 pt.");
        Assert.True(
            Enumerable.Range(2, 20).Any(x => Enumerable.Range(2, 30).Any(yy => IsLabelText(bitmap.GetPixel(x, yy)))),
            "The top-left corner carries no 0 label.");
    }

    [Fact]
    public void Grid_WritesJpegOutput()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreatePage(fixture, "blank.pdf", Rotation.None, marker: null);
        string output = fixture.File("grid.jpg");

        PdfRenderResult result = PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "jpeg"),
            Dpi = 72,
            Grid = 20,
        });

        Assert.Equal(100, result.Grid!.LabelSpacing);
        using SKBitmap bitmap = SKBitmap.Decode(output);
        Assert.True(IsGridBlue(bitmap.GetPixel(100, 150)), "No grid line at x = 100 pt in the JPEG.");
    }

    [Fact]
    public void Grid_WithoutTheOptionLeavesTheRasterAsTheEngineWritesIt()
    {
        using var fixture = new PdfEngineFixture();
        string input = CreatePage(fixture, "blank.pdf", Rotation.None, marker: null);
        string output = fixture.File("plain.png");

        Render(fixture, input, output, grid: null);

        using var document = new Document(input);
        using var expected = new MemoryStream();
        new PngDevice(new Resolution(144)).Process(document.Pages[1], expected);
        Assert.Equal(expected.ToArray(), File.ReadAllBytes(output));
    }

    [Theory]
    [InlineData("svg", 50)]
    [InlineData("png", 9)]
    [InlineData("png", 501)]
    public void Grid_RefusesVectorOutputAndSpacingOutsideTheRange(string format, int spacing)
    {
        using var fixture = new PdfEngineFixture();
        string input = CreatePage(fixture, "blank.pdf", Rotation.None, marker: null);
        string output = fixture.File($"refused.{format}");

        CliException error = Assert.Throws<CliException>(() => PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: format),
            Grid = spacing,
        }));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains("--grid", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private static PdfRenderResult Render(PdfEngineFixture fixture, string input, string output, int? grid) =>
        PdfRender.Run(fixture.Session, new PdfRenderRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: "png"),
            Dpi = 144,
            Grid = grid,
        });

    /// <summary>A 400 x 300 pt page cropped to 360 x 280 pt, with an optional black marker in visible coordinates.</summary>
    private static string CreatePage(PdfEngineFixture fixture, string fileName, Rotation rotation, PdfRectInput? marker)
    {
        string path = fixture.File(fileName);
        using var document = new Document();
        Page page = document.Pages.Add();
        page.MediaBox = new Rectangle(0, 0, 400, 300);
        page.CropBox = new Rectangle(20, 10, 380, 290);
        page.Rotate = rotation;
        if (marker is not null)
        {
            Rectangle area = PdfEngineSupport.ToPdfRect(page, marker);
            page.Contents.Add(new GSave());
            page.Contents.Add(new SetRGBColor(0, 0, 0));
            page.Contents.Add(new Re(area.LLX, area.LLY, area.Width, area.Height));
            page.Contents.Add(new Fill());
            page.Contents.Add(new GRestore());
        }

        document.Save(path);
        return path;
    }

    private static bool IsGridBlue(SKColor color) => color.Blue > 200 && color.Blue - color.Red > 40;

    private static bool IsDark(SKColor color) => color.Red < 60 && color.Green < 60 && color.Blue < 80;

    private static bool IsLabelText(SKColor color) => color.Blue - color.Red > 60 && color.Red < 120;
}
