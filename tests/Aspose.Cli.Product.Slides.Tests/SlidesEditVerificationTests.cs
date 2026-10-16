using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What <c>slides edit --verify</c> checks and what the snapshots around each operation see:
/// expectations follow the slide an operation resolved when it ran, wherever later operations
/// move it, and a read-back that differs fails the verification.
/// </summary>
public sealed class SlidesEditVerificationTests
{
    [Fact]
    public void Verify_ChecksEachTitleOnTheSlideAtItsPositionAfterAMove()
    {
        using var fixture = new SlidesEngineFixture();
        string input = Outline(fixture, "moved.pptx");
        uint[] ids;
        using (var source = new Presentation(input))
        {
            ids = [.. source.Slides.Select(static slide => slide.SlideId)];
        }

        string output = fixture.File("moved.out.pptx");
        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new MoveSlideOp { Id = "move", Slide = 3, To = 1 },
                    new SetTitleOp { Id = "tA", Slide = 2, Text = "Z" },
                    new SetTitleOp { Id = "tC", Slide = 1, Text = "Y" },
                ],
            },
            Output = TestOutput.At(output),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok);
        Assert.Empty(result.Verification.Issues);
        Assert.Equal(
            fixture.LicenseState == LicenseState.Licensed ? ["move", "tA", "tC"] : ["move"],
            result.Verification.CheckedOps);
        using var edited = new Presentation(output);
        Assert.Equal([ids[2], ids[0], ids[1]], edited.Slides.Select(static slide => slide.SlideId));
        Assert.Equal("Z", SlidesEngineSupport.Title(edited.Slides.Single(slide => slide.SlideId == ids[0])));
        Assert.Equal("Y", SlidesEngineSupport.Title(edited.Slides.Single(slide => slide.SlideId == ids[2])));
    }

    [Fact]
    public void Verify_ReportsAReadBackThatDiffers_AndTheEditFails()
    {
        TestLicense.Require("Evaluation mode records no text expectation.");
        using var fixture = new SlidesEngineFixture();
        string input = Outline(fixture, "differs.pptx");
        using var edited = new Presentation(input);
        using var output = new Presentation(input);
        var operation = new SetTitleOp { Id = "title", Slide = 1, Text = "Changed" };
        SlidesMutationHandlers.ResolvedSlidesOp target = SlidesMutationHandlers.ResolveTarget(edited, operation);
        var verifier = new SlidesEditVerifier(edited, _ => target, evaluation: false);
        SlidesAuthoring.Title(edited.Slides[0]).TextFrame.Text = "Changed";

        verifier.Record(operation, AppliedOperations.Changed(SlidesOp.Catalog, operation, 1));
        // The output read back still holds the original title.
        VerificationFacts facts = verifier.Verify(output);

        VerificationIssue issue = Assert.Single(facts.Issues);
        Assert.Equal("SLIDE_CONTENT_MISMATCH", issue.Code);
        Assert.Equal("slide 1", issue.Location);
        // The title was read back and compared, so it counts as checked although it differs.
        Assert.Contains("title", facts.Checked);
        SlidesEditResult failed = new()
        {
            Input = new InputInfo { Path = input, Format = "pptx", SizeBytes = 1, Fingerprint = new FileFingerprint { Sha256 = new string('0', 64) }, Encrypted = false },
            DryRun = false,
            Applied = [],
            Verification = new SlidesEditVerification { Ok = false, Issues = facts.Issues, CheckedOps = [] },
        };
        Assert.True(failed.HasFailures);
    }

    [Fact]
    public void Verify_CountsAHiddenSlideReadBackThatDiffersAsChecked()
    {
        using var fixture = new SlidesEngineFixture();
        string input = Outline(fixture, "hidden.pptx");
        using var edited = new Presentation(input);
        using var output = new Presentation(input);
        var operation = new SetSlideHiddenOp { Id = "hide", Slides = "1", Hidden = true };
        SlidesMutationHandlers.ResolvedSlidesOp target = SlidesMutationHandlers.ResolveTarget(edited, operation);
        var verifier = new SlidesEditVerifier(edited, _ => target, evaluation: fixture.LicenseState != LicenseState.Licensed);
        edited.Slides[0].Hidden = true;

        verifier.Record(operation, AppliedOperations.Changed(SlidesOp.Catalog, operation, 1));
        // The output read back still shows the slide.
        VerificationFacts facts = verifier.Verify(output);

        Assert.Equal("SLIDE_CONTENT_MISMATCH", Assert.Single(facts.Issues).Code);
        Assert.Contains("hide", facts.Checked);
    }

    [Fact]
    public void Verify_LeavesAnOperationWithoutReadBackUnchecked_WithoutAnIssue()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("unread.pptx", slides: 1);
        string picture = fixture.File("unread.bmp");
        File.WriteAllBytes(picture, Bitmap(0x40));

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetBackgroundOp { Id = "background", Slides = "1", Path = picture }] },
            Output = TestOutput.At(fixture.File("unread.out.pptx")),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok);
        Assert.Empty(result.Verification.Issues);
        Assert.DoesNotContain("background", result.Verification.CheckedOps);
    }

    [Fact]
    public void SetBackground_WithAPictureOfTheSameSize_ChangesTheSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("background.pptx", slides: 1);
        string first = fixture.File("first.bmp");
        string second = fixture.File("second.bmp");
        File.WriteAllBytes(first, Bitmap(0x20));
        File.WriteAllBytes(second, Bitmap(0xC0));
        Assert.Equal(new FileInfo(first).Length, new FileInfo(second).Length);
        string pictured = fixture.File("background.first.pptx");
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetBackgroundOp { Slides = "1", Path = first }] },
            Output = TestOutput.At(pictured),
        });

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = pictured,
            Batch = new SlidesOpsBatch { Ops = [new SetBackgroundOp { Slides = "1", Path = second }] },
            Output = TestOutput.At(fixture.File("background.second.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
    }

    [Fact]
    public void SetLayout_ToTheSameNamedLayoutOfAnotherMaster_ChangesTheSlide()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("masters.pptx");
        string layout;
        using (var presentation = new Presentation())
        {
            IMasterSlide copy = presentation.Masters.AddClone(presentation.Masters[0]);
            layout = copy.LayoutSlides[0].Name;
            Assert.Equal(layout, presentation.Masters[0].LayoutSlides[0].Name);
            presentation.Slides[0].LayoutSlide = copy.LayoutSlides[0];
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetLayoutOp { Slides = "1", Layout = layout }] },
            Output = TestOutput.At(fixture.File("masters.out.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
    }

    [Fact]
    public void InsertSection_IsAddressedByItsName_EscapingOnlyWhatBreaksTheAddress()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("sections.pptx", slides: 2);

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new InsertSectionOp { Name = " Q3 / plan 100% ", At = 1 },
                    new InsertSectionOp { Name = "业绩回顾 Results", At = 2 },
                ],
            },
            Output = TestOutput.At(fixture.File("sections.out.pptx")),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal(["section/%20Q3 %2F plan 100%25%20"], result.Applied[0].Targets);
        Assert.Equal(["section/业绩回顾 Results"], result.Applied[1].Targets);
    }

    [Fact]
    public void Verify_ReadsBackTextReplacementsBodiesOrderFootersAndTransitions()
    {
        TestLicense.Require("Evaluation mode records no text expectation.");
        using var fixture = new SlidesEngineFixture();
        string input = Outline(fixture, "covered.pptx");
        // The imported deck holds neither the find text nor its replacement.
        string other = fixture.CreatePresentation("other.pptx", slides: 1);
        string output = fixture.File("covered.out.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SlidesReplaceTextOp { Id = "replace", Find = "one", Replace = "uno" },
                    new SetBodyOp
                    {
                        Id = "body",
                        Slide = 3,
                        Paragraphs = [new SlidesParagraphInput { Text = "First" }, new SlidesParagraphInput { Text = "Inner", List = new SlidesListInput { Kind = "number", Level = 2 } }],
                    },
                    new InsertSlideOp { Id = "insert" },
                    new ImportSlidesOp { Id = "import", Path = other },
                    new SetFooterOp { Id = "footer", Slides = "2", Text = "Foot", ShowNumber = true },
                    new SetTransitionOp { Id = "transition", Slides = "1", Kind = SlidesTransitionKinds.Fade, DurationMs = 500 },
                ],
            },
            Output = TestOutput.At(output),
            Verify = true,
        });

        Assert.True(result.Verification!.Ok);
        Assert.Equal(["replace", "body", "insert", "import", "footer", "transition"], result.Verification.CheckedOps);
    }

    private static string Outline(SlidesEngineFixture fixture, string name)
    {
        string markdown = fixture.File(name + ".md");
        File.WriteAllText(markdown, "# A\n\n## B\n\n- one\n\n## C\n\n- two\n");
        string path = fixture.File(name);
        SlidesCreate.Run(fixture.Session, new SlidesCreateRequest { Output = TestOutput.At(path), Markdown = new SourceFile("--from-markdown", markdown) });
        return path;
    }

    /// <summary>A 2 by 2 pixel 24-bit bitmap of one gray level; every level gives a file of the same size.</summary>
    private static byte[] Bitmap(byte level)
    {
        const int Pixels = 16; // 2 rows of 2 pixels, each row padded to 8 bytes.
        byte[] data = new byte[54 + Pixels];
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BitConverter.GetBytes(data.Length).CopyTo(data, 2);
        BitConverter.GetBytes(54).CopyTo(data, 10);
        BitConverter.GetBytes(40).CopyTo(data, 14);
        BitConverter.GetBytes(2).CopyTo(data, 18);
        BitConverter.GetBytes(2).CopyTo(data, 22);
        BitConverter.GetBytes((short)1).CopyTo(data, 26);
        BitConverter.GetBytes((short)24).CopyTo(data, 28);
        BitConverter.GetBytes(Pixels).CopyTo(data, 34);
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 6; column++)
            {
                data[54 + (row * 8) + column] = level;
            }
        }

        return data;
    }
}
