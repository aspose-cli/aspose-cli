using Aspose.Slides;
using Aspose.Slides.Charts;
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
            Slide(13, Shape(1, new(60, 100, 400, 60), "White") with
            {
                Runs = [new SlideTextRunData { Text = "White", Color = "#FFFFFF" }],
                Backdrop = System.Drawing.Color.White,
            }),
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
    public void Findings_NameEachShapeWithItsShapeId()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, Shape(4, new(100, 100, 200, 200), "Body"), Occluder(7, new(100, 100, 200, 200)))],
            Width,
            Height);

        ReviewFinding finding = Assert.Single(analysis.Findings);
        Assert.Contains("'Shape 7' (shapeId 7)", finding.Message, StringComparison.Ordinal);
        Assert.Contains("'Shape 4' (shapeId 4)", finding.Message, StringComparison.Ordinal);
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
    public void SmallOpaqueShapeInFrontOfText_CoversTheText()
    {
        // A badge far smaller than the body still hides the words beneath it.
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, BodyWithLines(1, new(45, 105, 400, 80)), Occluder(2, new(300, 105, 120, 60)))],
            Width,
            Height);

        ReviewFinding finding = Assert.Single(analysis.Findings);
        Assert.Equal(SlidesReviewChecks.TextOverlapsObject.Code, finding.Code);
        Assert.Contains("'Shape 2' (shapeId 2)", finding.Message, StringComparison.Ordinal);
        Assert.Equal(1, analysis.TextOverlaps);
    }

    [Fact]
    public void OpaqueShapeBehindText_DoesNotCoverIt()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(1, Occluder(1, new(300, 105, 120, 60)), BodyWithLines(2, new(45, 105, 400, 80)))],
            Width,
            Height);

        Assert.Empty(analysis.Findings);
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
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(deck), MarkdownPath = markdown });
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
            new PresentationEditRequest { Output = TestOutput.At(edited) });

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
        // Markdown bodies shrink their text on overflow, so rendering fits it to the frame.
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("long-bullets.md");
        File.WriteAllText(markdown, "## Short title\n\n" + string.Concat(Enumerable.Range(1, 12).Select(static item =>
            $"- 培训 Training {item}：每季度一次线下集训，线上课程全年开放 on-demand courses for every partner\n")));
        string deck = fixture.File("long-bullets.pptx");
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(deck), MarkdownPath = markdown });

        Assert.DoesNotContain(Review(fixture, deck).Findings, static finding => finding.Code == SlidesReviewChecks.TextOverflowsShape.Code);
    }

    [Fact]
    public void BodyThatShrinksLongLinesToFit_IsMeasuredWhereItIsDrawn()
    {
        // The engine shrinks these lines to fit the frame; its paragraph rectangles end past the
        // slide's right edge while the runs, as rendered, end well inside it (SLIDES-AUTOFIT-RECT).
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("bilingual-risks.md");
        File.WriteAllText(markdown, "## 风险清单 Risk Register\n\n" + string.Concat(Enumerable.Range(1, 10).Select(static item =>
            $"- 风险 {item}：跨市场数据合规要求不一致导致项目延期 Risk {item}: inconsistent cross-market data compliance delays delivery\n")));
        string deck = fixture.File("bilingual-risks.pptx");
        fixture.Engine.Create(new NewPresentationRequest { Output = TestOutput.At(deck), MarkdownPath = markdown });

        PresentationReadResult read = fixture.Engine.Read(deck, new PresentationReadRequest { Scope = PresentationReadScopes.Full });
        SlideShapeData body = Assert.Single(read.Slides[0].Shapes, static shape => shape.Placeholder == "body");
        SlideRect text = Assert.IsType<SlideRect>(body.TextRect);

        Assert.InRange(text.X + text.Width, body.Rect.X, body.Rect.X + body.Rect.Width);
        Assert.DoesNotContain(Review(fixture, deck).Findings, static finding => finding.Code.StartsWith("SLIDES_TEXT_O", StringComparison.Ordinal));
    }

    [Fact]
    public void TextTheEyeCannotTellFromWhatIsBehindIt_HasTooLittleContrast()
    {
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("contrast.pptx");
        var navy = System.Drawing.Color.FromArgb(0x1B, 0x2A, 0x41);
        using (var presentation = new Presentation())
        {
            ILayoutSlide blank = presentation.LayoutSlides.GetByType(SlideLayoutType.Blank);
            presentation.Slides.RemoveAt(0);
            ISlide[] slides = [.. Enumerable.Range(0, 6).Select(_ => presentation.Slides.AddEmptySlide(blank))];

            // 1: theme-black text on a navy background; 2: white text on the white background.
            slides[0].Background.Type = BackgroundType.OwnBackground;
            slides[0].Background.FillFormat.FillType = FillType.Solid;
            slides[0].Background.FillFormat.SolidFillColor.Color = navy;
            TextBox(slides[0], "Quarterly revenue");
            TextBox(slides[1], "Merged from a dark template", System.Drawing.Color.White);

            // 3: theme text on white; 4: white text on its own navy fill; 5: white text over a navy panel.
            TextBox(slides[2], "Readable");
            IAutoShape filled = TextBox(slides[3], "Badge", System.Drawing.Color.White);
            filled.FillFormat.FillType = FillType.Solid;
            filled.FillFormat.SolidFillColor.Color = navy;
            IAutoShape panel = slides[4].Shapes.AddAutoShape(ShapeType.Rectangle, 20, 80, 600, 200);
            panel.FillFormat.FillType = FillType.Solid;
            panel.FillFormat.SolidFillColor.Color = navy;
            TextBox(slides[4], "On the panel", System.Drawing.Color.White);

            // 6: a chart whose text is stated white, on the white background.
            IChart chart = slides[5].Shapes.AddChart(ChartType.ClusteredColumn, 40, 80, 500, 300);
            chart.TextFormat.PortionFormat.FillFormat.FillType = FillType.Solid;
            chart.TextFormat.PortionFormat.FillFormat.SolidFillColor.Color = System.Drawing.Color.White;
            presentation.Save(deck, SaveFormat.Pptx);
        }

        SlidesReviewAnalysis analysis = Review(fixture, deck);

        ReviewFinding[] findings = [.. analysis.Findings.Where(static finding => finding.Code == SlidesReviewChecks.TextLowContrast.Code)];
        Assert.Equal(["slide 1", "slide 2", "slide 6"], findings.Select(static finding => finding.Location));
        Assert.Contains("#000000 on #1B2A41", findings[0].Message, StringComparison.Ordinal);
        Assert.Contains("shapeId ", findings[1].Message, StringComparison.Ordinal);
        Assert.Equal(3, analysis.LowContrastTexts);
    }

    [Fact]
    public void TextOverTemplateArtOrASmallPatch_IsJudgedByWhatIsReallyBehindIt()
    {
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("template-art.pptx");
        var navy = System.Drawing.Color.FromArgb(0x1B, 0x2A, 0x41);
        using (var presentation = new Presentation())
        {
            ILayoutSlide blank = presentation.LayoutSlides.GetByType(SlideLayoutType.Blank);
            ILayoutSlide titled = presentation.LayoutSlides.GetByType(SlideLayoutType.TitleOnly);
            Bar(presentation.Masters[0], new(0, 0, 720, 60));
            Bar(titled, new(0, 80, 720, 120));
            presentation.Slides.RemoveAt(0);

            // 1: white text on the layout's navy bar; 2: white text on the master's navy bar.
            TextBox(presentation.Slides.AddEmptySlide(titled), "On the layout bar", System.Drawing.Color.White);
            TextBox(presentation.Slides.AddEmptySlide(blank), "On the master bar", System.Drawing.Color.White).Y = 0;

            // 3: theme-black text on white, over a navy patch at its center.
            ISlide patched = presentation.Slides.AddEmptySlide(blank);
            Bar(patched, new(275, 130, 30, 20));
            TextBox(patched, "Mostly on white");

            // 4: white text on the white background is still reported.
            TextBox(presentation.Slides.AddEmptySlide(blank), "Invisible", System.Drawing.Color.White);
            presentation.Save(deck, SaveFormat.Pptx);

            void Bar(IBaseSlide slide, Rect rect)
            {
                IAutoShape bar = slide.Shapes.AddAutoShape(ShapeType.Rectangle, (float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
                bar.FillFormat.FillType = FillType.Solid;
                bar.FillFormat.SolidFillColor.Color = navy;
                bar.LineFormat.FillFormat.FillType = FillType.NoFill;
            }
        }

        SlidesReviewAnalysis analysis = Review(fixture, deck);

        Assert.Equal(
            ["slide 4"],
            analysis.Findings.Where(static finding => finding.Code == SlidesReviewChecks.TextLowContrast.Code).Select(static finding => finding.Location));
    }

    private static IAutoShape TextBox(ISlide slide, string text, System.Drawing.Color? color = null)
    {
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 100, 500, 80, createFromTemplate: false);
        box.AddTextFrame(text);
        if (color is { } stated)
        {
            IPortionFormat format = box.TextFrame.Paragraphs[0].Portions[0].PortionFormat;
            format.FillFormat.FillType = FillType.Solid;
            format.FillFormat.SolidFillColor.Color = stated;
        }

        return box;
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
