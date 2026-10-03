using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// The review adapter declares exactly the checks its analyzer can report, so a new finding
/// cannot ship without a declaration and a declaration cannot outlive its finding.
/// </summary>
public sealed class SlidesReviewCheckTests
{
    private const double Width = 720;
    private const double Height = 540;

    [Fact]
    public void Analyze_EmitsEveryDeclaredCheckAndNothingElse()
    {
        SlideShapeData repeated = Shape(1, new(50, 50, 300, 100), "Repeated content long enough to compare");
        SlideData[] slides =
        [
            Slide(1, repeated),
            Slide(2, repeated),
            Slide(3),
            Slide(4, Shape(1, new(700, 50, 100, 50), "Hi") with
            {
                Runs = [new SlideTextRunData { Text = "Hi", Size = 10 }],
            }),
            Slide(5, [.. Enumerable.Range(1, 18).Select(id => Shape(id, new(id * 30, 400, 20, 20), "Item"))]),
            Slide(6, Shape(1, new(0, 0, 10, 10), "a"), Shape(2, new(20, 0, 10, 10), "b"), Shape(3, new(0, 20, 10, 10), "c")),
            Slide(7, Shape(1, new(100, 100, 200, 200), type: "chart"), Occluder(2, new(100, 100, 100, 200))),
            Slide(8, Shape(1, new(100, 100, 200, 200), "Body"), Occluder(2, new(100, 100, 200, 200))),
            Slide(9, BodyWithLines(1, new(60, 150, 400, 60)), Shape(2, new(60, 195, 600, 280), type: "table")),
            Slide(10, Shape(1, new(60, 100, 600, 300)) with { Placeholder = "body" }),
            Slide(11, TitleWithLines(1, new(40, 22, 640, 58), new(52, -24, 616, 100))),
            Slide(12, TitleWithLines(1, new(400, 100, 200, 40), new(410, 104, 180, 108))),
        ];

        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(slides, Width, Height);

        Assert.Equal(
            SlidesReviewChecks.All.Select(static check => check.Code).Order(StringComparer.Ordinal),
            analysis.Findings.Select(static finding => finding.Code).Distinct().Order(StringComparer.Ordinal));
        // A finding names the view part of its slide, so its evidence is that slide's image; a
        // duplicate concerns two slides and names none.
        Assert.All(analysis.Findings, static finding => Assert.Equal(
            finding.Code == SlidesReviewChecks.SlideDuplicate.Code
                ? null
                : $"slide-{255 + int.Parse(finding.Location!["slide ".Length..], System.Globalization.CultureInfo.InvariantCulture)}",
            finding.Part));
    }

    [Fact]
    public void TextAboveATable_IsNotAnOverlap()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, BodyWithLines(1, new(60, 120, 400, 60)), Shape(2, new(60, 195, 600, 280), type: "table"))],
            Width,
            Height);

        Assert.DoesNotContain(analysis.Findings, static finding => finding.Code == SlidesReviewChecks.TextOverlapsObject.Code);
    }

    [Fact]
    public void TextEvaluationModeReplaced_DoesNotMakeASlideDense()
    {
        // Evaluation mode replaces every text longer than five characters with its start and the
        // SDK's truncation marker, so a 25-cell table reads as about 1,500 characters.
        string cells = string.Join(' ', Enumerable.Repeat($"¥1,20... {SlidesEngineSupport.EvaluationTruncationMarker}.", 25));
        SlideData slide = Slide(
            1,
            Shape(1, new(40, 22, 640, 58), $"投资与回报... {SlidesEngineSupport.EvaluationTruncationMarker}."),
            Shape(2, new(40, 100, 640, 144), cells, type: "table"));
        Assert.True(slide.Shapes.Sum(static shape => shape.Text!.Length) >= 1400);

        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze([slide], Width, Height);

        Assert.DoesNotContain(analysis.Findings, static finding => finding.Code == SlidesReviewChecks.ContentDensityHigh.Code);
        Assert.Equal(0, analysis.HighDensitySlides);
    }

    [Fact]
    public void ManyObjects_MakeASlideDenseEvenWhenEvaluationModeReplacedTheirText()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, [.. Enumerable.Range(1, 18).Select(id => Shape(id, new(id * 30, 400, 20, 20), $"Item ... {SlidesEngineSupport.EvaluationTruncationMarker}."))])],
            Width,
            Height);

        Assert.Contains(analysis.Findings, static finding => finding.Code == SlidesReviewChecks.ContentDensityHigh.Code);
    }

    [Fact]
    public void TextRect_IsWhereTheTextIsLaidOutOnTheSlide()
    {
        using var presentation = new Aspose.Slides.Presentation();
        Aspose.Slides.IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(Aspose.Slides.ShapeType.Rectangle, 100, 150, 400, 300);
        box.TextFrame.Text = "A summary line";
        box.TextFrame.TextFrameFormat.AnchoringType = Aspose.Slides.TextAnchorType.Top;

        SlideRect rect = Assert.IsType<SlideRect>(Engine.Mapping.SlidesReviewProjection.TextRect(box));

        Assert.InRange(rect.Y, 150, 170);
        Assert.InRange(rect.Height, 10, 60);
        Assert.True(rect.X >= 100 && rect.X + rect.Width <= 500);
    }

    [Fact]
    public void TextPushedAboveTheSlide_IsTextOutsideTheSlideOnly()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, TitleWithLines(1, new(40, 22, 640, 58), new(52, -24, 616, 100)))],
            Width,
            Height);

        ReviewFinding finding = Assert.Single(analysis.Findings);
        Assert.Equal(SlidesReviewChecks.TextOutsideSlide.Code, finding.Code);
        Assert.Contains("top", finding.Message, StringComparison.Ordinal);
        Assert.Equal(1, analysis.TextOutsideSlide);
    }

    [Fact]
    public void TextSpillingOutOfItsShapeInsideTheSlide_IsTextOverflowingTheShape()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, TitleWithLines(1, new(400, 100, 200, 40), new(410, 104, 180, 108)))],
            Width,
            Height);

        ReviewFinding finding = Assert.Single(analysis.Findings);
        Assert.Equal(SlidesReviewChecks.TextOverflowsShape.Code, finding.Code);
        Assert.Equal(1, analysis.TextOverflows);
    }

    [Fact]
    public void TextThatFitsOrResizesItsShape_IsNotReported()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [
                Slide(1,
                    TitleWithLines(1, new(40, 22, 640, 58), new(47, 43, 316, 34)),
                    TitleWithLines(2, new(100, 300, 200, 30), new(60, 296, 280, 38)) with { TextAutofits = true }),
            ],
            Width,
            Height);

        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void LongTitleGrowingAboveTheSlide_IsReportedOnlyAfterItIsSet()
    {
        // The default design anchors the title at the bottom of a one-line frame near the top
        // edge, so a title that wraps onto three lines grows upward off the slide.
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("outline.md");
        File.WriteAllText(markdown, "# Proposal\n\nSales team\n\n## Next steps\n\n- Confirm the pilot site\n- Sign the letter of intent\n");
        string deck = fixture.File("deck.pptx");
        string edited = fixture.File("deck.long-title.pptx");
        fixture.Engine.Create(new NewPresentationRequest { MarkdownPath = markdown, OutputPath = deck });
        fixture.Engine.ApplyOps(
            deck,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new SetTitleOp
                    {
                        Slide = 2,
                        Text = "下一步 Next steps：确认试点仓库、签署意向书 LOI、启动需求调研（两周内完成） within two weeks of approval by the steering committee",
                    },
                ],
            },
            new PresentationEditRequest { OutputPath = edited });

        SlidesReviewAnalysis before = Review(fixture, deck);
        SlidesReviewAnalysis after = Review(fixture, edited);

        Assert.DoesNotContain(before.Findings, static finding => finding.Code.StartsWith("SLIDES_TEXT_O", StringComparison.Ordinal));
        ReviewFinding finding = Assert.Single(after.Findings, static finding => finding.Code == SlidesReviewChecks.TextOutsideSlide.Code);
        Assert.Equal("slide 2", finding.Location);
        Assert.Contains("top edge", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(after.Findings, static finding => finding.Code == SlidesReviewChecks.ShapeOutsideSlide.Code);
    }

    [Fact]
    public void BodyThatShrinksTextOnOverflow_IsNotReportedAsOverflowing()
    {
        // Markdown bodies shrink their text on overflow, which rendering applies but the
        // engine's paragraph layout does not, so the laid-out lines run past the frame.
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("long-bullets.md");
        File.WriteAllText(markdown, "## Short title\n\n" + string.Concat(Enumerable.Range(1, 12).Select(static item =>
            $"- 培训 Training {item}：每季度一次线下集训，线上课程全年开放 on-demand courses for every partner\n")));
        string deck = fixture.File("long-bullets.pptx");
        fixture.Engine.Create(new NewPresentationRequest { MarkdownPath = markdown, OutputPath = deck });

        Assert.DoesNotContain(Review(fixture, deck).Findings, static finding => finding.Code == SlidesReviewChecks.TextOverflowsShape.Code);
    }

    [Fact]
    public void EvaluationWatermarkOverATable_IsLeftOutOnlyByAnEvaluationReview()
    {
        // A box shaped like the watermark an evaluation save adds, which an evaluation save adds
        // once more: an evaluation review leaves both out, a licensed review judges the copy.
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("watermarked.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 100, 400, 80);
            box.FillFormat.FillType = FillType.NoFill;
            box.TextFrame.Text = "Evaluation only.\nCreated with Aspose.Slides.";
            box.ShapeLock.SelectLocked = true;
            box.ShapeLock.PositionLocked = true;
            slide.Shapes.AddTable(100, 100, [200, 200], [100, 100]);
            presentation.Save(deck, SaveFormat.Pptx);
        }

        SlidesReviewAnalysis analysis = Review(fixture, deck);

        if (fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Equal(0, analysis.ExcludedEvaluationWatermarks);
            Assert.Contains(analysis.Findings, static finding => finding.Code == SlidesReviewChecks.TextOverlapsObject.Code);
            return;
        }

        Assert.Equal(2, analysis.ExcludedEvaluationWatermarks);
        Assert.Empty(analysis.Findings);
    }

    private static SlidesReviewAnalysis Review(SlidesEngineFixture fixture, string path)
    {
        PresentationSummary info = fixture.Engine.GetInfo(path, new PresentationInfoRequest()).Presentation;
        PresentationReadResult read = fixture.Engine.Read(path, new PresentationReadRequest { Scope = PresentationReadScopes.Full });
        return SlidesReviewAnalyzer.Analyze(read.Slides, info.WidthPoints, info.HeightPoints);
    }

    private static SlideShapeData TitleWithLines(long id, Rect frame, Rect lines) =>
        Shape(id, frame, "A title long enough to wrap onto several lines") with
        {
            ShapeName = "Title",
            Placeholder = "title",
            TextRect = new SlideRect { X = lines.X, Y = lines.Y, Width = lines.Width, Height = lines.Height },
        };

    private static SlideShapeData BodyWithLines(long id, Rect lines) =>
        Shape(id, new(lines.X, lines.Y, lines.Width, 350), "Two lines of summary text above the table") with
        {
            Placeholder = "body",
            TextRect = new SlideRect { X = lines.X, Y = lines.Y, Width = lines.Width, Height = lines.Height },
        };

    private static SlideData Slide(int number, params SlideShapeData[] shapes) => new()
    {
        Slide = number,
        SlideId = (uint)(255 + number),
        Shapes = shapes,
        ContentTruncated = false,
    };

    private static SlideShapeData Shape(long id, Rect rect, string? text = null, string type = "shape") => new()
    {
        ShapeId = id,
        ShapeName = $"Shape {id}",
        Type = type,
        Text = text,
        Rect = new SlideRect { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height },
        ZOrder = (int)id,
    };

    private static SlideShapeData Occluder(long id, Rect rect) => Shape(id, rect) with { HasOpaqueFill = true };

    private readonly record struct Rect(double X, double Y, double Width, double Height);
}
