using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// A Markdown output holds the slides' text alone, so a convert to it warns
/// <c>LOSSY_CONVERSION</c> with <c>details.content</c> <c>["pictures"]</c>, naming the converted
/// slides that show pictures and how many pictures they show: as many as the
/// <c>slides extract --what media</c> of its hint writes, which counts a picture shown twice
/// once and a slide's background and picture fills too. A layout's picture is the deck's design
/// and counts on no slide; a video is no picture. Every text here is at most five characters
/// long, which evaluation mode reads whole.
/// </summary>
public sealed class SlidesMarkdownPictureTests
{
    /// <summary>Markdown names the pictures of slide 2; a presentation format or an export that draws the slides does not.</summary>
    [Theory]
    [InlineData("md", "md", true)]
    [InlineData("pptx", "pptx", false)]
    [InlineData("pdf", "pdf", false)]
    [InlineData("html", "html", false)]
    public void Convert_NamesThePicturesOnlyAnOutputOfTheSlidesTextLeavesOut(string format, string extension, bool leavesThemOut)
    {
        using var fixture = new SlidesEngineFixture();
        string output = fixture.File($"pictured.{extension}");

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = Deck(fixture),
            Output = TestOutput.At(output, format: format),
        });

        if (!leavesThemOut)
        {
            Assert.Empty(PictureWarnings(result));
            return;
        }

        Warning warning = Assert.Single(PictureWarnings(result));
        Assert.Equal("The md output holds the slides' text alone and leaves out the 2 picture(s) shown on slide(s) 2.", warning.Message);
        Assert.Contains(" --what media --slides 2 --out-dir ", warning.Hint, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(Path.GetDirectoryName(output)!, "pictured.pictures"), warning.Hint, StringComparison.Ordinal);
        Assert.False(warning.AffectsCompleteness);
        string markdown = File.ReadAllText(output);
        Assert.Contains("T2", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("![", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// The warning names only the converted slides with pictures: a selection without them, and
    /// a hidden slide the convert of every slide leaves out, are not named. Slide 4 shows the
    /// picture slide 2 shows, which counts once.
    /// </summary>
    [Theory]
    [InlineData(null, false, "2 picture(s) shown on slide(s) 2, 4")]
    [InlineData("1,3", false, null)]
    [InlineData(null, true, "2 picture(s) shown on slide(s) 2")]
    [InlineData("4", true, "1 picture(s) shown on slide(s) 4")]
    public void Warning_NamesOnlyTheConvertedSlidesWithPictures(string? slides, bool hideFourth, string? expected)
    {
        using var fixture = new SlidesEngineFixture();
        string input = Deck(fixture);
        using (var deck = new Presentation(input))
        {
            ISlide fourth = deck.Slides.AddEmptySlide(deck.Slides[0].LayoutSlide);
            fourth.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = "T4";
            fourth.Shapes.AddPictureFrame(ShapeType.Rectangle, 300, 200, 40, 40, ((IPictureFrame)deck.Slides[1].Shapes[1]).PictureFormat.Picture.Image);
            fourth.Hidden = hideFourth;
            deck.Save(input, SaveFormat.Pptx);
        }

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("pictured.md"), format: "md"),
            Slides = slides is null ? null : PageRange.Parse(slides),
        });

        Warning[] warnings = [.. PictureWarnings(result)];
        if (expected is null)
        {
            Assert.Empty(warnings);
            return;
        }

        Assert.EndsWith($"leaves out the {expected}.", Assert.Single(warnings).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A picture the slide shows as its own background, or as the fill of a shape, is a picture
    /// the Markdown leaves out and the extract writes, so the slide is named.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Warning_NamesASlideWhosePictureIsItsBackgroundOrAFill(bool background)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("filled.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = "T1";
            IAutoShape shape = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 300, 200, 80, 80);
            if (background)
            {
                slide.Background.Type = BackgroundType.OwnBackground;
            }

            IFillFormat fill = background ? slide.Background.FillFormat : shape.FillFormat;
            fill.FillType = FillType.Picture;
            fill.PictureFillFormat.Picture.Image = presentation.Images.AddImage(SlidesEngineFixture.Png(8));
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesConvertResult result = SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("filled.md"), format: "md"),
        });

        Assert.EndsWith("leaves out the 1 picture(s) shown on slide(s) 1.", Assert.Single(PictureWarnings(result)).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// One picture shown once on slide 2 and twice on slide 3 is one picture: the warning says so,
    /// and the extract its hint names writes one image file for it.
    /// </summary>
    [Fact]
    public void Warning_CountsAPictureShownOnSeveralSlides_AsTheExtractOfItsHintWritesIt()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("repeated.pptx");
        using (var presentation = new Presentation())
        {
            IPPImage image = presentation.Images.AddImage(SlidesEngineFixture.Png(8));
            ILayoutSlide layout = presentation.Slides[0].LayoutSlide;
            for (int number = 1; number <= 3; number++)
            {
                ISlide slide = number == 1 ? presentation.Slides[0] : presentation.Slides.AddEmptySlide(layout);
                slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = $"T{number}";
                for (int copy = 1; copy < number; copy++)
                {
                    slide.Shapes.AddPictureFrame(ShapeType.Rectangle, 100 * copy, 200, 40, 40, image);
                }
            }

            presentation.Save(input, SaveFormat.Pptx);
        }

        Warning warning = Assert.Single(PictureWarnings(SlidesExport.Convert(fixture.Session, new SlidesConvertRequest
        {
            Input = input,
            Output = TestOutput.At(fixture.File("repeated.md"), format: "md"),
        })));
        SlidesExtractResult extracted = SlidesExtract.Run(fixture.Session, new SlidesExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("repeated.pictures")),
            What = SlidesExtractKinds.Media,
            Slides = PageRange.Parse("2,3"),
        });

        Assert.Equal("The md output holds the slides' text alone and leaves out the 1 picture(s) shown on slide(s) 2, 3.", warning.Message);
        Assert.Contains(" --slides 2,3 ", warning.Hint, StringComparison.Ordinal);
        Assert.Single(extracted.Outputs);
    }

    private static IEnumerable<Warning> PictureWarnings(SlidesConvertResult result) =>
        (result.Warnings ?? []).Where(static warning =>
            warning.Code == WarningCodes.LossyConversion && warning.Details?["content"]?.ToJsonString() == """["pictures"]""");

    /// <summary>
    /// A three-slide deck whose slides show T1, T2 and T3 on a layout with a picture: slide 2 also
    /// shows a picture and a group that holds another, slide 3 a video.
    /// </summary>
    private static string Deck(SlidesEngineFixture fixture)
    {
        string path = fixture.File("deck.pptx");
        using var presentation = new Presentation();
        ILayoutSlide layout = presentation.LayoutSlides.GetByType(SlideLayoutType.Blank);
        layout.Shapes.AddPictureFrame(ShapeType.Rectangle, 600, 10, 40, 40, presentation.Images.AddImage(SlidesEngineFixture.Png(10)));
        for (int number = 1; number <= 3; number++)
        {
            ISlide slide = presentation.Slides.AddEmptySlide(layout);
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = $"T{number}";
        }

        presentation.Slides.RemoveAt(0);
        ISlide second = presentation.Slides[1];
        second.Shapes.AddPictureFrame(ShapeType.Rectangle, 300, 200, 40, 40, presentation.Images.AddImage(SlidesEngineFixture.Png(8)));
        second.Shapes.AddGroupShape().Shapes.AddPictureFrame(ShapeType.Rectangle, 400, 200, 40, 40, presentation.Images.AddImage(SlidesEngineFixture.Png(9)));
        presentation.Slides[2].Shapes.AddVideoFrame(300, 200, 80, 60, presentation.Videos.AddVideo([.. Enumerable.Range(0, 64).Select(static value => (byte)value)]));
        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }
}
