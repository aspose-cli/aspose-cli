using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCreationTests
{
    [Fact]
    public void Create_WithoutTemplate_SavesTheEnginesNewPresentation()
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File("blank.pptx");

        SlidesCreateResult result = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(output) });

        Assert.Equal(1, result.SlideCount);
        Assert.Empty(result.Inputs);
        using var engine = new Presentation();
        using var deck = new Presentation(output);
        Assert.Equal(engine.SlideSize.Size, deck.SlideSize.Size);
        Assert.Equal(engine.LayoutSlides.Select(static layout => layout.Name), deck.LayoutSlides.Select(static layout => layout.Name));
        Assert.Equal(engine.Slides[0].LayoutSlide.Name, Assert.Single(deck.Slides).LayoutSlide.Name);
    }

    /// <summary>A template is saved as the new presentation as it is: one without slides gives a presentation without slides.</summary>
    [Fact]
    public void Create_FromATemplate_SavesItAsTheNewPresentation()
    {
        using var fixture = new SlidesEngineFixture();
        string template = fixture.File("layouts-only.pptx");
        using (var source = new Presentation())
        {
            source.Slides.RemoveAt(0);
            source.SlideSize.SetSize(800, 450, SlideSizeScaleType.DoNotScale);
            source.Save(template, SaveFormat.Pptx);
        }

        SlidesCreateResult result = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest
        {
            Output = TestOutput.At(fixture.File("from-template.pptx")),
            Template = new SourceFile("--template", template),
        });

        Assert.Equal(0, result.SlideCount);
        Assert.Equal("pptx", Assert.Single(result.Inputs).Format);
        using var deck = new Presentation(fixture.File("from-template.pptx"));
        Assert.Empty(deck.Slides);
        Assert.Equal(800f, deck.SlideSize.Size.Width, 0.1f);
        Assert.Equal(450f, deck.SlideSize.Size.Height, 0.1f);
    }

    [Fact]
    public void SlideSelection_OnAPresentationWithoutSlides_ReportsAddressNotFound()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("empty.pptx");
        using (var source = new Presentation())
        {
            source.Slides.RemoveAt(0);
            source.Save(input, SaveFormat.Pptx);
        }

        CliException render = Assert.Throws<CliException>(() => SlidesExport.Render(
            fixture.Session,
            new SlidesRenderRequest { Input = input, Output = TestOutput.At(fixture.File("slide.png"), format: "png"), Slides = PageRange.Parse("1") }));
        CliException all = Assert.Throws<CliException>(() => SlidesExport.Render(
            fixture.Session,
            new SlidesRenderRequest { Input = input, Output = TestOutput.At(fixture.File("all.png"), format: "png") }));
        CliException convert = Assert.Throws<CliException>(() => SlidesExport.Convert(
            fixture.Session,
            new SlidesConvertRequest { Input = input, Output = TestOutput.At(fixture.File("converted.pdf"), format: "pdf"), Slides = PageRange.Parse("1") }));

        Assert.All(
            new[] { render, all, convert },
            static error => Assert.Equal(ErrorCodes.AddressNotFound, error.Code));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "*.png"));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "*.pdf"));
    }
}
