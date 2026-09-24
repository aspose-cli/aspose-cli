using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Slides.Export;
using Aspose.Slides;
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
            OutputPath = fixture.File("converted" + SlidesFormats.Definitions.ExtensionFor(format)),
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
    public void ConvertImages_RejectsAnOversizedSlideBeforePublishingAnyPart(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 8);
        using (var source = new Presentation(input))
        {
            // 7000 pt at the 192 DPI of convert is 18667 px square, above the 256 MiP image budget.
            source.SlideSize.SetSize(7000, 7000, SlideSizeScaleType.DoNotScale);
            source.Save(input, SaveFormat.Pptx);
        }
        string output = fixture.File("oversized" + SlidesFormats.Definitions.ExtensionFor(format));

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
    [InlineData("png", false)]
    [InlineData("jpeg", false)]
    [InlineData("png", true)]
    [InlineData("jpeg", true)]
    public void RasterOutput_UsesManagedStreamsForLongPublicationPaths(string format, bool convert)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 1);
        string directory = fixture.File(Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90)));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "slide" + SlidesFormats.Definitions.ExtensionFor(format));
        Assert.True(output.Length > 260);

        if (convert)
        {
            fixture.Engine.Convert(input, new PresentationConvertRequest
            {
                TargetFormatId = format,
                OutputPath = output,
            });
        }
        else
        {
            fixture.Engine.Render(input, new PresentationRenderRequest
            {
                TargetFormatId = format,
                OutputPath = output,
            });
        }

        using FileStream stream = File.OpenRead(output);
        using IImage image = Images.FromStream(stream);
        Assert.Equal(1920, image.Width);
        Assert.Equal(1440, image.Height);
        Assert.Empty(Directory.GetFiles(directory, "*.stage"));
    }
    [Fact]
    public void Edit_WritesLongPublicationPathsThroughRealCli()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("outline.md"), "# Quarterly review\n\nGrowth and retention");
        CliResult create = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--size", "16x9", "--output", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"op":"set_notes","slide":1,"text":"Review note"}]}""");
        string directory = workspace.File(Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90)));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "edited.pptx");
        Assert.True(output.Length > 260);
        CliResult edited = workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", output, "--output", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal(output, JsonNode.Parse(edited.StdOut)!["output"]!["path"]!.GetValue<string>());
        Assert.True(File.Exists(output));
        Assert.Empty(Directory.GetFiles(directory, "*.stage"));
    }
    [Fact]
    public void Review_RendersLongPublicationPathsThroughRealCli()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("outline.md"), "# Quarterly review\n\nGrowth and retention");
        CliResult create = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--size", "16x9", "--output", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        string directory = workspace.File(Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90)));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "review");
        Assert.True(output.Length > 260);
        CliResult reviewed = workspace.Run("review", "deck.pptx", "--out", output, "--max-items", "1", "--output", "json");
        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        JsonNode manifest = JsonNode.Parse(reviewed.StdOut)!;
        string relativeImage = Assert.Single(manifest["artifacts"]!.AsArray(),
            static artifact => artifact!["mediaType"]!.GetValue<string>() == "image/png")!["path"]!.GetValue<string>();
        string imagePath = Path.Combine(output, relativeImage);
        using FileStream stream = File.OpenRead(imagePath);
        using IImage image = Images.FromStream(stream);
        Assert.Equal(1600, image.Width);
        Assert.Equal(900, image.Height);
    }
    // The encoders and their dimensions are covered in-process; this proves the CLI route once.
    [Fact]
    public void ConvertImages_ProducesUsefulDimensionsThroughRealCli()
    {
        const string format = "png";
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("outline.md"), "# Quarterly review\n\nGrowth and retention");
        CliResult create = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--size", "16x9", "--output", "json");
        Assert.True(create.ExitCode == 0, create.StdErr);
        string output = "slide" + SlidesFormats.Definitions.ExtensionFor(format);
        CliResult converted = workspace.Run("slides", "convert", "deck.pptx", "--to", format, "--out", output, "--output", "json");
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        using IImage image = Images.FromFile(workspace.File(output));
        Assert.Equal(1920, image.Width);
        Assert.Equal(1080, image.Height);
    }
}
