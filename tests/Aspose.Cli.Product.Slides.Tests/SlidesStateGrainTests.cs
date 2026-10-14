using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Aspose.Slides.SlideShow;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The snapshots around an operation and <c>--verify</c> compare the state the engine stores, at
/// least as finely as a read reports it: white space a read collapses, a point a read shows as 0,
/// a bullet a paragraph only inherits, and a footer or transition setting a read leaves out still
/// count. A setting that a read rounds is compared as the read rounds it.
/// </summary>
public sealed class SlidesStateGrainTests
{
    [Theory]
    [InlineData("A  B", "S 1")]
    [InlineData("A B", "S  1")]
    public void SetChartData_ChangingOnlyWhiteSpaceInANameOrCategory_ChangesTheChart(string category, string series)
    {
        using var fixture = new SlidesEngineFixture();
        (string input, long chartId) = Chart(fixture);

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetChartDataOp { Slide = 1, ShapeId = chartId, Categories = [category, "C"], Series = [new SlidesChartSeriesInput { Name = series, Values = [1, 2] }] }],
            },
            Output = TestOutput.At(fixture.File("chart.out.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
    }

    [Fact]
    public void SetChartData_WritingZeroIntoAnEmptyPoint_ChangesTheChart()
    {
        using var fixture = new SlidesEngineFixture();
        (string input, long chartId) = Chart(fixture);
        using (var presentation = new Presentation(input))
        {
            IChart chart = presentation.Slides[0].Shapes.OfType<IChart>().Single();
            chart.ChartData.Series[0].DataPoints[0].Value.Data = null;
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetChartDataOp { Slide = 1, ShapeId = chartId, Categories = ["A B", "C"], Series = [new SlidesChartSeriesInput { Name = "S 1", Values = [0, 2] }] }],
            },
            Output = TestOutput.At(fixture.File("empty.out.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
    }

    [Fact]
    public void SetBody_StatingTheBulletAParagraphInherited_ChangesTheBody()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("body.pptx", slides: 1);
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetBodyOp { Slide = 1, Paragraphs = [new SlidesParagraphInput { Text = "x" }] }] },
            Output = TestOutput.At(input, overwrite: true),
        });
        using (var presentation = new Presentation(input))
        {
            IParagraph paragraph = SlidesAuthoring.Body(presentation.Slides[0]).TextFrame.Paragraphs[0];
            paragraph.ParagraphFormat.Bullet.Type = BulletType.NotDefined;
            Assert.Equal(BulletType.None, paragraph.ParagraphFormat.GetEffective().Bullet.Type);
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetBodyOp { Slide = 1, Paragraphs = [new SlidesParagraphInput { Text = "x" }] }] },
            Output = TestOutput.At(fixture.File("body.out.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
    }

    [Fact]
    public void Verify_ComparesWhetherTheFooterIsShown()
    {
        using var fixture = new SlidesEngineFixture();
        VerificationFacts facts = Verify(
            fixture.CreatePresentation("footer.pptx", slides: 1),
            new SetFooterOp { Id = "footer", Slides = "1", Text = "Foot" },
            static edited => edited.Slides[0].HeaderFooterManager.SetFooterVisibility(true),
            static output => output.Slides[0].HeaderFooterManager.SetFooterVisibility(false));

        Assert.Single(facts.Issues);
    }

    [Fact]
    public void Verify_ComparesATransitionNoOperationSets()
    {
        using var fixture = new SlidesEngineFixture();
        VerificationFacts facts = Verify(
            fixture.CreatePresentation("transition.pptx", slides: 1),
            new SetTransitionOp { Id = "transition", Slides = "1", Kind = SlidesTransitionKinds.Fade },
            static edited => Transition(edited, TransitionType.Circle),
            static output => Transition(output, TransitionType.Zoom));

        Assert.Single(facts.Issues);
    }

    [Fact]
    public void Verify_ComparesTheWhiteSpaceOfABodyParagraph()
    {
        TestLicense.Require("Evaluation mode records no text expectation.");
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("spaces.pptx");
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(input) });
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new InsertSlideOp { Layout = "Title and Content" }] },
            Output = TestOutput.At(input, overwrite: true),
        });
        VerificationFacts facts = Verify(
            input,
            new SetBodyOp { Id = "body", Slide = 2, Paragraphs = [new SlidesParagraphInput { Text = "a b" }] },
            static edited => SlidesAuthoring.Body(edited.Slides[1]).TextFrame.Text = "a  b",
            static output => SlidesAuthoring.Body(output.Slides[1]).TextFrame.Text = "a b");

        Assert.Single(facts.Issues);
    }

    [Fact]
    public void Verify_CountsReplacementsInTheNotesAsStored()
    {
        TestLicense.Require("Evaluation mode records no text expectation.");
        using var fixture = new SlidesEngineFixture();
        VerificationFacts facts = Verify(
            fixture.CreatePresentation("notes.pptx", slides: 1),
            new SlidesReplaceTextOp { Id = "replace", Find = " ", Replace = "", Scope = SlidesSearchScopes.Notes },
            static edited => edited.Slides[0].NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "x",
            static output => output.Slides[0].NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = " x");

        Assert.Single(facts.Issues);
    }

    [Fact]
    public void Verify_ComparesTheSlideSizeAsInspectRoundsIt()
    {
        using var fixture = new SlidesEngineFixture();
        VerificationFacts facts = Verify(
            fixture.CreatePresentation("size.pptx", slides: 1),
            new SetSlideSizeOp { Id = "size", SlideSize = new SlidesSlideSizeInput { Preset = SlidesSlideSizePresets.Standard } },
            static edited => edited.SlideSize.SetSize(720.001f, 540f, SlideSizeScaleType.DoNotScale),
            static output => output.SlideSize.SetSize(720f, 540f, SlideSizeScaleType.DoNotScale));

        Assert.Empty(facts.Issues);
    }

    [Fact]
    public void Verify_ComparesALayoutByItsMasterAndName()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("masters.pptx");
        string layout;
        using (var presentation = new Presentation())
        {
            IMasterSlide copy = presentation.Masters.AddClone(presentation.Masters[0]);
            layout = copy.LayoutSlides[0].Name;
            presentation.Save(input, SaveFormat.Pptx);
        }

        VerificationFacts facts = Verify(
            input,
            new SetLayoutOp { Id = "layout", Slides = "1", Layout = layout },
            static edited => edited.Slides[0].LayoutSlide = edited.Masters[0].LayoutSlides[0],
            static output => output.Slides[0].LayoutSlide = output.Masters[1].LayoutSlides[0]);

        Assert.Empty(facts.Issues);
    }

    private static void Transition(Presentation presentation, TransitionType type)
    {
        presentation.Slides[0].SlideShowTransition.Type = type;
        presentation.Slides[0].SlideShowTransition.Duration = 500;
    }

    /// <summary>Records one operation against the presentation <paramref name="edit"/> leaves and verifies the one <paramref name="write"/> leaves.</summary>
    private static VerificationFacts Verify(string input, SlidesOp operation, Action<Presentation> edit, Action<Presentation> write)
    {
        using var edited = new Presentation(input);
        using var output = new Presentation(input);
        edit(edited);
        write(output);
        SlidesMutationHandlers.ResolvedSlidesOp target = Assert.Single(SlidesMutationHandlers.ResolveBatch(edited, [operation]));
        var verifier = new SlidesEditVerifier(edited, _ => target, evaluation: false);
        verifier.Record(operation, AppliedOperations.Changed(SlidesOp.Catalog, operation, 1));
        return verifier.Verify(output);
    }

    /// <summary>A one-slide deck with a column chart of categories "A B" and "C" and one series "S 1"; returns it and the chart's id.</summary>
    private static (string Input, long ChartId) Chart(SlidesEngineFixture fixture)
    {
        string input = fixture.CreatePresentation("chart.pptx", slides: 1);
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertChartOp
                    {
                        Slide = 1,
                        Kind = SlidesChartKinds.Column,
                        Rect = new SlidesRectInput { X = 50, Y = 120, Width = 400, Height = 300 },
                        Categories = ["A B", "C"],
                        Series = [new SlidesChartSeriesInput { Name = "S 1", Values = [1, 2] }],
                    },
                ],
            },
            Output = TestOutput.At(input, overwrite: true),
        });
        using var presentation = new Presentation(input);
        return (input, presentation.Slides[0].Shapes.OfType<IChart>().Single().OfficeInteropShapeId);
    }
}
