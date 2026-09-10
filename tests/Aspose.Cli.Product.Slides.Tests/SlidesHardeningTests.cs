using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesHardeningTests
{
    [Fact]
    public void FontRegistryOverride_IsRestoredWhenInitializationFails()
    {
        nint replacement = 42;
        var applied = new List<nint>();
        var expected = new InvalidOperationException("font initialization failed");

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() =>
            SlidesFontCatalog.RunWithTemporaryUserRegistryOverride(
                replacement,
                handle =>
                {
                    applied.Add(handle);
                    return 0;
                },
                () => throw expected));

        Assert.Same(expected, actual);
        Assert.Equal([replacement, nint.Zero], applied);
    }

    [Fact]
    public void LargeDeck_RemainsProgressiveAndSupportsBoundedRendering()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("windowed.pptx", slides: 12);

        PresentationInfoResult info = fixture.Engine.GetInfo(
            input,
            new PresentationInfoRequest());
        PresentationReadResult window = fixture.Engine.Read(
            input,
            new PresentationReadRequest { Scope = PresentationReadScopes.Text });
        SlidesRenderResult rendered = fixture.Engine.Render(
            input,
            new PresentationRenderRequest
            {
                TargetFormatId = "png",
                OutputPath = fixture.File("bounded.png"),
                Slides = PageRange.Parse("1,12"),
                Width = 320,
            });

        Assert.Equal(12, info.Presentation.Slides);
        Assert.Equal(10, window.Slides.Count);
        Assert.True(window.Window.Truncated);
        Assert.Equal([1, 12], rendered.Outputs.Select(static item => item.Slide));
        Assert.All(rendered.Outputs, static item => Assert.True(item.Output.SizeBytes > 100));
        Assert.Equal(320, PngWidth(rendered.Outputs[0].Output.Path));
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains(
                window.Warnings ?? [],
                static warning => warning.Code == WarningCodes.EvalInputTruncated);
        }
    }

    [Fact]
    public void UnboundedHighDpiBatch_IsRejectedBeforeWriting()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("budget.pptx", slides: 1);
        using (var presentation = new Aspose.Slides.Presentation(input))
        {
            presentation.SlideSize.SetSize(
                720,
                2_000,
                Aspose.Slides.SlideSizeScaleType.DoNotScale);
            presentation.Save(input, Aspose.Slides.Export.SaveFormat.Pptx);
        }

        string output = fixture.File("too-large.png");

        CliException error = Assert.Throws<CliException>(() =>
            fixture.Engine.Render(
                input,
                new PresentationRenderRequest
                {
                    TargetFormatId = "png",
                    OutputPath = output,
                    AllSlides = true,
                    Dpi = 1200,
                }));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "too-large.s*.png"));
    }

    [Fact]
    public void Render_RespectsExactWidthAndDpiGeometry()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("geometry.pptx", slides: 1);
        string widthOutput = fixture.File("width.png");
        string dpiOutput = fixture.File("dpi.png");

        fixture.Engine.Render(input, new PresentationRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = widthOutput,
            Width = 640,
        });
        fixture.Engine.Render(input, new PresentationRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = dpiOutput,
            Dpi = 150,
        });

        Assert.Equal(640, PngWidth(widthOutput));
        Assert.Equal(1500, PngWidth(dpiOutput));
    }

    [Theory]
    [MemberData(nameof(MalformedPresentations))]
    public void MalformedPresentation_ReturnsFileCorrupt(
        string extension,
        byte[] content)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("malformed" + extension);
        File.WriteAllBytes(input, content);
        CliException infoError = Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(input, new PresentationInfoRequest()));

        Assert.Equal(ErrorCodes.FileCorrupt, infoError.Code);
    }

    [Fact]
    public void MalformedConversion_WritesNoPartialOutput()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("malformed.pptx");
        File.WriteAllBytes(input, [0x50, 0x4B, 0x03, 0x04, 0x00]);
        string output = fixture.File("malformed.pdf");

        CliException error = Assert.Throws<CliException>(() =>
            fixture.Engine.Convert(
                input,
                new PresentationConvertRequest
                {
                    TargetFormatId = "pdf",
                    OutputPath = output,
                }));

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.False(File.Exists(output));
    }

    public static TheoryData<string, byte[]> MalformedPresentations => new()
    {
        { ".pptx", [0x50, 0x4B, 0x03, 0x04, 0x00] },
        { ".ppt", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1] },
        { ".pptx", "plain text renamed as a deck"u8.ToArray() },
        { ".odp", [0x00, 0x01, 0x02, 0x03, 0xFF] },
    };

    private static int PngWidth(string path)
    {
        byte[] header = File.ReadAllBytes(path);
        Assert.True(header.Length >= 24);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], header[..4]);
        return (header[16] << 24)
            | (header[17] << 16)
            | (header[18] << 8)
            | header[19];
    }
}
