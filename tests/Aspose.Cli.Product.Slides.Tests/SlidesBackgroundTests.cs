using System.Drawing;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// A slide's own background belongs to its design: a slide that takes another design, through
/// <c>use-dest</c> or <c>apply_layout</c>, shows that design's background instead.
/// </summary>
public sealed class SlidesBackgroundTests
{
    private const string Beige = "#FFF4E0";

    [Theory]
    [InlineData("use-dest", false)]
    [InlineData("keep-source", true)]
    public void AppendedSlide_KeepsItsOwnBackgroundOnlyWithItsOwnDesign(string masterPolicy, bool kept)
    {
        using var fixture = new SlidesEngineFixture();
        string source = BeigeDeck(fixture, "source.pptx");
        string destination = fixture.File("destination.pptx");
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(destination) });
        string output = fixture.File($"merged-{masterPolicy}.pptx");

        SlidesEditResult result = fixture.Engine.ApplyOps(destination, new SlidesOpsBatch
        {
            Ops = [new AppendPresentationOp { Path = source, MasterPolicy = masterPolicy }],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        using var deck = new Presentation(output);
        Assert.Equal(kept, IsBeige(deck.Slides[1]));
        Assert.Equal(kept ? null : "slide 2", result.Warnings?.SingleOrDefault(static warning => warning.Code == "SLIDE_BACKGROUND_RESET")?.Location);
    }

    [Fact]
    public void ApplyLayout_ShowsTheLayoutBackgroundUnlessTheBatchSetsOneAfterwards()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("deck.pptx");
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(input) });
        fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops =
            [
                new AddSlideOp { Layout = "Title and Content" },
                new AddSlideOp { Layout = "Title and Content" },
            ],
        }, new PresentationEditRequest { Output = TestOutput.At(input, overwrite: true) });
        fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops = [new SetBackgroundOp { Color = Beige }],
        }, new PresentationEditRequest { Output = TestOutput.At(input, overwrite: true) });
        string output = fixture.File("relaid.pptx");

        SlidesEditResult result = fixture.Engine.ApplyOps(input, new SlidesOpsBatch
        {
            Ops =
            [
                new ApplyLayoutOp { Slides = "2-3", Layout = "Section Header" },
                new SetBackgroundOp { Slides = "3", Color = Beige },
                new ApplyLayoutOp { Slides = "2", Layout = "Title and Content" },
            ],
        }, new PresentationEditRequest { Output = TestOutput.At(output) });

        // Only a slide that had its own background is named.
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == "SLIDE_BACKGROUND_RESET");
        Assert.Equal(("slides 2, 3", "The own background of slide(s) 2, 3 was removed, so they show their layout's background."),
            (warning.Location, warning.Message));
        using var deck = new Presentation(output);
        Assert.Equal([true, false, true], deck.Slides.Select(IsBeige));
        Assert.Equal(
            deck.Slides[1].LayoutSlide.Background.GetEffective().FillFormat.SolidFillColor.ToArgb(),
            deck.Slides[1].Background.GetEffective().FillFormat.SolidFillColor.ToArgb());
    }

    private static string BeigeDeck(SlidesEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        fixture.Engine.ApplyOps(
            fixture.CreatePresentation("plain-" + name, slides: 1),
            new SlidesOpsBatch { Ops = [new SetBackgroundOp { Color = Beige }] },
            new PresentationEditRequest { Output = TestOutput.At(path) });
        return path;
    }

    private static bool IsBeige(ISlide slide) =>
        slide.Background.GetEffective().FillFormat.SolidFillColor.ToArgb() == Color.FromArgb(0xFF, 0xF4, 0xE0).ToArgb();
}
