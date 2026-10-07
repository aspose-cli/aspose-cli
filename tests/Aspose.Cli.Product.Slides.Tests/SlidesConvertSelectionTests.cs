using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesConvertSelectionTests
{
    [Theory]
    [InlineData("pptx")]
    [InlineData("odp")]
    public void ConvertSelectedSlides_KeepsTheSourceSizePropertiesAndMasters(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("source.pptx", slides: 3);
        using (var source = new Presentation(input))
        {
            source.SlideSize.SetSize(800, 450, SlideSizeScaleType.EnsureFit);
            source.DocumentProperties.Title = "Deck";
            source.Masters[0].Name = "Brand";
            source.Save(input, SaveFormat.Pptx);
        }

        string output = fixture.File($"selected.{format}");
        SlidesConvertResult result = fixture.Engine.Convert(input, new PresentationConvertRequest
        {
            Output = TestOutput.At(output, format: format),
            Slides = PageRange.Parse("2-3"),
        });

        Assert.Equal("2-3", result.Slides);
        using var converted = new Presentation(output);
        Assert.Equal(2, converted.Slides.Count);
        Assert.Equal("slide-2", converted.Slides[0].Name);
        Assert.Equal(800f, converted.SlideSize.Size.Width, 0.1f);
        Assert.Equal(450f, converted.SlideSize.Size.Height, 0.1f);
        Assert.Equal("Deck", converted.DocumentProperties.Title);
        // ODP names its master pages itself; PPTX keeps the source master.
        Assert.Equal(format == "pptx" ? "Brand" : converted.Masters[0].Name, converted.Slides[0].LayoutSlide.MasterSlide.Name);
        Assert.Single(converted.Masters);
    }

    [Fact]
    public void ConvertSelectedSlides_KeepsSourceEncryption()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("locked.pptx", slides: 2, password: "secret");
        string output = fixture.File("locked.selected.pptx");

        fixture.Engine.Convert(input, new PresentationConvertRequest
        {
            Output = TestOutput.At(output, format: "pptx"),
            Slides = PageRange.Parse("1"),
            Password = "secret",
        });

        IPresentationInfo info = PresentationFactory.Instance.GetPresentationInfo(output);
        Assert.True(info.IsPasswordProtected);
        Assert.True(info.CheckPassword("secret"));
    }
}
