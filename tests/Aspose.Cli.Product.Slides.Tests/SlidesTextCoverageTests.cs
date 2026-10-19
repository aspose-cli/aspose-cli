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

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Find = "bc", Replace = "Q" }] },
            Output = TestOutput.At(output),
        });

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
    public void Read_KeepsAShapesParagraphsOnTheirOwnLines()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("lines.pptx");
        using (var deck = new Presentation())
        {
            IAutoShape box = deck.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 200, 100);
            box.TextFrame.Text = "q";
            box.TextFrame.Paragraphs.Add(new Paragraph { Text = "x y" });
            deck.Save(input, SaveFormat.Pptx);
        }

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input });
        SlidesSearchResult search = SlidesSearch.Run(fixture.Session, new SlidesSearchRequest
        {
            Input = input,
            Query = new Sdk.Text.SearchQuery(Sdk.Text.TextSearch.Create("q", regex: false, caseSensitive: false), 10, SlidesSearchScopes.Shapes),
        });

        SlidesShapeData shape = Assert.Single(read.Slides[0].Shapes, static shape => !SlidesEngineFixture.IsEvaluationWatermark(shape.Text));
        Assert.Equal("q\nx y", shape.Text);
        Assert.Equal("q x y", Assert.Single(search.Hits).Context);
    }

    [Fact]
    public void ReplaceText_ThatMatchesNothing_WarnsWithoutRepeatingTheFindText()
    {
        using var fixture = new SlidesEngineFixture();
        string input = CreateDeck(fixture);

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SlidesReplaceTextOp { Find = "bc", Replace = "Q", Scope = SlidesSearchScopes.Notes },
                    new SlidesReplaceTextOp { Find = "q", Replace = "z" },
                ],
            },
            Output = TestOutput.At(fixture.File("unmatched.pptx")),
        });

        // Only the first operation matched nothing: "bc" is on the slide, not in its notes.
        Assert.Equal([0L, 4L], result.Applied.Select(static outcome => outcome.ItemsAffected));
        // SLD-4: an operation that changed nothing names no target, not the whole presentation.
        Assert.Empty(result.Applied[0].Targets);
        Warning warning = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.OpNoEffect);
        Assert.Equal(result.Applied[0].Id, warning.Details!["id"]!.GetValue<string>());
        Assert.Equal("replace_text", warning.Details["op"]!.GetValue<string>());
        Assert.Equal(NoEffectReasons.NoMatch, warning.Details["reason"]!.GetValue<string>());
        Assert.Contains("'notes'", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bc", warning.Message, StringComparison.Ordinal);
        // SLD-3: the sentences of the hint are joined with a space.
        Assert.Equal(
            "Search with 'slides search' and the same pattern and scope, or set \"scope\": \"all\" on the op to cover both shape text and speaker notes. "
                + "Chart text and alternative text are never matched.",
            warning.Hint);
    }

    [Fact]
    public void ReplaceText_ReachesTableCellsGroupChildrenSmartArtAndNotes()
    {
        using var fixture = new SlidesEngineFixture();
        string input = CreateDeck(fixture);
        string output = fixture.File("covered.pptx");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops =
                [
                    new SlidesReplaceTextOp { Find = "q", Replace = "z", CaseSensitive = true },
                    new SlidesReplaceTextOp { Find = "(t)y", Replace = "$1Y", Regex = true },
                ],
            },
            Output = TestOutput.At(output),
        });

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

        SlidesReadResult read = SlidesRead.Run(
            fixture.Session,
            new SlidesReadRequest { Input = input, Content = SlidesReadContents.Full });
        SlidesSearchResult search = SlidesSearch.Run(
            fixture.Session,
            new SlidesSearchRequest { Input = input, Query = new SearchQuery(TextSearch.Create("q", regex: false, caseSensitive: true), 100, SlidesSearchScopes.Shapes) });
        SlidesExtractResult extract = SlidesExtract.Run(
            fixture.Session,
            new SlidesExtractRequest { Input = input, Output = new ResolvedDirectory(fixture.File("text")), What = SlidesExtractKinds.Text });

        SlidesShapeData[] shapes = Assert.Single(read.Slides).Shapes.ToArray();
        Assert.Contains(shapes, static shape => shape.Kind == "table" && shape.Text == "tq\nty");
        Assert.Contains(shapes, static shape => shape.Kind == "group" && shape.Text == "gq");
        Assert.Contains(shapes, static shape => shape.Text == "sq");
        Assert.Contains(shapes, static shape => shape.Kind == "table" && shape.Runs?.Count == 2);
        Assert.Equal(3, search.Hits.Count);
        string text = File.ReadAllText(Assert.Single(extract.Outputs).Path);
        Assert.Contains("tq", text, StringComparison.Ordinal);
        Assert.Contains("gq", text, StringComparison.Ordinal);
        Assert.Contains("sq", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracted text holds neither a shape's alternative text nor a chart's text; a shapes read
    /// reports the alternative text as the shape's <c>altText</c>.
    /// </summary>
    [Fact]
    public void ExtractedText_LeavesAltTextAndChartTextOut()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("alt.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 20, 200, 80);
            box.TextFrame.Text = "kq";
            box.AlternativeText = "zalt";
            slide.Shapes.AddChart(Aspose.Slides.Charts.ChartType.ClusteredColumn, 20, 120, 300, 200);
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesExtractResult extract = SlidesExtract.Run(
            fixture.Session,
            new SlidesExtractRequest { Input = input, Output = new ResolvedDirectory(fixture.File("text")), What = SlidesExtractKinds.Text });
        SlidesReadResult read = SlidesRead.Run(
            fixture.Session,
            new SlidesReadRequest { Input = input, Content = SlidesReadContents.Shapes });

        string text = File.ReadAllText(Assert.Single(extract.Outputs).Path);
        Assert.Contains("kq", text, StringComparison.Ordinal);
        Assert.DoesNotContain("zalt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Categ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Serie", text, StringComparison.Ordinal);
        Assert.Contains(Assert.Single(read.Slides).Shapes, static shape => shape.Text == "kq" && shape.AltText == "zalt");
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

        SlidesExtractResult extract = SlidesExtract.Run(
            fixture.Session,
            new SlidesExtractRequest { Input = input, Output = new ResolvedDirectory(fixture.File("text")), What = SlidesExtractKinds.Text });
        SlidesSearchResult search = SlidesSearch.Run(
            fixture.Session,
            new SlidesSearchRequest { Input = input, Query = new SearchQuery(TextSearch.Create("cd", regex: false, caseSensitive: true), 100, SlidesSearchScopes.Shapes) });

        string text = File.ReadAllText(Assert.Single(extract.Outputs).Path);
        Assert.DoesNotContain('\r', text);
        Assert.Contains("ab\ncd\nef", text, StringComparison.Ordinal);
        Assert.Equal("ab cd", Assert.Single(search.Hits).Context);
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

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input, Content = SlidesReadContents.Full });

        Assert.Equal(
            ["#1B2A41", "#000000"],
            read.Slides[0].Shapes.Where(static shape => !SlidesEngineFixture.IsEvaluationWatermark(shape.Text)).Select(static shape => Assert.Single(shape.Runs!).Color));
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

        SlidesReadResult read = SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input, Content = SlidesReadContents.Full });

        SlidesTextRunData run = Assert.Single(read.Slides[0].Shapes.Single(static shape => !SlidesEngineFixture.IsEvaluationWatermark(shape.Text)).Runs!);
        Assert.Equal("Calibri", run.Font);
        Assert.Equal("SimSun", run.EastAsianFont);
    }

    [Fact]
    public void ReplaceText_LeavesTextThatEvaluationModeCutShortAsItIs()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("long-text.pptx", slides: 1);
        string output = fixture.File("long-text-replaced.pptx");
        var batch = new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Find = "Slide 1", Replace = "Intro" }] };
        var request = new SlidesEditRequest { Input = input, Batch = batch, Output = TestOutput.At(output) };

        if (fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Equal(1, Assert.Single(SlidesEdit.Run(fixture.Session, request).Applied).ItemsAffected);
            return;
        }

        // Writing back what evaluation mode read would lose the rest of the text, so the cut
        // paragraph is left alone; the CLI discloses the cut (see SlidesCliWorkflowTests), and
        // the no-effect warning says the cut, not a missing match, is why nothing changed.
        SlidesEditResult result = SlidesEdit.Run(fixture.Session, request);
        Assert.Equal(0, Assert.Single(result.Applied).ItemsAffected);
        Warning noEffect = Assert.Single(result.Warnings!, static warning => warning.Code == WarningCodes.OpNoEffect);
        Assert.Equal(NoEffectReasons.EvaluationTruncated, noEffect.Details!["reason"]!.GetValue<string>());
        Assert.Contains("license", noEffect.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("Search with", noEffect.Hint, StringComparison.Ordinal);
        Assert.True(File.Exists(output));
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
