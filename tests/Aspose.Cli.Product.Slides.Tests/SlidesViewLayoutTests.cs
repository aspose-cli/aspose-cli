using System.Drawing;
using Aspose.Cli.Sdk.Views;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The slide view places every top-level shape with its persisted id, so a
/// viewer can point at exactly the shape an edit changed.
/// </summary>
public sealed class SlidesViewLayoutTests
{
    [Fact]
    public void RenderView_PlacesShapesWithStableIdsInCssPixels()
    {
        using var fixture = new SlidesEngineFixture();
        (string path, double widthPoints) = CreateDeck(fixture, "deck.pptx", Color.SteelBlue);

        ViewPart slide = Assert.Single(Render(fixture, path).Parts);

        double scale = 960d / widthPoints;
        Assert.Equal(3, slide.Elements!.Count);
        Assert.All(slide.Elements, static element => Assert.Matches("^shape-[0-9]+$", element.Id));
        ViewElement title = Assert.Single(slide.Elements, static element => element.Label == "Quarterly results");
        Assert.Equal(40 * scale, title.Box.X, 1);
        Assert.Equal(30 * scale, title.Box.Y, 1);
        Assert.Equal(600 * scale, title.Box.Width, 1);
        Assert.Equal(70 * scale, title.Box.Height, 1);
        Assert.Equal("Speaker note", slide.Properties!["notes"]);
    }

    [Fact]
    public void RenderView_ChangesOnlyTheDigestOfTheEditedShape()
    {
        using var fixture = new SlidesEngineFixture();
        ViewElement[] blue = Render(fixture, CreateDeck(fixture, "blue.pptx", Color.SteelBlue).Path)
            .Parts.Single().Elements!.ToArray();
        ViewElement[] red = Render(fixture, CreateDeck(fixture, "red.pptx", Color.OrangeRed).Path)
            .Parts.Single().Elements!.ToArray();

        Assert.Equal(blue.Select(static element => element.Id), red.Select(static element => element.Id));
        Assert.Equal(1, blue.Zip(red).Count(static pair => pair.First.Digest != pair.Second.Digest));
    }

    private static ViewManifest Render(SlidesEngineFixture fixture, string path) =>
        fixture.Engine.RenderView(
            path,
            new ViewRenderRequest
            {
                View = SlidesViews.Slides,
                MaxParts = 5,
                Purpose = ViewPurpose.Evidence,
            },
            new DiscardingSink());

    private static (string Path, double WidthPoints) CreateDeck(
        SlidesEngineFixture fixture,
        string name,
        Color boxColor)
    {
        fixture.Gate.EnsureApplied();
        string path = fixture.File(name);
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides[0];
        IAutoShape title = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 30, 600, 70);
        title.TextFrame.Text = "Quarterly results";
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 150, 200, 100);
        box.FillFormat.FillType = FillType.Solid;
        box.FillFormat.SolidFillColor.Color = boxColor;
        IAutoShape caption = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 340, 150, 280, 100);
        caption.TextFrame.Text = "Revenue grew";
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame!.Text = "Speaker note";
        presentation.Save(path, SaveFormat.Pptx);
        return (path, presentation.SlideSize.Size.Width);
    }

    private sealed class DiscardingSink : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
        }

        public void WriteText(string relativePath, string content)
        {
        }
    }
}
