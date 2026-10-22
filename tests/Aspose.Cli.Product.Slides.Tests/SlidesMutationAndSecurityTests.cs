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
    [InlineData(false, "3")]
    [InlineData(true, "3")]
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
        var request = new SlidesEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = bestEffort },
        };
        if (bestEffort)
        {
            SlidesEditResult result = SlidesEdit.Run(fixture.Session, request);
            Assert.Equal(["ok", "failed"], result.Applied.Select(static outcome => outcome.Status));
            Assert.Equal(1, result.Applied[0].ItemsAffected);
            Assert.Equal(0, result.Applied[1].ItemsAffected);
            Assert.Equal(2, SlidesInfo.Run(fixture.Session, new SlidesInspectRequest { Input = output }).Presentation.SlideCount);
        }
        else
        {
            Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, request));
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
            SlidesInfo.Run(fixture.Session, new SlidesInspectRequest { Input = input }));
        CliException wrong = Assert.Throws<CliException>(() =>
            SlidesInfo.Run(
                fixture.Session,
                new SlidesInspectRequest { Input = input, Password = new Secret("wrong") }));
        SlidesInspectResult opened = SlidesInfo.Run(
            fixture.Session,
            new SlidesInspectRequest { Input = input, Password = new Secret("correct") });

        Assert.Equal(ErrorCodes.PasswordRequired, missing.Code);
        Assert.Equal(ErrorCodes.PasswordInvalid, wrong.Code);
        Assert.Single(opened.Slides);
    }

    [Fact]
    public void RemoteMarkdownImage_IsOmittedWithAWarning()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("remote.md");
        string output = fixture.File("remote.pptx");
        File.WriteAllText(
            markdown,
            "# Remote\n\n![hero](https://example.test/hero.png)");

        SlidesCreateResult result = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest
        {
            Output = TestOutput.At(output),
            Markdown = new SourceFile("--from-markdown", markdown),
        });

        Assert.Empty(Media(output));
        Assert.Contains(result.Warnings ?? [], static warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
    }

    /// <summary>
    /// A Markdown image is read only from an ordinary file beneath the Markdown file's directory:
    /// a picture outside it, named by an absolute path, a parent path or a share, is omitted and
    /// reported, while the picture beside the Markdown is embedded.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("parent")]
    [InlineData("share")]
    public void MarkdownImageOutsideItsDirectory_IsOmittedWhileOneBesideItIsEmbedded(string reference)
    {
        using var fixture = new SlidesEngineFixture();
        string outside = Directory.CreateDirectory(fixture.File("outside")).FullName;
        string inside = Directory.CreateDirectory(fixture.File("deck")).FullName;
        byte[] sentinel = SlidesEngineFixture.Png(41);
        byte[] picture = SlidesEngineFixture.Png(40);
        Assert.NotEqual(sentinel, picture);
        string sentinelPath = Path.Combine(outside, "sentinel.png");
        File.WriteAllBytes(sentinelPath, sentinel);
        File.WriteAllBytes(Path.Combine(inside, "picture.png"), picture);
        string target = reference switch
        {
            "absolute" => sentinelPath,
            "parent" => "../outside/sentinel.png",
            _ => @"\\localhost\" + sentinelPath[0] + "$" + sentinelPath[2..],
        };
        string markdown = Path.Combine(inside, "deck.md");
        File.WriteAllText(markdown, $"## Outside\n\n![](<{target}>)\n\n## Beside\n\n![](picture.png)\n");
        string output = Path.Combine(inside, "deck.pptx");

        SlidesCreateResult result = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest
        {
            Output = TestOutput.At(output),
            Markdown = new SourceFile("--from-markdown", markdown),
        });

        byte[][] media = Media(output);
        Assert.DoesNotContain(media, item => item.AsSpan().SequenceEqual(sentinel));
        Assert.Contains(media, item => item.AsSpan().SequenceEqual(picture));
        Assert.Contains(result.Warnings ?? [], static warning =>
            warning.Code == WarningCodes.RemoteResourcesBlocked && warning.AffectsCompleteness);
    }

    /// <summary>
    /// The omission warning names each picture the outline references but the presentation
    /// leaves out, as the outline wrote it, with its own reason, and names no local folder.
    /// </summary>
    [Fact]
    public void OmittedMarkdownImages_AreNamedWithTheirReasons()
    {
        using var fixture = new SlidesEngineFixture();
        string inside = Directory.CreateDirectory(fixture.File("deck")).FullName;
        string shared = Directory.CreateDirectory(fixture.File("shared")).FullName;
        File.WriteAllBytes(Path.Combine(shared, "logo.png"), SlidesEngineFixture.Png(40));
        string markdown = Path.Combine(inside, "deck.md");
        File.WriteAllText(markdown,
            "## Missing\n\n![](images/missing.png)\n\n## Outside\n\n![](../shared/logo.png)\n\n## Remote\n\n![](https://example.test/hero.png)\n");

        SlidesCreateResult result = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest
        {
            Output = TestOutput.At(Path.Combine(inside, "deck.pptx")),
            Markdown = new SourceFile("--from-markdown", markdown),
        });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code == WarningCodes.RemoteResourcesBlocked);
        Assert.Contains("images/missing.png (not found)", warning.Message, StringComparison.Ordinal);
        Assert.Contains("../shared/logo.png (outside the input folder)", warning.Message, StringComparison.Ordinal);
        Assert.Contains("https://example.test/hero.png (network address)", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("external", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(shared, warning.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[][] Media(string presentation)
    {
        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(presentation);
        return [.. archive.Entries
            .Where(static entry => entry.FullName.StartsWith("ppt/media/", StringComparison.Ordinal))
            .Select(static entry =>
            {
                using Stream stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            })];
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
                new InsertSectionOp { Name = "Results", At = 1 },
                new InsertSectionOp { Name = "Results", At = 2 },
                new SetNotesOp { Slide = 1, Text = "Saved" },
            ],
        };

        Assert.ThrowsAny<Exception>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(atomicOutput),
        }));
        Assert.False(File.Exists(atomicOutput));

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = batch,
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal(["ok", "failed", "ok"],
            result.Applied.Select(static operation => operation.Status));
        Assert.Equal(ErrorCodes.OpsInvalid.Name, result.Applied[1].Error!.Code);
        Assert.Equal(["section/Results"], result.Applied[0].Targets);
        Assert.Equal(["section/Results"], result.Applied[1].Targets);
        Assert.Equal(["slide/1/notes"], result.Applied[2].Targets);
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

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetNotesOp { Slide = 1, Text = "Safe" }],
            },
            Output = TestOutput.At(output),
            EncryptPassword = new Secret("correct"),
        });

        Assert.Equal(ErrorCodes.PasswordRequired, Assert.Throws<CliException>(() =>
            SlidesInfo.Run(fixture.Session, new SlidesInspectRequest { Input = output })).Code);
        Assert.Equal(3, SlidesInfo.Run(
            fixture.Session,
            new SlidesInspectRequest { Input = output, Password = new Secret("correct") }).Presentation.SlideCount);
        SlidesReadResult read = SlidesRead.Run(
            fixture.Session,
            new SlidesReadRequest
            {
                Input = output,
                Password = new Secret("correct"),
                Slides = PageRange.Parse("1"),
                Content = SlidesReadContents.Full,
                IncludeNotes = true,
            });
        Assert.NotNull(read.Slides[0].Notes);
        Assert.Contains("Safe", read.Slides[0].Notes!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuralTextObjectAndPresentationOps_RoundTripTogether()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("operations.pptx", slides: 3);
        string output = fixture.File("operations.out.pptx");
        // Evaluation mode refuses replace_text over the titles it reads cut short.
        bool licensed = fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed;

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertSectionOp { Name = "Results", At = 2 },
                    .. licensed ? [new SlidesReplaceTextOp { Find = "Slide", Replace = "Q" }] : Array.Empty<SlidesOp>(),
                    new InsertShapeOp
                    {
                        Slide = 1,
                        Geometry = "rounded-rectangle",
                        Rect = new SlidesRectInput { X = 80, Y = 140, Width = 240, Height = 80 },
                        Text = "Key",
                    },
                    new SlidesSetPropertiesOp { Title = "Q4", Author = "CLI" },
                ],
            },
            Output = TestOutput.At(output),
        });

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

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetFooterOp { Text = "Confidential", ShowNumber = true }] },
            Output = TestOutput.At(output),
        });

        BoundedOperationOutcome applied = Assert.Single(result.Applied);
        Assert.Equal(1, applied.ItemsAffected);
        Assert.Equal(["slide/2"], applied.Targets);
        Assert.Equal([2], result.SlidesTouched);
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

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
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
            Output = TestOutput.At(output),
        });

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

        Assert.DoesNotContain(
            fixture.Review(output).Findings!,
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

    /// <summary>
    /// An I/O exception the engine raises inside an operation, such as Aspose.Slides reading past
    /// the end of a TIFF picture cut after its header (the CLI recognizes TIFF by its signature
    /// and leaves its decoding to the engine), is an engine failure of that operation: the error
    /// names its index as every other engine failure does.
    /// </summary>
    [Fact]
    public void AnEngineIoFailureInsideAnOperation_NamesTheOperation()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("cut-image.pptx", slides: 1);
        string picture = fixture.File("cut.tif");
        File.WriteAllBytes(picture, [(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00]);
        string output = fixture.File("cut-image.out.pptx");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SetNotesOp { Slide = 1, Text = "First." },
                    new SlidesInsertImageOp { Slide = 1, Path = picture },
                ],
            },
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal("op-0002", error.Details!["id"]!.GetValue<string>());
        Assert.Equal("insert_image", error.Details["op"]!.GetValue<string>());
        Assert.False(File.Exists(output));
    }
}
