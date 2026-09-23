using System.IO.Compression;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCreationTests
{
    [Fact]
    public void Create_WithoutTemplate_UsesTheBuiltInWidescreenDesign()
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File("blank.pptx");

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest { OutputPath = output });

        Assert.Equal(1, result.Slides);
        Assert.Null(result.Template);
        using var deck = new Presentation(output);
        Assert.Equal(720f, deck.SlideSize.Size.Width);
        Assert.Equal(405f, deck.SlideSize.Size.Height);
        Assert.Contains(deck.LayoutSlides, static layout => layout.Name == "Two Content");
        Assert.Equal("Title Slide", Assert.Single(deck.Slides).LayoutSlide.Name);
        Assert.NotNull(SlidesPlaceholders.Title(deck.Slides[0]));
    }

    [Fact]
    public void Create_FromATemplateWithoutSlides_AddsOneTitleSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string template = fixture.File("layouts-only.pptx");
        using (var source = new Presentation())
        {
            source.Slides.RemoveAt(0);
            source.Save(template, SaveFormat.Pptx);
        }

        SlidesCreateResult result = fixture.Engine.Create(new NewPresentationRequest
        {
            OutputPath = fixture.File("from-template.pptx"),
            TemplatePath = template,
        });

        Assert.Equal(1, result.Slides);
        using var deck = new Presentation(fixture.File("from-template.pptx"));
        Assert.Equal(SlideLayoutType.Title, Assert.Single(deck.Slides).LayoutSlide.LayoutType);
    }

    [Theory]
    [InlineData("4x3", 720f, 540f)]
    [InlineData("16x9", 720f, 405f)]
    public void Create_WithSize_ScalesInheritedPlaceholdersWithTheCanvas(string size, float width, float height)
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File($"sized-{size}.pptx");

        fixture.Engine.Create(new NewPresentationRequest { OutputPath = output, Size = size });

        using var deck = new Presentation(output);
        Assert.Equal(width, deck.SlideSize.Size.Width);
        Assert.Equal(height, deck.SlideSize.Size.Height);
        IEnumerable<IShape> inherited = deck.Masters.SelectMany(static master => master.Shapes)
            .Concat(deck.LayoutSlides.SelectMany(static layout => layout.Shapes))
            .Where(static shape => shape.Placeholder is not null);
        Assert.All(inherited, shape =>
        {
            Assert.InRange(shape.X, -0.5f, width);
            Assert.InRange(shape.Y, -0.5f, height);
            Assert.True(shape.X + shape.Width <= width + 0.5f, $"{shape.Name} exceeds the canvas width.");
            Assert.True(shape.Y + shape.Height <= height + 0.5f, $"{shape.Name} exceeds the canvas height.");
        });
    }

    [Fact]
    public void BuiltInDesign_CarriesNoGeneratorMetadata()
    {
        using Stream stream = typeof(SlidesPresentationLoader).Assembly
            .GetManifestResourceStream("Templates/default-16x9.pptx")!;
        using var package = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.DoesNotContain(package.Entries, static entry => entry.FullName.StartsWith("ppt/tags/", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Entries, static entry => entry.FullName.Contains("thumbnail", StringComparison.OrdinalIgnoreCase));
        foreach (ZipArchiveEntry entry in package.Entries.Where(static entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.Open());
            string xml = reader.ReadToEnd();
            Assert.DoesNotContain("AS_OS", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("Aspose", xml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SlideSelection_OnAPresentationWithoutSlides_ReportsSlideNotFound()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("empty.pptx");
        using (var source = new Presentation())
        {
            source.Slides.RemoveAt(0);
            source.Save(input, SaveFormat.Pptx);
        }

        CliException render = Assert.Throws<CliException>(() => fixture.Engine.Render(
            input,
            new PresentationRenderRequest { TargetFormatId = "png", OutputPath = fixture.File("slide.png") }));
        CliException all = Assert.Throws<CliException>(() => fixture.Engine.Render(
            input,
            new PresentationRenderRequest { TargetFormatId = "png", OutputPath = fixture.File("all.png"), AllSlides = true }));
        CliException convert = Assert.Throws<CliException>(() => fixture.Engine.Convert(
            input,
            new PresentationConvertRequest { TargetFormatId = "png", OutputPath = fixture.File("converted.png") }));
        CliException read = Assert.Throws<CliException>(() => fixture.Engine.Read(
            input,
            new PresentationReadRequest { Slides = PageRange.Parse("1") }));

        Assert.All(
            new[] { render, all, convert, read },
            static error => Assert.Equal(SlidesDiagnostics.SlideNotFound, error.Code));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "*.png"));
    }
}
