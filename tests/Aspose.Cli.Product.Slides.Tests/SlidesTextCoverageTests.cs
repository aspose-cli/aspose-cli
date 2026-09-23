using Aspose.Slides;
using Aspose.Slides.Export;
using Aspose.Slides.SmartArt;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

// Evaluation mode truncates any text longer than five characters when it is read,
// so every text in these decks is at most five characters long.
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
                    new SlidesReplaceTextOp { Find = "x", Replace = "z", MatchCase = true },
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
            new PresentationSearchRequest { Pattern = "x", CaseSensitive = true, Scope = PresentationSearchScopes.Shapes });
        SlidesExtractResult extract = fixture.Engine.Extract(
            input,
            new PresentationExtractRequest { What = PresentationExtractKinds.Text, OutputDirectory = fixture.File("text") });

        SlideShapeData[] shapes = Assert.Single(read.Slides).Shapes.ToArray();
        Assert.Contains(shapes, static shape => shape.Type == "table" && shape.Text == "tx ty");
        Assert.Contains(shapes, static shape => shape.Type == "group" && shape.Text == "gx");
        Assert.Contains(shapes, static shape => shape.Text == "sx");
        Assert.Contains(shapes, static shape => shape.Type == "table" && shape.Runs?.Count == 2);
        Assert.Equal(3, search.Hits.Count);
        string text = File.ReadAllText(Assert.Single(extract.Items).Path);
        Assert.Contains("tx", text, StringComparison.Ordinal);
        Assert.Contains("gx", text, StringComparison.Ordinal);
        Assert.Contains("sx", text, StringComparison.Ordinal);
    }

    private static IAutoShape Styled(Presentation deck) =>
        deck.Slides[0].Shapes.OfType<IAutoShape>().Single(static shape => shape.Name == "Styled");

    private static string CreateDeck(SlidesEngineFixture fixture)
    {
        fixture.Gate.EnsureApplied();
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
        table[0, 0].TextFrame.Text = "tx";
        table[1, 0].TextFrame.Text = "ty";

        IGroupShape group = slide.Shapes.AddGroupShape();
        group.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 140, 100, 30).TextFrame.Text = "gx";

        ISmartArt smartArt = slide.Shapes.AddSmartArt(260, 20, 200, 150, SmartArtLayoutType.BasicBlockList);
        foreach (ISmartArtNode node in smartArt.AllNodes.ToArray().Skip(1))
        {
            smartArt.AllNodes.RemoveNode(node);
        }

        smartArt.AllNodes[0].TextFrame.Text = "sx";
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "nx";
        presentation.Save(path, SaveFormat.Pptx);
        return path;
    }
}
