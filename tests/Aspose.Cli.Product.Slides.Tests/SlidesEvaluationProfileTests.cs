using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>Slide images show only the evaluation watermark of the slides that carry it.</summary>
public sealed class SlidesEvaluationProfileTests
{
    [Fact]
    public void SlideImages_ShowTheWatermarkOnlyOfTheSlidesThatCarryIt()
    {
        using var presentation = new Presentation();
        IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 100, 100, 400, 80);
        box.TextFrame.Text = "Evaluation only.";
        box.TextFrame.Paragraphs.Add(new Paragraph { Text = "Created with Aspose.Slides." });
        box.ShapeLock.SelectLocked = true;
        box.ShapeLock.PositionLocked = true;
        presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        IEvaluationProfile<Presentation> profile = new SlidesEvaluationProfile();

        Assert.NotEmpty(profile.Inspect(presentation, "png", [1]).Marks);
        Assert.Empty(profile.Inspect(presentation, "png", [2]).Marks);
    }
}
