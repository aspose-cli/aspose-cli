using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// A layout name set_layout gives names a layout of each slide's own master first, so a slide
/// leaves its master only for a name its master has none of, which the edit names with the names
/// that take the slide back. No command adds a master whose layouts are named apart from the
/// first's, so the decks are built here.
/// </summary>
public sealed class SlidesLayoutMasterTests
{
    [Fact]
    public void SetLayout_TakesALayoutOfAnotherMasterOnlyForANameItsOwnHasNoneOf()
    {
        using var fixture = new SlidesEngineFixture();
        // Slide 2 is on a second master, a copy of the first whose Title Only layout is renamed.
        string input = Deck(fixture, static second => second.LayoutSlides.GetByType(SlideLayoutType.TitleOnly).Name = "Heading Only");

        string output = fixture.File("relaid.pptx");
        SlidesEditResult result = Edit(fixture, input, output,
            new SetLayoutOp { Slides = "1-2", Layout = "Heading Only" },
            new SetLayoutOp { Slides = "2", Layout = "Title and Content" });

        using (var relaid = new Presentation(output))
        {
            Assert.All(relaid.Slides, slide => Assert.Same(relaid.Masters[1], slide.LayoutSlide.MasterSlide));
            Assert.Equal(["Heading Only", "Title and Content"], relaid.Slides.Select(static slide => slide.LayoutSlide.Name));
        }

        Warning moved = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "SLIDE_MASTER_CHANGED");
        Assert.Equal("slide/1", moved.Location);
        Assert.Equal(1, (int)moved.Details!["fromMaster"]!);
        Assert.Equal(2, (int)moved.Details!["toMaster"]!);
        // Master 2 has every name of master 1 but Title Only, so only Title Only takes the slide back.
        Assert.StartsWith("To take the slide back to master 1, set_layout it to one of 'Title Only', the layouts of master 1 whose names master 2 has none of;", moved.Hint, StringComparison.Ordinal);

        string back = fixture.File("back.pptx");
        Edit(fixture, output, back, new SetLayoutOp { Slides = "1", Layout = "Title Only" });
        using var returned = new Presentation(back);
        Assert.Same(returned.Masters[0], returned.Slides[0].LayoutSlide.MasterSlide);
    }

    [Fact]
    public void SetLayout_SaysThatNoNameTakesTheSlideBackWhereItsNewMasterHasEveryNameOfItsOld()
    {
        using var fixture = new SlidesEngineFixture();
        // The second master is a copy of the first with one more layout.
        string input = Deck(fixture, static second => second.LayoutSlides.Add(SlideLayoutType.TitleOnly, "Heading Only"));

        SlidesEditResult result = Edit(fixture, input, fixture.File("relaid.pptx"), new SetLayoutOp { Slides = "1", Layout = "Heading Only" });

        Warning moved = Assert.Single(result.Warnings!, static warning => warning.Code.Name == "SLIDE_MASTER_CHANGED");
        Assert.StartsWith("No layout name takes the slide back to master 1:", moved.Hint, StringComparison.Ordinal);
    }

    // A deck whose slide 1 is on the first master and slide 2 on a copy of it that the action changes.
    private static string Deck(SlidesEngineFixture fixture, Action<IMasterSlide> change)
    {
        string path = fixture.File("two-masters.pptx");
        using var deck = new Presentation();
        IMasterSlide second = deck.Masters.AddClone(deck.Masters[0]);
        change(second);
        deck.Slides.AddEmptySlide(second.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject));
        deck.Save(path, SaveFormat.Pptx);
        return path;
    }

    private static SlidesEditResult Edit(SlidesEngineFixture fixture, string input, string output, params SlidesOp[] ops) =>
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = ops },
            Output = TestOutput.At(output),
        });
}
