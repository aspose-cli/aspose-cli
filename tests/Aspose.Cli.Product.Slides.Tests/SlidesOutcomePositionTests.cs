using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What an edit reports it changed is where the output holds it: a slide, and the shapes and notes
/// on it, are numbered by the slide's position in the output of the whole batch, or in its input
/// when the batch removed the slide, whatever later operations moved or deleted.
/// </summary>
public sealed class SlidesOutcomePositionTests
{
    [Fact]
    public void ASlideAndWhatIsOnIt_AreNamedWhereALaterDeletionLeavesThem()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 3);
        long title = ShapeId(input, slide: 3, "Title 3");

        // Deleting slide 1 makes input slides 2 and 3 the output's slides 1 and 2.
        SlidesEditResult result = Edit(
            fixture,
            input,
            new SetTextOp { Slide = 3, ShapeName = "Title 3", Text = "Moved up" },
            new SetNotesOp { Slide = 3, Text = "Moved notes" },
            new SetSlideHiddenOp { Slides = "2", Hidden = true },
            new DeleteSlidesOp { Slides = "1" });

        Assert.Equal([$"slide/2/shape/{title}"], result.Applied[0].Targets);
        Assert.Equal(["slide/2/notes"], result.Applied[1].Targets);
        Assert.Equal(["slide/1"], result.Applied[2].Targets);
        Assert.Equal(["slide/1"], result.Applied[3].Targets);
        Assert.Equal([1, 2], result.SlidesTouched);
    }

    [Fact]
    public void AMovedSlide_IsNamedWhereALaterDeletionLeavesIt_AndTheDeletedOneByItsNumberInTheInput()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 3);

        // Slide 1 moves to the end (2, 3, 1), then input slide 2, now first, is deleted: 3, 1.
        SlidesEditResult result = Edit(
            fixture,
            input,
            new MoveSlideOp { Slide = 1, To = 3 },
            new DeleteSlidesOp { Slides = "1" });

        Assert.Equal(["slide/2"], result.Applied[0].Targets);
        Assert.Equal(["slide/2"], result.Applied[1].Targets);
        Assert.Equal([2], result.SlidesTouched);
        using var output = new Presentation(result.Output!.Path);
        Assert.Equal(["slide-3", "slide-1"], output.Slides.Select(static slide => slide.Name));
    }

    [Fact]
    public void ADeletedShape_IsNamedOnItsSlide_AndTouchesNoSlideOfTheOutput()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 3);
        long title = ShapeId(input, slide: 3, "Title 3");

        SlidesEditResult result = Edit(fixture, input, new DeleteShapeOp { Slide = 3, ShapeName = "Title 3" });

        Assert.Equal([$"slide/3/shape/{title}"], Assert.Single(result.Applied).Targets);
        Assert.Null(result.SlidesTouched);
    }

    private static long ShapeId(string path, int slide, string name)
    {
        using var presentation = new Presentation(path);
        return presentation.Slides[slide - 1].Shapes.Single(shape => shape.Name == name).OfficeInteropShapeId;
    }

    private static SlidesEditResult Edit(SlidesEngineFixture fixture, string input, params SlidesOp[] ops) =>
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = ops },
            Output = TestOutput.At(fixture.File($"edited-{Guid.NewGuid():N}.pptx")),
        });
}
