using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Markup;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// A block-level content control, such as the one Word wraps a table of contents in, is a
/// container: its paragraphs, and those of nested controls, are blocks in document order.
/// </summary>
public sealed class WordsContentControlTests : IClassFixture<WordsFixture>
{
    private static readonly string[] Blocks = ["Before", "Inside one", "Inside two", "Nested", "After"];

    private readonly WordsFixture _fixture;

    public WordsContentControlTests(WordsFixture fixture) => _fixture = fixture;

    [Fact]
    public void QueryBlocks_NumbersTheParagraphsInsideControls()
    {
        string input = CreateDocument();

        DocumentReadResult read = _fixture.Engine.Read(input, new DocumentReadRequest { Scope = "text" });

        Assert.Equal(Blocks, read.Blocks.Select(static block => block.Text));
        Assert.Equal([1, 2, 3, 4, 5], read.Blocks.Select(static block => block.Block));
    }

    [Fact]
    public void ExtractedText_IncludesTheControlsInBlockOrder()
    {
        string input = CreateDocument();

        WordsExtractResult result = _fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "text",
            OutputDirectory = _fixture.Temp.File($"text-{Guid.NewGuid():N}"),
        });

        Assert.Equal(string.Join('\n', Blocks), File.ReadAllText(Assert.Single(result.Items).Path));
    }

    [Fact]
    public void SetText_EditsTheParagraphInsideTheControl()
    {
        string input = CreateDocument();
        string output = _fixture.Temp.File($"set-{Guid.NewGuid():N}.docx");

        _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new SetTextOp { At = new WordsTarget { Block = 3 }, Text = "Replaced" }],
        }, new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        StructuredDocumentTag control = Controls(document).First();
        Assert.Contains("Replaced", control.GetText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Inside two", control.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteBlocks_RemovesAControlItEmpties()
    {
        string input = CreateDocument();
        string output = _fixture.Temp.File($"delete-{Guid.NewGuid():N}.docx");

        _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new DeleteBlocksOp { Target = new WordsTarget { Blocks = "2-4" } }],
        }, new WordsEditRequest { OutputPath = output });

        Assert.Empty(Controls(new Document(output)));
        DocumentReadResult read = _fixture.Engine.Read(output, new DocumentReadRequest { Scope = "text" });
        Assert.Equal(["Before", "After"], read.Blocks.Select(static block => block.Text));
    }

    [Fact]
    public void SectionBreak_CannotSplitAControl()
    {
        string input = CreateDocument();

        CliException error = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new InsertBreakOp { At = new WordsTarget { Block = 2 }, Position = "after", Kind = "section" }],
        }, new WordsEditRequest { OutputPath = _fixture.Temp.File($"break-{Guid.NewGuid():N}.docx") }));

        Assert.Contains("content control", error.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<StructuredDocumentTag> Controls(Document document) =>
        document.GetChildNodes(NodeType.StructuredDocumentTag, true).Cast<StructuredDocumentTag>();

    /// <summary>A paragraph, a rich-text control holding two paragraphs and a nested control, and a paragraph.</summary>
    private string CreateDocument()
    {
        var document = new Document();
        Body body = document.FirstSection.Body;
        body.RemoveAllChildren();
        body.AppendChild(Paragraph(document, "Before"));
        StructuredDocumentTag control = Control(document, Paragraph(document, "Inside one"), Paragraph(document, "Inside two"));
        control.AppendChild(Control(document, Paragraph(document, "Nested")));
        body.AppendChild(control);
        body.AppendChild(Paragraph(document, "After"));
        string path = _fixture.Temp.File($"controls-{Guid.NewGuid():N}.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }

    private static StructuredDocumentTag Control(Document document, params Node[] blocks)
    {
        var control = new StructuredDocumentTag(document, SdtType.RichText, MarkupLevel.Block);
        control.RemoveAllChildren();
        foreach (Node block in blocks)
        {
            control.AppendChild(block);
        }

        return control;
    }

    private static Paragraph Paragraph(Document document, string text)
    {
        var paragraph = new Paragraph(document);
        paragraph.AppendChild(new Run(document, text));
        return paragraph;
    }
}
