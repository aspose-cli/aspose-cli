using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Layout;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// A paragraph of text right before a Markdown image, a heading or a line that introduces the
/// image, stays on the image's page; the setting is on that paragraph, and no style changes.
/// </summary>
public sealed class WordsMarkdownImageKeepTests
{
    // Body text that leaves too little of the first page for the image, in the built-in design's
    // spacing and in the tighter spacing of a blank document's.
    private const int BuiltInDesignFiller = 25;

    private const int BlankDocumentFiller = 35;

    private static string Filler(int paragraphs) =>
        string.Concat(Enumerable.Range(1, paragraphs).Select(static number => $"Body paragraph {number}.\n\n"));

    [Theory]
    [InlineData("create")]
    [InlineData("insert_markdown")]
    public void LeadInParagraph_StaysOnThePageOfTheImageAfterIt(string path)
    {
        using var fixture = new WordsFixture();
        WriteImage(fixture);

        (int before, int leadIn, int image) = Pages(Imported(fixture, path, $"{Filler(BuiltInDesignFiller)}The chart below shows the week.\n\n![chart](chart.png)\n", template: null), "The chart below shows the week.");

        Assert.Equal(1, before);
        Assert.Equal(2, image);
        Assert.Equal(image, leadIn);
    }

    /// <summary>A template's heading style without keep-with-next: the heading stays with the image, and the style stays as it is.</summary>
    [Fact]
    public void HeadingOfATemplateThatDoesNotKeep_StaysOnThePageOfTheImageAfterIt()
    {
        using var fixture = new WordsFixture();
        WriteImage(fixture);
        string template = fixture.Temp.File("no-keep.docx");
        var source = new Document();
        new DocumentBuilder(source).Write("Anchor");
        source.Styles[StyleIdentifier.Heading1].ParagraphFormat.KeepWithNext = false;
        source.Save(template, SaveFormat.Docx);

        string output = Imported(fixture, "create", $"{Filler(BlankDocumentFiller)}# Chart\n\n![chart](chart.png)\n", template);

        (int before, int heading, int image) = Pages(output, "Chart");
        Assert.Equal(1, before);
        Assert.Equal(2, image);
        Assert.Equal(image, heading);
        Assert.False(new Document(output).Styles[StyleIdentifier.Heading1].ParagraphFormat.KeepWithNext);
    }

    /// <summary>The built-in design's heading styles keep with the next paragraph, so a heading stays with a large image after it.</summary>
    [Fact]
    public void BuiltInHeading_StaysOnThePageOfTheImageAfterIt()
    {
        using var fixture = new WordsFixture();
        WriteImage(fixture);

        (int before, int heading, int image) = Pages(Imported(fixture, "create", $"{Filler(BuiltInDesignFiller)}# Chart\n\n![chart](chart.png)\n", template: null), "Chart");

        Assert.Equal(1, before);
        Assert.Equal(2, image);
        Assert.Equal(image, heading);
    }

    [Fact]
    public void BuiltInDesign_EveryHeadingStyleKeepsWithTheNextParagraph()
    {
        Document template = Engine.Mapping.WordsDocumentLoader.OpenDefaultTemplate();

        Assert.All(new[] { StyleIdentifier.Heading1, StyleIdentifier.Heading2, StyleIdentifier.Heading3, StyleIdentifier.Heading4, StyleIdentifier.Heading5, StyleIdentifier.Heading6 },
            style => Assert.True(template.Styles[style].ParagraphFormat.KeepWithNext, style.ToString()));
    }

    /// <summary>
    /// Only a paragraph right before an image-only paragraph keeps: not one before ordinary text, a
    /// paragraph whose text surrounds an image, an image that follows an image, or a paragraph
    /// before an omitted image.
    /// </summary>
    [Fact]
    public void OtherParagraphs_DoNotKeepWithTheNext()
    {
        using var fixture = new WordsFixture();
        WriteImage(fixture);

        var document = new Document(Imported(fixture, "create",
            "Ordinary.\n\nNext to it.\n\nBefore mixed.\n\nSee ![chart](chart.png) here.\n\nBefore a gone image.\n\n![gone](gone.png)\n\n![chart](chart.png)\n\n![chart](chart.png)\n", template: null));

        Paragraph[] paragraphs = [.. document.FirstSection.Body.Paragraphs.Cast<Paragraph>().Where(static paragraph => !Engine.Mapping.WordsEvaluation.IsMark(paragraph))];
        Assert.DoesNotContain(paragraphs, static paragraph => paragraph.ParagraphFormat.KeepWithNext && paragraph.GetText().Trim() is "Ordinary." or "Next to it." or "Before mixed." or "Before a gone image.");
        Assert.DoesNotContain(paragraphs.SkipLast(1), static paragraph => paragraph.ParagraphFormat.KeepWithNext && paragraph.GetChildNodes(NodeType.Shape, true).Count > 0);
    }

    private static void WriteImage(WordsFixture fixture)
    {
        using var bitmap = new SKBitmap(800, 600);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream file = File.Create(fixture.Temp.File("chart.png"));
        png.SaveTo(file);
    }

    private static string Imported(WordsFixture fixture, string path, string markdown, string? template)
    {
        string source = fixture.Temp.File("doc.md");
        string created = fixture.Temp.File("created.docx");
        if (path == "create")
        {
            File.WriteAllText(source, markdown);
            WordsCreate.Run(fixture.Session, new WordsCreateRequest
            {
                Output = TestOutput.At(created),
                Markdown = new SourceFile("--from-markdown", source),
                Template = template is null ? null : new SourceFile("--template", template),
            });
            return created;
        }

        File.WriteAllText(source, "Anchor\n");
        WordsCreate.Run(fixture.Session, new WordsCreateRequest { Output = TestOutput.At(created), Markdown = new SourceFile("--from-markdown", source) });
        string output = fixture.Temp.File("inserted.docx");
        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = created,
            Batch = new WordsOpsBatch { Ops = [new InsertMarkdownOp { At = new WordsTarget { Find = "Anchor" }, Position = "after", Markdown = markdown }] },
            Output = TestOutput.At(output),
        });
        return output;
    }

    /// <summary>The page of the last body paragraph before a paragraph, of that paragraph and of the document's image.</summary>
    private static (int Before, int Paragraph, int Image) Pages(string path, string text)
    {
        var document = new Document(path);
        var layout = new LayoutCollector(document);
        Paragraph[] paragraphs = [.. document.FirstSection.Body.Paragraphs.Cast<Paragraph>()];
        int at = Array.FindIndex(paragraphs, paragraph => paragraph.GetText().Trim() == text);
        Shape image = Assert.Single(document.FirstSection.Body.GetChildNodes(NodeType.Shape, true).OfType<Shape>(), static shape => shape.HasImage);
        return (layout.GetEndPageIndex(paragraphs[at - 1]), layout.GetStartPageIndex(paragraphs[at]), layout.GetStartPageIndex(image));
    }
}
