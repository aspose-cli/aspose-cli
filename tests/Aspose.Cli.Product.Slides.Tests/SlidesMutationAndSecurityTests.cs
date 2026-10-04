using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesMutationAndSecurityTests
{
    [Theory]
    [InlineData(false, "1-3")]
    [InlineData(true, "1-3")]
    [InlineData(false, "1")]
    [InlineData(true, "1")]
    [InlineData(false, "2-3")]
    [InlineData(true, "2-3")]
    public void DeleteSlides_ValidatesEveryResolvedTargetBeforeMutating(bool bestEffort, string secondRange)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("deletions.pptx", slides: 3);
        byte[] original = File.ReadAllBytes(input);
        string output = fixture.File("deletions.out.pptx");
        var batch = new SlidesOpsBatch
        {
            Ops = [new DeleteSlidesOp { Slides = "1" }, new DeleteSlidesOp { Slides = secondRange }],
        };
        var request = new PresentationEditRequest
        {
            OutputPath = output,
            Options = new EditCommandOptions { BestEffort = bestEffort },
        };
        if (bestEffort)
        {
            SlidesEditResult result = fixture.Engine.ApplyOps(input, batch, request);
            Assert.Equal(["ok", "failed"], result.Applied.Select(static outcome => outcome.Status));
            Assert.Equal(1, result.Applied[0].ItemsAffected);
            Assert.Equal(0, result.Applied[1].ItemsAffected);
            Assert.Equal(2, fixture.Engine.GetInfo(output, new PresentationInfoRequest()).Presentation.SlideCount);
        }
        else
        {
            Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(input, batch, request));
            Assert.False(File.Exists(output));
        }
        Assert.Equal(original, File.ReadAllBytes(input));
    }
    [Fact]
    public void PasswordProtectedPresentation_RequiresTheCorrectPassword()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(
            "protected.pptx",
            slides: 1,
            password: "correct");

        CliException missing = Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(input, new PresentationInfoRequest()));
        CliException wrong = Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(
                input,
                new PresentationInfoRequest { Password = "wrong" }));
        PresentationInfoResult opened = fixture.Engine.GetInfo(
            input,
            new PresentationInfoRequest { Password = "correct" });

        Assert.Equal(ErrorCodes.PasswordRequired, missing.Code);
        Assert.Equal(ErrorCodes.PasswordInvalid, wrong.Code);
        Assert.Single(opened.Slides);
    }

    [Fact]
    public void RemoteMarkdownImage_IsBlockedBeforeWriting()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("remote.md");
        string output = fixture.File("remote.pptx");
        File.WriteAllText(
            markdown,
            "# Remote\n\n![hero](https://example.test/hero.png)");

        CliException error = Assert.Throws<CliException>(() =>
            fixture.Engine.Create(new NewPresentationRequest
            {
                OutputPath = output,
                MarkdownPath = markdown,
            }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void ContinueOnError_PersistsSuccessesAndReportsTheFailure()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        uint[] slideIds;
        using (var baseline = new Presentation(input))
        {
            slideIds = baseline.Slides.Select(static slide => slide.SlideId).ToArray();
        }
        string atomicOutput = fixture.File("atomic.pptx");
        string output = fixture.File("partial.pptx");
        var batch = new SlidesOpsBatch
        {
            Ops =
            [
                new AddSectionOp { Name = "Results", StartSlide = 1 },
                new AddSectionOp { Name = "Results", StartSlide = 2 },
                new SetNotesOp { Slide = 1, Text = "Saved" },
            ],
        };

        Assert.ThrowsAny<Exception>(() => fixture.Engine.ApplyOps(
            input,
            batch,
            new PresentationEditRequest { OutputPath = atomicOutput }));
        Assert.False(File.Exists(atomicOutput));

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            batch,
            new PresentationEditRequest
            {
                OutputPath = output,
                Options = new EditCommandOptions { BestEffort = true },
            });

        Assert.Equal(["ok", "failed", "ok"],
            result.Applied.Select(static operation => operation.Status));
        Assert.Equal(ErrorCodes.OpsInvalid.Name, result.Applied[1].Error!.Code);
        Assert.Equal([$"slide/{slideIds[0]}"], result.Applied[0].Targets);
        Assert.Equal([$"slide/{slideIds[1]}"], result.Applied[1].Targets);
        Assert.Equal([$"slide/{slideIds[0]}"], result.Applied[2].Targets);
        Assert.True(File.Exists(output));
        using var reopened = new Presentation(output);
        Assert.Contains(reopened.Sections, static section => section.Name == "Results");
        Assert.Contains(
            "Saved",
            reopened.Slides[0].NotesSlideManager.NotesSlide!.NotesTextFrame!.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EncryptedEdit_VerifiesAndReopensWithTheNewPassword()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation();
        string output = fixture.File("encrypted.pptx");

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops = [new SetNotesOp { Slide = 1, Text = "Safe" }],
            },
            new PresentationEditRequest
            {
                OutputPath = output,
                EncryptPassword = "correct",
            });

        Assert.Equal(ErrorCodes.PasswordRequired, Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(output, new PresentationInfoRequest())).Code);
        Assert.Equal(3, fixture.Engine.GetInfo(
            output,
            new PresentationInfoRequest { Password = "correct" }).Presentation.SlideCount);
        PresentationReadResult read = fixture.Engine.Read(
            output,
            new PresentationReadRequest
            {
                Password = "correct",
                Slides = PageRange.Parse("1"),
                Scope = PresentationReadScopes.Full,
                IncludeNotes = true,
            });
        Assert.NotNull(read.Slides[0].Notes);
        Assert.Contains("Safe", read.Slides[0].Notes!, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuralTextObjectAndPresentationOps_RoundTripTogether()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("operations.pptx", slides: 3);
        string output = fixture.File("operations.out.pptx");
        // Evaluation mode refuses replace_text over the titles it reads cut short.
        bool licensed = fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed;

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new AddSectionOp { Name = "Results", StartSlide = 2 },
                    .. licensed ? [new SlidesReplaceTextOp { Find = "Slide", Replace = "Q" }] : Array.Empty<SlidesOp>(),
                    new InsertShapeOp
                    {
                        Slide = 1,
                        Kind = "rounded-rectangle",
                        Rect = new SlidesRectInput { X = 80, Y = 140, Width = 240, Height = 80 },
                        Text = "Key",
                    },
                    new SlidesSetPropertiesOp { Title = "Q4", Author = "CLI" },
                ],
            },
            new PresentationEditRequest { OutputPath = output });

        Assert.All(result.Applied, static operation => Assert.Equal("ok", operation.Status));
        Assert.All(result.Applied, static operation => Assert.NotEmpty(operation.Targets));
        using var reopened = new Presentation(output);
        Assert.Contains(reopened.Sections, static section => section.Name == "Results");
        Assert.Equal("Q4", reopened.DocumentProperties.Title);
        string[] text = reopened.Slides[0].Shapes
            .OfType<IAutoShape>()
            .Where(static shape => shape.TextFrame is not null)
            .Select(static shape => shape.TextFrame.Text)
            .ToArray();
        Assert.Equal(licensed, text.Any(static value => value.StartsWith("Q", StringComparison.Ordinal)));
        Assert.Contains(text, static value => value.Contains("Key", StringComparison.Ordinal));
    }

    [Fact]
    public void SetFooter_TargetsOnlySlidesWhoseLayoutShowsAFooter()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("footerless-source.pptx");
        string output = fixture.File("footerless-output.pptx");
        uint shown;
        using (var presentation = new Presentation())
        {
            // A title layout often hides footers by having no footer, number or date placeholder.
            ILayoutSlide footerless = presentation.LayoutSlides[0];
            foreach (IShape shape in footerless.Shapes.Where(static shape => shape.Placeholder?.Type
                         is PlaceholderType.Footer or PlaceholderType.SlideNumber or PlaceholderType.DateAndTime).ToArray())
            {
                footerless.Shapes.Remove(shape);
            }

            shown = presentation.Slides.AddEmptySlide(presentation.LayoutSlides[1]).SlideId;
            presentation.Slides[0].LayoutSlide = footerless;
            presentation.Save(input, Aspose.Slides.Export.SaveFormat.Pptx);
        }

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch { Ops = [new SetFooterOp { Text = "Confidential", ShowNumber = true }] },
            new PresentationEditRequest { OutputPath = output });

        BoundedOperationOutcome applied = Assert.Single(result.Applied);
        Assert.Equal(1, applied.ItemsAffected);
        Assert.Equal([$"slide/{shown}"], applied.Targets);
        Assert.Equal([shown], result.SlidesTouched);
    }

    [Fact]
    public void SetFooter_ActivatesLayoutPlaceholdersInsideAScaledCanvasAndKeepsExplicitGeometry()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("footer-source.pptx");
        string output = fixture.File("footer-output.pptx");
        const float originalX = 96;
        const float originalY = 360;
        const float originalWidth = 220;
        const float originalHeight = 24;

        using (var presentation = new Presentation())
        {
            presentation.SlideSize.SetSize(
                SlideSizeType.OnScreen16x9,
                SlideSizeScaleType.EnsureFit);
            _ = presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
            foreach (ISlide slide in presentation.Slides)
            {
                foreach (IShape shape in slide.Shapes.ToArray())
                {
                    slide.Shapes.Remove(shape);
                }
            }

            ISlide first = presentation.Slides[0];
            IBaseSlideHeaderFooterManager manager =
                first.HeaderFooterManager.AsIBaseSlideHeaderFooterManager;
            manager.SetFooterText("Existing footer");
            manager.SetFooterVisibility(true);
            IShape existing = Placeholder(first, PlaceholderType.Footer);
            existing.X = originalX;
            existing.Y = originalY;
            existing.Width = originalWidth;
            existing.Height = originalHeight;
            presentation.Save(input, Aspose.Slides.Export.SaveFormat.Pptx);
        }

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new SetFooterOp
                    {
                        Slides = "1-2",
                        Text = "Confidential",
                        ShowNumber = true,
                    },
                ],
            },
            new PresentationEditRequest { OutputPath = output });

        Assert.All(result.Applied, static operation => Assert.Equal("ok", operation.Status));
        float slideWidth;
        float slideHeight;
        using (var reopened = new Presentation(output))
        {
            slideWidth = reopened.SlideSize.Size.Width;
            slideHeight = reopened.SlideSize.Size.Height;
            IShape firstFooter = Placeholder(reopened.Slides[0], PlaceholderType.Footer);
            Assert.InRange(Math.Abs(firstFooter.X - originalX), 0, 0.01f);
            Assert.InRange(Math.Abs(firstFooter.Y - originalY), 0, 0.01f);
            Assert.InRange(Math.Abs(firstFooter.Width - originalWidth), 0, 0.01f);
            Assert.InRange(Math.Abs(firstFooter.Height - originalHeight), 0, 0.01f);

            foreach (ISlide slide in reopened.Slides)
            {
                IShape footer = Placeholder(slide, PlaceholderType.Footer);
                Assert.StartsWith(
                    "Confi",
                    ((IAutoShape)footer).TextFrame.Text,
                    StringComparison.Ordinal);
                AssertInsideCanvas(footer, slideWidth, slideHeight);
                AssertInsideCanvas(
                    Placeholder(slide, PlaceholderType.SlideNumber),
                    slideWidth,
                    slideHeight);
            }
        }

        PresentationReadResult read = fixture.Engine.Read(
            output,
            new PresentationReadRequest { Scope = PresentationReadScopes.Full });
        SlidesReviewAnalysis review = SlidesReviewAnalyzer.Analyze(
            read.Slides,
            slideWidth,
            slideHeight);
        Assert.DoesNotContain(
            review.Findings,
            static finding => finding.Code == SlidesReviewChecks.ShapeOutsideSlide.Code);
    }

    private static IShape Placeholder(ISlide slide, PlaceholderType type) =>
        Assert.Single(slide.Shapes, shape => shape.Placeholder?.Type == type);

    private static void AssertInsideCanvas(
        IShape shape,
        float slideWidth,
        float slideHeight)
    {
        Assert.True(shape.X >= -0.5f);
        Assert.True(shape.Y >= -0.5f);
        Assert.True(shape.X + shape.Width <= slideWidth + 0.5f);
        Assert.True(shape.Y + shape.Height <= slideHeight + 0.5f);
    }
}
