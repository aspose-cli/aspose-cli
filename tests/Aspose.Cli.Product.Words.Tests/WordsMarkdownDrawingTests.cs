using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Drawing.Charts;
using Aspose.Words.Notes;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// A Markdown output holds the text alone, as the Markdown of a PDF or a deck does: the save
/// leaves out every drawing its export would write as a picture, inline as base64 or as a file
/// beside the output, and warns <c>LOSSY_CONVERSION</c> naming the pictures, charts and other
/// drawings it left out, with the <c>words extract --what images</c> command that writes the
/// pictures. The paragraphs and tables of a shape or text box move into the body after the
/// paragraph that held it, with their revisions and lists, and a horizontal rule, which the
/// export writes as a line, stays. The scenarios check the rest of the Markdown of each kind.
/// </summary>
public sealed class WordsMarkdownDrawingTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    // The drawings of Drawn: two pictures, a chart, a group of a picture and a rectangle, a footnote's picture and a text box.
    private const string LeftOut = "The md output holds the text alone and leaves out 4 picture(s), 1 chart(s) and 2 other drawing(s), such as shapes, text boxes, WordArt and SmartArt; the text of shapes and text boxes stays in the body.";

    /// <summary>
    /// A redline compare writes into Markdown leaves out the drawings as a convert does, and its
    /// hint names the pictures of the revised document; the scenarios run the command it gives.
    /// </summary>
    [Fact]
    public void CompareIntoMarkdown_LeavesOutTheDrawingsAsAConvertDoes()
    {
        using var fixture = new WordsFixture();
        string left = Drawn(fixture);
        var changed = new Document(left);
        new DocumentBuilder(changed).Write("Changed.");
        string right = fixture.Temp.File("changed.docx");
        changed.Save(right, SaveFormat.Docx);
        string output = fixture.Temp.File("redline.md");

        WordsCompareResult result = WordsCompare.Run(fixture.Session, new WordsCompareRequest
        {
            Left = left,
            Right = right,
            Output = TestOutput.At(output, format: "md"),
        });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Message.Contains("holds the text alone", StringComparison.Ordinal));
        Assert.Equal(LeftOut, warning.Message);
        Assert.Contains("those of the revised document", warning.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("data:", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every block text <c>words read</c> gives of a document with every kind of drawing, the text
    /// of its shapes and text boxes included, is in the Markdown its convert writes, each shape's
    /// text once and after the text of the paragraph that held it. The text of WordArt and
    /// SmartArt, which no read gives, is not.
    /// </summary>
    [Fact]
    public void Convert_KeepsInMarkdownEveryTextTheReadOfTheDocumentGives()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("drawn.docx");
        File.Copy(Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Invariants.Tests", "Scenarios", "Assets", "words-every-kind-of-drawing.docx"), input);
        string output = fixture.Temp.File("drawn.md");

        WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = input, Output = TestOutput.At(output, format: "md") });

        string markdown = File.ReadAllText(output);
        string[] texts = [.. WordsRead.Run(fixture.Session, new WordsReadRequest { Input = input }).Blocks.Select(static block => block.Text).OfType<string>().Where(static text => text.Length > 0)];
        Assert.Contains("TEXTBOX-TEXT", texts);
        Assert.All(texts, text => Assert.Equal(1, Regex.Count(markdown, Regex.Escape(text))));
        Assert.True(markdown.IndexOf("Rectangle with text:", StringComparison.Ordinal) < markdown.IndexOf("RECT-TEXT", StringComparison.Ordinal));
        Assert.True(markdown.IndexOf("Text box:", StringComparison.Ordinal) < markdown.IndexOf("TEXTBOX-TEXT", StringComparison.Ordinal));
        Assert.Contains("-----", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// A text box a tracked change deletes leaves its paragraph in the body once, and the output
    /// is still named for writing deleted text beside the rest: the revisions are read before the
    /// drawings come out.
    /// </summary>
    [Fact]
    public void Convert_StillNamesTheDeletionOfATextBox()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Box:");
        Shape box = TextBox(document, "DEL-BOX");
        builder.InsertNode(box);
        builder.Writeln();
        builder.Write("End.");
        document.StartTrackRevisions("Reviewer");
        box.Remove();
        document.StopTrackRevisions();

        (WordsConvertResult result, string markdown) = ToMarkdown(fixture, document);

        Assert.Contains(result.Warnings ?? [], static warning => warning.Details?.ToJsonString() == """{"content":["revisions"]}""");
        Assert.Equal(1, Regex.Count(markdown, "DEL-BOX"));
    }

    /// <summary>A numbered list in a text box stays a Markdown list once its paragraphs move into the body.</summary>
    [Fact]
    public void Convert_WritesAListInATextBoxAsAList()
    {
        using var fixture = new WordsFixture();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Box:");
        Shape box = TextBox(document, string.Empty);
        builder.InsertNode(box);
        builder.Writeln();
        builder.Write("After.");
        var inside = new DocumentBuilder(document);
        inside.MoveTo(box.FirstParagraph);
        inside.ListFormat.ApplyNumberDefault();
        inside.Writeln("TB-ITEM1");
        inside.Write("TB-ITEM2");

        string markdown = ToMarkdown(fixture, document).Markdown;

        Assert.Contains("1. TB-ITEM1", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(@"1\.", markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// A redline whose change is in a text box writes it into Markdown as the redline of the same
    /// change in a body paragraph writes it, with the warning for the deleted text beside it.
    /// </summary>
    [Fact]
    public void CompareIntoMarkdown_WritesAChangeInATextBoxAsInTheBody()
    {
        using var fixture = new WordsFixture();
        (WordsCompareResult boxed, string inBox) = Redline(fixture, "boxed", box: true);
        string inBody = Redline(fixture, "plain", box: false).Markdown;

        string changed = inBody.Split('\n').Single(static line => line.Contains("BOX", StringComparison.Ordinal)).Replace("Intro ", string.Empty, StringComparison.Ordinal).Trim();
        Assert.Contains("LEFT", changed, StringComparison.Ordinal);
        Assert.Contains(changed, inBox, StringComparison.Ordinal);
        Assert.Contains(boxed.Warnings ?? [], static warning => warning.Details?.ToJsonString() == """{"content":["revisions"]}""");
    }

    private static (WordsCompareResult Result, string Markdown) Redline(WordsFixture fixture, string name, bool box)
    {
        string[] paths = [.. new[] { "LEFT-BOX", "RIGHT-BOX" }.Select(text =>
        {
            var document = new Document();
            var builder = new DocumentBuilder(document);
            builder.Write("Intro ");
            if (box)
            {
                builder.InsertNode(TextBox(document, text));
            }
            else
            {
                builder.Write(text);
            }

            string path = fixture.Temp.File($"{name}-{text}.docx");
            document.Save(path, SaveFormat.Docx);
            return path;
        })];
        string output = fixture.Temp.File($"{name}-redline.md");
        WordsCompareResult result = WordsCompare.Run(fixture.Session, new WordsCompareRequest { Left = paths[0], Right = paths[1], Output = TestOutput.At(output, format: "md") });
        return (result, File.ReadAllText(output));
    }

    private static (WordsConvertResult Result, string Markdown) ToMarkdown(WordsFixture fixture, Document document)
    {
        string input = fixture.Temp.File("boxed.docx");
        document.Save(input, SaveFormat.Docx);
        string output = fixture.Temp.File("boxed.md");
        WordsConvertResult result = WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = input, Output = TestOutput.At(output, format: "md") });
        return (result, File.ReadAllText(output));
    }

    private static Shape TextBox(Document document, string text)
    {
        var box = new Shape(document, ShapeType.TextBox) { Width = 100, Height = 50, WrapType = WrapType.Inline };
        box.AppendChild(new Paragraph(document));
        box.FirstParagraph.AppendChild(new Run(document, text));
        return box;
    }

    private static string Drawn(WordsFixture fixture)
    {
        byte[] png = Convert.FromBase64String(Png);
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Before.");
        builder.InsertImage(png);
        builder.Write(" and ");
        builder.InsertImage(png);
        builder.Writeln();
        builder.InsertChart(ChartType.Column, 200, 100);
        builder.Writeln();
        builder.InsertHorizontalRule();
        builder.Writeln();
        var picture = new Shape(document, ShapeType.Image) { Width = 20, Height = 20 };
        picture.ImageData.SetImage(new MemoryStream(png));
        var group = new GroupShape(document) { Width = 50, Height = 20, CoordSize = new System.Drawing.Size(50, 20) };
        group.AppendChild(picture);
        group.AppendChild(new Shape(document, ShapeType.Rectangle) { Left = 30, Width = 20, Height = 20 });
        builder.InsertNode(group);
        builder.Write("Noted");
        Footnote note = builder.InsertFootnote(FootnoteType.Footnote, "Note ");
        var noted = new Shape(document, ShapeType.Image) { Width = 20, Height = 20, WrapType = WrapType.Inline };
        noted.ImageData.SetImage(new MemoryStream(png));
        note.FirstParagraph.AppendChild(noted);
        builder.MoveToDocumentEnd();
        builder.Writeln();
        builder.CurrentParagraph.AppendChild(TextBox(document, "Boxed"));
        builder.Writeln();
        builder.Write("After.");
        string path = fixture.Temp.File("source.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }
}
