using System.Drawing;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// A slide's own background belongs to its design: a slide that takes another design, through
/// <c>use-destination</c> or <c>set_layout</c>, shows that design's background instead.
/// </summary>
public sealed class SlidesBackgroundTests
{
    private const string Beige = "#FFF4E0";

    [Theory]
    [InlineData("use-destination", false)]
    [InlineData("keep-source", true)]
    public void AppendedSlide_KeepsItsOwnBackgroundOnlyWithItsOwnDesign(string formatting, bool kept)
    {
        using var fixture = new SlidesEngineFixture();
        string source = BeigeDeck(fixture, "source.pptx");
        string destination = fixture.File("destination.pptx");
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(destination) });
        string output = fixture.File($"merged-{formatting}.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = destination,
            Batch = new SlidesOpsBatch
            {
                Ops = [new ImportSlidesOp { Path = source, Formatting = formatting }],
            },
            Output = TestOutput.At(output),
        });

        using var deck = new Presentation(output);
        Assert.Equal(kept, IsBeige(deck.Slides[1]));
        Assert.Equal(kept ? null : "slide/2", result.Warnings?.SingleOrDefault(static warning => warning.Code.Name == "SLIDE_BACKGROUND_RESET")?.Location);
    }

    [Fact]
    public void ApplyLayout_ShowsTheLayoutBackgroundUnlessTheBatchSetsOneAfterwards()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("deck.pptx");
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(input) });
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertSlideOp { Layout = "Title and Content" },
                    new InsertSlideOp { Layout = "Title and Content" },
                ],
            },
            Output = TestOutput.At(input, overwrite: true),
        });
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetBackgroundOp { Color = Beige }],
            },
            Output = TestOutput.At(input, overwrite: true),
        });
        string output = fixture.File("relaid.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SetLayoutOp { Slides = "2-3", Layout = "Section Header" },
                    new SetBackgroundOp { Slides = "3", Color = Beige },
                    new SetLayoutOp { Slides = "2", Layout = "Title and Content" },
                ],
            },
            Output = TestOutput.At(output),
        });

        // Only a slide that had its own background is named, each by its own warning.
        Assert.Equal(
            [
                ("slide/2", "The own background of slide 2 was removed, so it shows its layout's background."),
                ("slide/3", "The own background of slide 3 was removed, so it shows its layout's background."),
            ],
            result.Warnings!.Where(static warning => warning.Code.Name == "SLIDE_BACKGROUND_RESET").Select(static warning => (warning.Location, warning.Message)));
        using var deck = new Presentation(output);
        Assert.Equal([true, false, true], deck.Slides.Select(IsBeige));
        Assert.Equal(
            deck.Slides[1].LayoutSlide.Background.GetEffective().FillFormat.SolidFillColor.ToArgb(),
            deck.Slides[1].Background.GetEffective().FillFormat.SolidFillColor.ToArgb());
    }

    private static string BeigeDeck(SlidesEngineFixture fixture, string name)
    {
        string path = fixture.File(name);
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = fixture.CreatePresentation("plain-" + name, slides: 1),
            Batch = new SlidesOpsBatch { Ops = [new SetBackgroundOp { Color = Beige }] },
            Output = TestOutput.At(path),
        });
        return path;
    }

    private static bool IsBeige(ISlide slide) =>
        slide.Background.GetEffective().FillFormat.SolidFillColor.ToArgb() == Color.FromArgb(0xFF, 0xF4, 0xE0).ToArgb();
}
