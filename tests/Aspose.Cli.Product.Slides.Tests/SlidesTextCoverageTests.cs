using System.Drawing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Slides;
using Aspose.Slides.Export;
using Aspose.Slides.SmartArt;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

// Evaluation mode truncates any text longer than five characters when it is read,
// so every text in these decks is at most five characters long, and the searched letter
// 'q' never occurs in the watermark evaluation mode saves into every slide.
public sealed class SlidesTextCoverageTests
{
    [Fact]
    public void ReplaceText_ChangesOnlyTheMatchedCharactersAndKeepsRunFormatting()
    {
        using var fixture = new SlidesEngineFixture();
        string input = CreateDeck(fixture);
        string output = fixture.File("replaced.pptx");

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Find = "bc", Replace = "Q" }] },
            new PresentationEditRequest { OutputPath = output });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
        using var deck = new Presentation(output);
        IParagraph paragraph = Styled(deck).TextFrame.Paragraphs[0];
        Assert.Equal(TextAlignment.Center, paragraph.ParagraphFormat.Alignment);
        Assert.Equal(["aQ", "d"], paragraph.Portions.Select(static portion => portion.Text));
        Assert.Equal(NullableBool.True, paragraph.Portions[0].PortionFormat.FontBold);
        Assert.Equal(NullableBool.True, paragraph.Portions[1].PortionFormat.FontItalic);
        Assert.NotEqual(NullableBool.True, paragraph.Portions[1].PortionFormat.FontBold);
    }

    [Fact]
    public void ReplaceText_ThatMatchesNothing_WarnsWithoutRepeatingTheFindText()
    {
        using var fixture = new SlidesEngineFixture();
        string input = CreateDeck(fixture);

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new SlidesReplaceTextOp { Find = "bc", Replace = "Q", Scope = PresentationSearchScopes.Notes },
                    new SlidesReplaceTextOp { Find = "q", Replace = "z" },
                ],
            },
            new PresentationEditRequest { OutputPath = fixture.File("unmatched.pptx") });

        // Only the first operation matched nothing: "bc" is on the slide, not in its notes.
        Assert.Equal([0L, 4L], result.Applied.Select(static outcome => outcome.ItemsAffected));
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.ReplaceNoMatch);
        Assert.Contains("'notes'", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bc", warning.Message, StringComparison.Ordinal);
        Assert.Contains("slides query search", warning.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceText_ReachesTableCellsGroupChildrenSmartArtAndNotes()
    {
        using var fixture = new SlidesEngineFixture();
        string input = CreateDeck(fixture);
        string output = fixture.File("covered.pptx");

        SlidesEditResult result = fixture.Engine.ApplyOps(
            input,
            new SlidesOpsBatch
            {
                Ops =
                [
                    new SlidesReplaceTextOp { Find = "q", Replace = "z", MatchCase = true },
                    new SlidesReplaceTextOp { Find = "(t)y", Replace = "$1Y", Regex = true },
                ],
            },
            new PresentationEditRequest { OutputPath = output });

        Assert.Equal([4L, 1L], result.Applied.Select(static outcome => outcome.ItemsAffected));
        using var deck = new Presentation(output);
        ISlide slide = deck.Slides[0];
        ITable table = Assert.Single(slide.Shapes.OfType<ITable>());
        Assert.Equal("tz", table[0, 0].TextFrame.Text);
        Assert.Equal("tY", table[1, 0].TextFrame.Text);
        IGroupShape group = Assert.Single(slide.Shapes.OfType<IGroupShape>());
        Assert.Equal("gz", ((IAutoShape)group.Shapes[0]).TextFrame.Text);
        ISmartArt smartArt = Assert.Single(slide.Shapes.OfType<ISmartArt>());
        Assert.Equal("sz", smartArt.AllNodes[0].TextFrame.Text);
        Assert.Equal("nz", slide.NotesSlideManager.NotesSlide.NotesTextFrame.Text);
    }

    [Fact]
    public void ReadSearchAndExtract_IncludeTableCellsGroupChildrenAndSmartArt()
    {
        using var fixture = new SlidesEngineFixture();
        string input = CreateDeck(fixture);

        PresentationReadResult read = fixture.Engine.Read(
            input,
            new PresentationReadRequest { Scope = PresentationReadScopes.Full });
        SlidesSearchResult search = fixture.Engine.Search(
            input,
            new PresentationSearchRequest { Query = new SearchQuery(TextSearch.Create("q", regex: false, caseSensitive: true), 100, PresentationSearchScopes.Shapes) });
        SlidesExtractResult extract = fixture.Engine.Extract(
            input,
            new PresentationExtractRequest { What = PresentationExtractKinds.Text, OutputDirectory = fixture.File("text") });

        SlideShapeData[] shapes = Assert.Single(read.Slides).Shapes.ToArray();
        Assert.Contains(shapes, static shape => shape.Type == "table" && shape.Text == "tq ty");
        Assert.Contains(shapes, static shape => shape.Type == "group" && shape.Text == "gq");
        Assert.Contains(shapes, static shape => shape.Text == "sq");
        Assert.Contains(shapes, static shape => shape.Type == "table" && shape.Runs?.Count == 2);
        Assert.Equal(3, search.Hits.Count);
        string text = File.ReadAllText(Assert.Single(extract.Items).Path);
        Assert.Contains("tq", text, StringComparison.Ordinal);
        Assert.Contains("gq", text, StringComparison.Ordinal);
        Assert.Contains("sq", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractedTextAndSearchHits_EndEveryParagraphWithOneLineFeed()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("paragraphs.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 20, 200, 80).TextFrame.Text = "ab\rcd";
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 120, 200, 40).TextFrame.Text = "ef";
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesExtractResult extract = fixture.Engine.Extract(
            input,
            new PresentationExtractRequest { What = PresentationExtractKinds.Text, OutputDirectory = fixture.File("text") });
        SlidesSearchResult search = fixture.Engine.Search(
            input,
            new PresentationSearchRequest { Query = new SearchQuery(TextSearch.Create("cd", regex: false, caseSensitive: true), 100, PresentationSearchScopes.Shapes) });

        string text = File.ReadAllText(Assert.Single(extract.Items).Path);
        Assert.DoesNotContain('\r', text);
        Assert.Contains("ab\ncd\nef", text, StringComparison.Ordinal);
        Assert.Equal("ab\ncd", Assert.Single(search.Hits).Text);
    }

    [Fact]
    public void FullRead_ReportsTheColorEachRunIsDrawnIn()
    {
        // A stated color reads back as written; an inherited one as the theme resolves it.
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("colors.pptx");
        using (var source = new Presentation())
        {
            IAutoShape stated = source.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 300, 60);
            stated.TextFrame.Text = "Set";
            stated.TextFrame.Paragraphs[0].Portions[0].PortionFormat.FillFormat.FillType = FillType.Solid;
            stated.TextFrame.Paragraphs[0].Portions[0].PortionFormat.FillFormat.SolidFillColor.Color = Color.FromArgb(0x1B, 0x2A, 0x41);
            IAutoShape inherited = source.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 140, 300, 60, createFromTemplate: false);
            inherited.AddTextFrame("Base");
            source.Save(input, SaveFormat.Pptx);
        }

        PresentationReadResult read = fixture.Engine.Read(input, new PresentationReadRequest { Scope = PresentationReadScopes.Full });

        Assert.Equal(
            ["#1B2A41", "#000000"],
            read.Slides[0].Shapes.Where(static shape => !shape.EvaluationWatermark).Select(static shape => Assert.Single(shape.Runs!).Color));
    }

    [Fact]
    public void FullRead_ReportsTheFontOfLatinAndEastAsianText()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("fonts.pptx");
        using (var source = new Presentation())
        {
            IAutoShape box = source.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 300, 60);
            box.TextFrame.Text = "Fonts";
            IPortionFormat format = box.TextFrame.Paragraphs[0].Portions[0].PortionFormat;
            format.LatinFont = new FontData("Calibri");
            format.EastAsianFont = new FontData("SimSun");
            source.Save(input, SaveFormat.Pptx);
        }

        PresentationReadResult read = fixture.Engine.Read(input, new PresentationReadRequest { Scope = PresentationReadScopes.Full });

        SlideTextRunData run = Assert.Single(read.Slides[0].Shapes.Single(static shape => !shape.EvaluationWatermark).Runs!);
        Assert.Equal("Calibri", run.Font);
        Assert.Equal("SimSun", run.EastAsianFont);
    }

    [Fact]
    public void ReplaceText_RefusesTextThatEvaluationModeCutShort()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("long-text.pptx", slides: 1);
        string output = fixture.File("long-text-replaced.pptx");
        var batch = new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Find = "Slide 1", Replace = "Intro" }] };
        var request = new PresentationEditRequest { OutputPath = output };

        if (fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Equal(1, Assert.Single(fixture.Engine.ApplyOps(input, batch, request).Applied).ItemsAffected);
            return;
        }

        CliException error = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(input, batch, request));
        Assert.Equal(ErrorCodes.EvaluationLimit, error.Code);
        Assert.Contains("replace_text", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private static IAutoShape Styled(Presentation deck) =>
        deck.Slides[0].Shapes.OfType<IAutoShape>().Single(static shape => shape.Name == "Styled");

    private static string CreateDeck(SlidesEngineFixture fixture)
    {
        string path = fixture.File("text-coverage.pptx");
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides[0];

        IAutoShape styled = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 20, 200, 40);
        styled.Name = "Styled";
        styled.TextFrame.Paragraphs.Clear();
        var paragraph = new Paragraph();
        paragraph.ParagraphFormat.Alignment = TextAlignment.Center;
        var bold = new Portion("ab");
        bold.PortionFormat.FontBold = NullableBool.True;
        var italic = new Portion("cd");
        italic.PortionFormat.FontItalic = NullableBool.True;
        paragraph.Portions.Add(bold);
        paragraph.Portions.Add(italic);
        styled.TextFrame.Paragraphs.Add(paragraph);

        ITable table = slide.Shapes.AddTable(20, 80, [100, 100], [30]);
        table[0, 0].TextFrame.Text = "tq";
        table[1, 0].TextFrame.Text = "ty";

        IGroupShape group = slide.Shapes.AddGroupShape();
        group.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 140, 100, 30).TextFrame.Text = "gq";

        ISmartArt smartArt = slide.Shapes.AddSmartArt(260, 20, 200, 150, SmartArtLayoutType.BasicBlockList);
        foreach (ISmartArtNode node in smartArt.AllNodes.ToArray().Skip(1))
        {
            smartArt.AllNodes.RemoveNode(node);
        }

        smartArt.AllNodes[0].TextFrame.Text = "sq";
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "nq";
        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }
}
