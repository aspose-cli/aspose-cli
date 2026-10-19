using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.TestKit;
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
        SlidesReviewShape repeated = Shape(1, new(50, 50, 300, 100), "Repeated content long enough to compare");
        SlidesReviewSlide[] slides =
        [
            Slide(1, repeated),
            Slide(2, repeated),
            Slide(3),
            Slide(4, Shape(1, new(700, 50, 100, 50), "Hi") with
            {
                Runs = [new SlidesReviewRun { FontSize = 10 }],
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
                Runs = [new SlidesReviewRun { Color = "#FFFFFF" }],
                Backdrop = System.Drawing.Color.White,
            }),
            Slide(14, Shape(1, new(100, 100, 400, 80), "Evaluation only.") with { EvaluationWatermark = true }),
        ];

        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(slides, Width, Height);

        Assert.Equal(
            SlidesReviewChecks.All.Select(static check => check.Code).Order(StringComparer.Ordinal),
            analysis.Findings.Select(static finding => finding.Code).Distinct().Order(StringComparer.Ordinal));
        // A finding names the view part of its slide, so its evidence is that slide's image; a
        // duplicate names both slides.
        Assert.All(analysis.Findings, static finding => Assert.Equal(
            finding.Code == SlidesReviewChecks.SlideDuplicate.Code
                ? ["slide-256", "slide-257"]
                : [$"slide-{255 + int.Parse(finding.Location!["slide ".Length..], System.Globalization.CultureInfo.InvariantCulture)}"],
            finding.Parts!));
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
        string cells = string.Join(' ', Enumerable.Repeat($"¥1,20... {SlidesEvaluationMarks.EvaluationTruncationMarker}.", 25));
        SlidesReviewSlide slide = Slide(
            1,
            Shape(1, new(40, 22, 640, 58), $"投资与回报... {SlidesEvaluationMarks.EvaluationTruncationMarker}."),
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
            [Slide(1, [.. Enumerable.Range(1, 18).Select(id => Shape(id, new(id * 30, 400, 20, 20), $"Item ... {SlidesEvaluationMarks.EvaluationTruncationMarker}."))])],
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

        SlidesRect rect = Assert.IsType<SlidesRect>(SlidesReviewProjection.TextRect(box));

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
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(deck), Markdown = new SourceFile("--from-markdown", markdown) });
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = deck,
            Batch = new SlidesOpsBatch
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
            Output = TestOutput.At(edited),
        });

        ProductReviewAssessment before = fixture.Review(deck);
        ProductReviewAssessment after = fixture.Review(edited);

        Assert.DoesNotContain(before.Findings!, static finding => finding.Code.StartsWith("SLIDES_TEXT_O", StringComparison.Ordinal));
        ReviewFinding finding = Assert.Single(after.Findings!, static finding => finding.Code == SlidesReviewChecks.TextOutsideSlide.Code);
        Assert.Equal("slide 2", finding.Location);
        Assert.Contains("top edge", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(after.Findings!, static finding => finding.Code == SlidesReviewChecks.ShapeOutsideSlide.Code);
    }

    [Fact]
    public void TextWhoseCharactersReachAFewPointsPastItsShape_IsReported()
    {
        // Four lines of 14 pt text, 67.2 pt of lines, in a 60 pt box: the first and last lines
        // run 3.6 pt past its top and bottom edges, and the last line's characters onto its
        // outline; at the top, the room above the tallest Latin letters is not counted.
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("note.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = fixture.CreatePresentation("blank.pptx", slides: 1),
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertShapeOp
                    {
                        Slide = 1,
                        Geometry = SlidesShapeGeometries.Rectangle,
                        Rect = new SlidesRectInput { X = 460, Y = 110, Width = 220, Height = 60 },
                        Text = "备注 Note：华北 North 区整改率偏低，主要原因是 2026 年 Q2 组织架构调整 reorg 导致负责人变更，预计 Q4 末追平 catch up。",
                        Style = new SlidesShapeStyleInput { FillColor = "#FFFFFF", Color = "#333333", FontSize = 14 },
                    },
                ],
            },
            Output = TestOutput.At(deck),
        });

        ProductReviewAssessment review = fixture.Review(deck);

        ReviewFinding finding = Assert.Single(review.Findings!, static finding => finding.Code == SlidesReviewChecks.TextOverflowsShape.Code);
        Assert.Contains("rectangle", finding.Message, StringComparison.Ordinal);
        Assert.Contains("of the bottom of its shape", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LineThatFillsATightBoxWithItsCharactersInside_IsNotReported()
    {
        // A 14 pt line is 16.8 pt high; in a 15 pt box without margins its line spacing reaches
        // 0.9 pt past the top and bottom edges while its characters stay inside.
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("tight.pptx");
        using (var presentation = new Presentation())
        {
            IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 100, 200, 300, 15);
            box.TextFrame.Text = "Q3 up";
            ITextFrameFormat format = box.TextFrame.TextFrameFormat;
            format.AutofitType = TextAutofitType.None;
            format.AnchoringType = TextAnchorType.Center;
            format.MarginTop = 0;
            format.MarginBottom = 0;
            box.TextFrame.Paragraphs[0].Portions[0].PortionFormat.FontHeight = 14;
            presentation.Save(deck, SaveFormat.Pptx);
        }

        using (var saved = new Presentation(deck))
        {
            IShape shape = saved.Slides[0].Shapes[0];
            SlidesRect lines = Assert.IsType<SlidesRect>(SlidesReviewProjection.TextRect(shape));
            Assert.True(lines.Y < shape.Y && lines.Y + lines.Height > shape.Y + shape.Height, "The line spacing reaches past the box.");
        }

        Assert.DoesNotContain(fixture.Review(deck).Findings!, static finding => finding.Code.StartsWith("SLIDES_TEXT_O", StringComparison.Ordinal));
    }

    /// <summary>
    /// Three lines in one paragraph whose line rectangles reach past a box without margins while
    /// their rendered characters stay inside it, as rendered at 8x and measured, are not
    /// reported: a 28 pt word on the middle line of 14 pt lines, whose rectangles misstate the
    /// lines, in a 65.6 pt box; and 14 pt lines at 90% line spacing in a box 2 pt shorter than
    /// their rectangles.
    /// </summary>
    [Theory]
    [InlineData(28f, 100f, 65.6f)]
    [InlineData(14f, 90f, 0f)]
    public void LinesWhoseRectanglesAloneLeaveATightBox_AreNotReported(float middleWordSize, float lineSpacing, float boxHeight)
    {
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("spacing.pptx");
        using (var presentation = new Presentation())
        {
            IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 100, 100, 200, 300);
            box.FillFormat.FillType = FillType.NoFill;
            box.LineFormat.FillFormat.FillType = FillType.NoFill;
            ITextFrameFormat format = box.TextFrame.TextFrameFormat;
            format.AutofitType = TextAutofitType.None;
            format.AnchoringType = TextAnchorType.Center;
            format.MarginTop = 0;
            format.MarginBottom = 0;
            IParagraph paragraph = box.TextFrame.Paragraphs[0];
            paragraph.Portions.Clear();
            paragraph.ParagraphFormat.SpaceWithin = lineSpacing;
            // Soft line breaks keep the three lines in one paragraph.
            foreach ((string text, float size) in new[] { ("Fjgy line\v", 14f), ("Bjg", middleWordSize), ("\vLjgy line", 14f) })
            {
                var portion = new Portion(text);
                portion.PortionFormat.FontHeight = size;
                portion.PortionFormat.FillFormat.FillType = FillType.Solid;
                portion.PortionFormat.FillFormat.SolidFillColor.Color = System.Drawing.Color.Black;
                paragraph.Portions.Add(portion);
            }

            Assert.Equal(3, paragraph.GetLinesCount());
            box.Height = boxHeight > 0 ? boxHeight : (float)SlidesReviewProjection.TextRect(box)!.Height - 2;
            box.Y = 250 - (box.Height / 2);
            presentation.Save(deck, SaveFormat.Pptx);
        }

        using (var saved = new Presentation(deck))
        {
            IShape shape = saved.Slides[0].Shapes[0];
            SlidesRect lines = Assert.IsType<SlidesRect>(SlidesReviewProjection.TextRect(shape));
            double past = Math.Max(shape.Y - lines.Y, lines.Y + lines.Height - (shape.Y + shape.Height));
            Assert.InRange(past, 0.5, 4);
        }

        Assert.DoesNotContain(fixture.Review(deck).Findings!, static finding => finding.Code.StartsWith("SLIDES_TEXT_O", StringComparison.Ordinal));
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
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(deck), Markdown = new SourceFile("--from-markdown", markdown) });

        Assert.DoesNotContain(fixture.Review(deck).Findings!, static finding => finding.Code == SlidesReviewChecks.TextOverflowsShape.Code);
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
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(deck), Markdown = new SourceFile("--from-markdown", markdown) });

        using (var saved = new Presentation(deck))
        {
            IShape body = Assert.Single(saved.Slides[0].Shapes, static shape => SlidesPlaceholders.Role(shape.Placeholder?.Type) == "body");
            SlidesRect text = Assert.IsType<SlidesRect>(SlidesReviewProjection.TextRect(body));
            Assert.InRange(text.X + text.Width, body.X, body.X + body.Width);
        }

        Assert.DoesNotContain(fixture.Review(deck).Findings!, static finding => finding.Code.StartsWith("SLIDES_TEXT_O", StringComparison.Ordinal));
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

        ProductReviewAssessment review = fixture.Review(deck);

        ReviewFinding[] findings = [.. review.Findings!.Where(static finding => finding.Code == SlidesReviewChecks.TextLowContrast.Code)];
        Assert.Equal(["slide 1", "slide 2", "slide 6"], findings.Select(static finding => finding.Location));
        Assert.Contains("#000000 on #1B2A41", findings[0].Message, StringComparison.Ordinal);
        Assert.Contains("shapeId ", findings[1].Message, StringComparison.Ordinal);
        Assert.Equal(3, SlidesEngineFixture.Metric(review, "lowContrastTexts"));
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

        ProductReviewAssessment review = fixture.Review(deck);

        Assert.Equal(
            ["slide 4"],
            review.Findings!.Where(static finding => finding.Code == SlidesReviewChecks.TextLowContrast.Code).Select(static finding => finding.Location));
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
    public void EvaluationWatermarkOverATable_IsReported_AndLeftOutOfTheOtherChecksOnlyInEvaluationMode()
    {
        // A box shaped like the watermark an evaluation save adds, which an evaluation save adds
        // once more: every review reports the watermark; evaluation mode alone leaves it out of
        // the other checks and counts it.
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("watermarked.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 100, 400, 80);
            box.FillFormat.FillType = FillType.NoFill;
            // The watermark's notice is two paragraphs, which evaluation mode reads cut short each.
            box.TextFrame.Text = "Evaluation only.";
            box.TextFrame.Paragraphs.Add(new Paragraph { Text = "Created with Aspose.Slides." });
            box.ShapeLock.SelectLocked = true;
            box.ShapeLock.PositionLocked = true;
            slide.Shapes.AddTable(100, 100, [200, 200], [100, 100]);
            presentation.Save(deck, SaveFormat.Pptx);
        }

        ProductReviewAssessment review = fixture.Review(deck);

        if (fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Null(SlidesEngineFixture.Metric(review, "excludedEvaluationMarks"));
            Assert.Contains(review.Findings!, static finding => finding.Code == "SLIDES_EVALUATION_WATERMARK");
            Assert.Contains(review.Findings!, static finding => finding.Code != "SLIDES_EVALUATION_WATERMARK");
        }
        else
        {
            Assert.Equal(2, SlidesEngineFixture.Metric(review, "excludedEvaluationMarks"));
            ReviewFinding finding = Assert.Single(review.Findings!);
            Assert.Equal("SLIDES_EVALUATION_WATERMARK", finding.Code);
        }
    }

    [Fact]
    public void LockedBoxThatOnlyStartsWithEvaluation_IsNeverTakenForTheWatermark()
    {
        // Evaluation mode reads every run as its first characters, so only the watermark's two
        // runs, "Evaluation only." and "Created with Aspose.Slides", identify it there.
        using var fixture = new SlidesEngineFixture();
        string deck = fixture.File("criteria.pptx");
        using (var presentation = new Presentation())
        {
            IAutoShape box = presentation.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 100, 100, 400, 80);
            box.TextFrame.Text = "Evaluation criteria for the review";
            box.ShapeLock.SelectLocked = true;
            box.ShapeLock.PositionLocked = true;
            presentation.Save(deck, SaveFormat.Pptx);
        }

        ProductReviewAssessment review = fixture.Review(deck);

        // An evaluation save of the test deck adds the one real watermark.
        Assert.Equal(fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed ? null : 1, SlidesEngineFixture.Metric(review, "excludedEvaluationMarks"));
    }

    /// <summary>
    /// A deck with one or more of each layout problem: the review's findings and metrics through
    /// the view adapter, as they stood before review got its own layout projection.
    /// </summary>
    [Fact]
    public void Review_OfAFixedDeck_ReportsTheSameFindingsAndMetrics()
    {
        using var fixture = new SlidesEngineFixture();
        Sdk.Licensing.LicenseState license = fixture.LicenseState;
        string deck = fixture.File("fixed.pptx");
        using (var presentation = new Presentation())
        {
            ILayoutSlide blank = presentation.LayoutSlides.GetByType(SlideLayoutType.Blank);
            ILayoutSlide content = presentation.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject);
            presentation.Slides.RemoveAt(0);

            // 1: a headline, a 10 pt footnote and a box past the right edge; 2: a copy of it.
            ISlide first = presentation.Slides.AddEmptySlide(blank);
            TextBox(first, "Quarterly revenue grew in every region this year");
            IAutoShape footnote = first.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 470, 300, 30, createFromTemplate: false);
            footnote.AddTextFrame("Source: internal finance data");
            footnote.TextFrame.Paragraphs[0].Portions[0].PortionFormat.FontHeight = 10;
            first.Shapes.AddAutoShape(ShapeType.Rectangle, 600, 300, 200, 100).TextFrame.Text = "Outside";
            presentation.Slides.AddClone(first);

            // 3: blank; 4: a chart under an opaque box, and text over a table.
            presentation.Slides.AddEmptySlide(blank);
            ISlide covered = presentation.Slides.AddEmptySlide(blank);
            covered.Shapes.AddChart(ChartType.ClusteredColumn, 100, 40, 400, 300);
            IAutoShape box = covered.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 40, 300, 300);
            box.FillFormat.FillType = FillType.Solid;
            box.FillFormat.SolidFillColor.Color = System.Drawing.Color.Gray;
            covered.Shapes.AddTable(60, 400, [300, 300], [40, 40]);
            IAutoShape over = covered.Shapes.AddAutoShape(ShapeType.Rectangle, 60, 400, 400, 40, createFromTemplate: false);
            over.AddTextFrame("Text laid over the table");
            over.FillFormat.FillType = FillType.NoFill;

            // 5: white text on white, and a box hidden under an opaque one; 6: a title with an empty body placeholder.
            ISlide white = presentation.Slides.AddEmptySlide(blank);
            TextBox(white, "Invisible white words", System.Drawing.Color.White);
            white.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 300, 200, 100).TextFrame.Text = "Hidden body";
            IAutoShape lid = white.Shapes.AddAutoShape(ShapeType.Rectangle, 100, 300, 200, 100);
            lid.FillFormat.FillType = FillType.Solid;
            lid.FillFormat.SolidFillColor.Color = System.Drawing.Color.Gray;
            ISlide agenda = presentation.Slides.AddEmptySlide(content);
            agenda.Shapes.OfType<IAutoShape>().First(static shape => shape.Placeholder?.Type is PlaceholderType.Title or PlaceholderType.CenteredTitle).TextFrame.Text = "Agenda";

            // 7: eighteen objects; 8: text spilling out of a fixed box; 9: three dots in a corner.
            ISlide dense = presentation.Slides.AddEmptySlide(blank);
            for (int item = 1; item <= 18; item++)
            {
                dense.Shapes.AddAutoShape(ShapeType.Rectangle, 20 + (item * 35), 300, 30, 30).TextFrame.Text = $"I{item}";
            }

            IAutoShape spill = presentation.Slides.AddEmptySlide(blank).Shapes.AddAutoShape(ShapeType.Rectangle, 100, 200, 200, 30);
            spill.TextFrame.Text = "A sentence far too long to fit on one line of this narrow box, which wraps onto several.";
            spill.TextFrame.TextFrameFormat.AutofitType = TextAutofitType.None;
            ISlide corner = presentation.Slides.AddEmptySlide(blank);
            foreach (int offset in new[] { 0, 15, 30 })
            {
                corner.Shapes.AddAutoShape(ShapeType.Rectangle, offset, 0, 10, 10).TextFrame.Text = "a";
            }

            presentation.Save(deck, SaveFormat.Pptx);
        }

        ProductReviewAssessment assessment = fixture.Review(deck);

        string actual = string.Join(
            "\n",
            assessment.Findings!.Select(static finding =>
                $"{finding.Code}|{finding.Location}|{finding.Message}|{finding.Hint}|{string.Join(",", finding.Parts ?? [])}")
                .Concat(assessment.Coverage!.Select(static metric => $"{metric.Name}={metric.Value} {metric.Unit}")));
        string expected = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "tests",
            "Aspose.Cli.Product.Slides.Tests",
            "Assets",
            license == Sdk.Licensing.LicenseState.Licensed ? "review-fixed-deck.licensed.txt" : "review-fixed-deck.evaluation.txt"));
        Assert.Equal(expected.ReplaceLineEndings("\n").TrimEnd('\n'), actual);
    }

    private static SlidesReviewShape TitleWithLines(long id, Rect frame, Rect lines) =>
        Shape(id, frame, "A title long enough to wrap onto several lines") with
        {
            ShapeName = "Title",
            Placeholder = "title",
            TextRect = new SlidesRect { X = lines.X, Y = lines.Y, Width = lines.Width, Height = lines.Height },
        };

    private static SlidesReviewShape BodyWithLines(long id, Rect lines) =>
        Shape(id, new(lines.X, lines.Y, lines.Width, 350), "Two lines of summary text above the table") with
        {
            Placeholder = "body",
            TextRect = new SlidesRect { X = lines.X, Y = lines.Y, Width = lines.Width, Height = lines.Height },
        };

    private static SlidesReviewSlide Slide(int number, params SlidesReviewShape[] shapes) => new()
    {
        Slide = number,
        SlideId = (uint)(255 + number),
        Shapes = shapes,
    };

    private static SlidesReviewShape Shape(long id, Rect rect, string? text = null, string type = "shape") => new()
    {
        ShapeId = id,
        ShapeName = $"Shape {id}",
        Kind = type,
        Text = text,
        Rect = new SlidesRect { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height },
        ZOrder = (int)id,
    };

    private static SlidesReviewShape Occluder(long id, Rect rect) => Shape(id, rect) with { HasOpaqueFill = true };

    private readonly record struct Rect(double X, double Y, double Width, double Height);

    [Fact]
    public void EmptyPlaceholder_HintNamesTheOperationsThatFillOrDeleteIt()
    {
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            [Slide(2, Shape(5, new(60, 120, 400, 200)) with { Placeholder = "body" })],
            Width,
            Height);

        ReviewFinding finding = Assert.Single(analysis.Findings, static finding => finding.Code == SlidesReviewChecks.PlaceholderEmpty.Code);
        Assert.Contains("\"op\":\"set_text\",\"slide\":2,\"shapeId\":5", finding.Hint, StringComparison.Ordinal);
        Assert.Contains("\"op\":\"delete_shape\",\"slide\":2,\"shapeId\":5", finding.Hint, StringComparison.Ordinal);
    }
}
