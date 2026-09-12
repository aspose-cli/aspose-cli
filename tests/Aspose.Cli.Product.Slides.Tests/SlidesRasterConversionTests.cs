using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesRasterConversionTests
{
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    public void ConvertImages_UsesPresentationDimensionsAtDefaultRenderResolution(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        using (var source = new Presentation(input))
        {
            source.SlideSize.SetSize(720, 405, SlideSizeScaleType.DoNotScale);
            source.Save(input, SaveFormat.Pptx);
        }
        byte[] original = File.ReadAllBytes(input);
        SlidesConvertResult result = fixture.Engine.Convert(input, new PresentationConvertRequest
        {
            TargetFormatId = format,
            OutputPath = fixture.File("converted" + SlidesFormats.Extension(format)),
            Slides = PageRange.Parse("1,3"),
        });

        Assert.Equal(2, result.Outputs.Count);
        Assert.All(result.Outputs, output =>
        {
            using IImage image = Images.FromFile(output.Path);
            Assert.Equal(1920, image.Width);
            Assert.Equal(1080, image.Height);
            byte[] encoded = File.ReadAllBytes(output.Path);
            Assert.True(format == "png"
                ? encoded.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47 })
                : encoded.AsSpan().StartsWith(new byte[] { 0xff, 0xd8 }));
        });
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    public void ConvertImages_RejectsAnOversizedBatchBeforePublishing(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 8);
        using (var source = new Presentation(input))
        {
            source.SlideSize.SetSize(4000, 4000, SlideSizeScaleType.DoNotScale);
            source.Save(input, SaveFormat.Pptx);
        }
        string output = fixture.File("oversized" + SlidesFormats.Extension(format));

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.Convert(input, new PresentationConvertRequest
        {
            TargetFormatId = format,
            OutputPath = output,
        }));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "oversized.s*"));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    public void ConvertImages_ProducesUsefulDimensionsThroughRealCli(string format)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("outline.md"), "# Quarterly review\n\nGrowth and retention");
        CliResult create = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--size", "16x9", "--output", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        string output = "slide" + SlidesFormats.Extension(format);
        CliResult converted = workspace.Run("slides", "convert", "deck.pptx", "--to", format, "--out", output, "--output", "json");
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        using IImage image = Images.FromFile(workspace.File(output));
        Assert.Equal(1920, image.Width);
        Assert.Equal(1080, image.Height);
    }
}
