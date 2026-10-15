using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesTargetLivenessTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SlideNumberADeletionLeftPastTheEnd_FailsTheOperation(bool bestEffort, bool dryRun)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        byte[] original = File.ReadAllBytes(input);
        string output = fixture.File("deleted-slide.pptx");
        var batch = new SlidesOpsBatch
        {
            Ops =
            [
                new DeleteSlidesOp { Slides = "1" },
                new SetTitleOp { Slide = 3, Text = "Lost" },
                new SetNotesOp { Slide = 2, Text = "Safe" },
            ],
        };
        var request = new SlidesEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = bestEffort, DryRun = dryRun },
        };
        if (bestEffort)
        {
            SlidesEditResult result = SlidesEdit.Run(fixture.Session, request);
            Assert.Equal(["ok", "failed", "ok"], result.Applied.Select(operation => operation.Status));
            Assert.Equal(0, result.Applied[1].ItemsAffected);
            Assert.Equal(["slide/3"], result.Applied[1].Targets);
            Assert.Equal(!dryRun, File.Exists(output));
            if (!dryRun)
            {
                using var reopened = new Presentation(output);
                Assert.Equal(2, reopened.Slides.Count);
                Assert.Contains("Safe", reopened.Slides[1].NotesSlideManager.NotesSlide!.NotesTextFrame!.Text, StringComparison.Ordinal);
            }
        }
        else
        {
            Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, request));
            Assert.False(File.Exists(output));
        }
        Assert.Equal(original, File.ReadAllBytes(input));
    }

    [Fact]
    public void MultiSlideOperation_ValidatesEveryTargetBeforeMutatingAnyTarget()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("mixed.pptx");
        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new DeleteSlidesOp { Slides = "2" }, new SetSlideHiddenOp { Slides = "1-3", Hidden = true }],
            },
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = true },
        });
        Assert.Equal(["ok", "failed"], result.Applied.Select(operation => operation.Status));
        using var reopened = new Presentation(output);
        Assert.All(reopened.Slides, slide => Assert.False(slide.Hidden));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletedShape_CannotBeEditedThroughItsOriginalIdentity(bool bestEffort)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        long shapeId;
        using (var source = new Presentation(input))
        {
            shapeId = source.Slides[0].Shapes.First(shape => shape.Name == "Title 1").OfficeInteropShapeId;
        }
        string output = fixture.File("deleted-shape.pptx");
        var batch = new SlidesOpsBatch
        {
            Ops =
            [
                new DeleteShapeOp { Slide = 1, ShapeId = shapeId },
                new SetTextOp { Slide = 1, ShapeId = shapeId, Text = "Lost" },
                new SetNotesOp { Slide = 2, Text = "Safe" },
            ],
        };
        var request = new SlidesEditRequest { Input = input, Batch = batch, Output = TestOutput.At(output), Options = new EditCommandOptions { BestEffort = bestEffort } };
        if (!bestEffort)
        {
            Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, request));
            Assert.False(File.Exists(output));
            return;
        }
        SlidesEditResult result = SlidesEdit.Run(fixture.Session, request);
        Assert.Equal(["ok", "failed", "ok"], result.Applied.Select(operation => operation.Status));
        Assert.Equal(result.Applied[0].Targets, result.Applied[1].Targets);
        using var reopened = new Presentation(output);
        Assert.DoesNotContain(reopened.Slides[0].Shapes, shape => shape.OfficeInteropShapeId == shapeId);
    }

    [Fact]
    public void InsertedSlide_CanBeTargetedByALaterOperationOfTheSameBatch()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("inserted.pptx");
        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new InsertSlideOp(), new SetTextOp { Slide = 4, Placeholder = "title", Text = "Added" }],
            },
            Output = TestOutput.At(output),
        });

        Assert.Equal(["ok", "ok"], result.Applied.Select(operation => operation.Status));
        Assert.Equal(["slide/4"], result.Applied[0].Targets);
        using var reopened = new Presentation(output);
        Assert.Equal(4, reopened.Slides.Count);
        IAutoShape title = reopened.Slides[3].Shapes.OfType<IAutoShape>()
            .Single(shape => shape.Placeholder?.Type is PlaceholderType.Title or PlaceholderType.CenteredTitle);
        Assert.Equal("Added", title.TextFrame.Text);
        Assert.Equal(1, result.Applied[1].ItemsAffected);
        Assert.Equal([$"slide/4/shape/{title.OfficeInteropShapeId}"], result.Applied[1].Targets);
    }

    [Fact]
    public void MovedSlide_IsAddressedByItsPositionAfterTheMove()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        uint movedId;
        uint secondId;
        using (var source = new Presentation(input))
        {
            movedId = source.Slides[0].SlideId;
            secondId = source.Slides[1].SlideId;
        }

        string output = fixture.File("moved.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new MoveSlideOp { Slide = 1, To = 3 }, new SetNotesOp { Slide = 3, Text = "Moved" }, new SetNotesOp { Slide = 1, Text = "One" }],
            },
            Output = TestOutput.At(output),
        });
        using var reopened = new Presentation(output);
        Assert.Equal(movedId, reopened.Slides[2].SlideId);
        Assert.Equal(secondId, reopened.Slides[0].SlideId);
        Assert.Contains("Moved", reopened.Slides[2].NotesSlideManager.NotesSlide!.NotesTextFrame!.Text, StringComparison.Ordinal);
        Assert.Contains("One", reopened.Slides[0].NotesSlideManager.NotesSlide!.NotesTextFrame!.Text, StringComparison.Ordinal);
    }
}
