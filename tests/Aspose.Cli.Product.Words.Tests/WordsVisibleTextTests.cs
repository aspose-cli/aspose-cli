using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Notes;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Blocks, search snippets and text addresses read the text a reader sees: field results
/// rather than codes, no text a tracked change deletes, and no comment or footnote text
/// inside the paragraph that anchors it.
/// </summary>
public sealed class WordsVisibleTextTests : IClassFixture<WordsFixture>
{
    private const string Visible = "See the link text and more.";

    private readonly WordsFixture _fixture;

    public WordsVisibleTextTests(WordsFixture fixture) => _fixture = fixture;

    [Fact]
    public void Blocks_ProjectFieldResultsWithoutCodesDeletionsOrAnchoredNotes()
    {
        string input = CreateAnnotatedParagraph();

        DocumentReadResult read = _fixture.Engine.Read(input, new DocumentReadRequest { Scope = "text" });

        Assert.Equal(Visible, read.Blocks[0].Text);
    }

    [Fact]
    public void SearchSnippetsAndFindAddresses_UseTheSameVisibleText()
    {
        string input = CreateAnnotatedParagraph();

        WordsSearchResult link = _fixture.Engine.Search(input, new WordsSearchRequest { Pattern = "link text" });
        WordsSearchResult code = _fixture.Engine.Search(input, new WordsSearchRequest { Pattern = "HYPERLINK" });
        WordsSearchResult deleted = _fixture.Engine.Search(input, new WordsSearchRequest { Pattern = "withdrawn" });
        CliException anchor = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new SetTextOp { At = new WordsTarget { Find = "example.com" }, Text = "x" }] },
            new WordsEditRequest { OutputPath = _fixture.Temp.File("anchor.docx") }));

        Assert.Equal(Visible, Assert.Single(link.Hits).Snippet);
        Assert.Empty(code.Hits);
        Assert.Empty(deleted.Hits);
        Assert.Equal(WordsDiagnostics.AnchorNotFound, anchor.Code);
    }

    [Fact]
    public void ExtractedText_IsTheVisibleText()
    {
        string input = CreateAnnotatedParagraph();

        WordsExtractResult result = _fixture.Engine.Extract(input, new WordsExtractRequest
        {
            What = "text",
            OutputDirectory = _fixture.Temp.File($"text-{Guid.NewGuid():N}"),
        });

        Assert.Equal(Visible, File.ReadAllText(Assert.Single(result.Items).Path));
    }

    [Fact]
    public void CommentText_ShowsFieldResults()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Commented");
        var comment = new Comment(document, "Reviewer", "R", DateTime.Now);
        comment.AppendChild(new Paragraph(document));
        builder.CurrentParagraph.AppendChild(comment);
        builder.MoveTo(comment.FirstParagraph);
        builder.Write("See ");
        builder.InsertHyperlink("the source", "https://example.com/source", false);
        string input = _fixture.Temp.File("comment-field.docx");
        document.Save(input, SaveFormat.Docx);

        DocumentInfoResult info = _fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["comments"] });

        Assert.Equal("See the source", Assert.Single(info.Comments!).Text);
    }

    /// <summary>
    /// One paragraph holding a hyperlink field, a footnote, a comment and a tracked deletion
    /// whose visible text is <see cref="Visible"/>.
    /// </summary>
    private string CreateAnnotatedParagraph()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("See the ");
        builder.InsertHyperlink("link text", "https://example.com/target", false);
        builder.Write(" and");
        builder.InsertFootnote(FootnoteType.Footnote, "Footnote words.");
        Paragraph paragraph = document.FirstSection.Body.FirstParagraph;
        var comment = new Comment(document, "Reviewer", "R", DateTime.Now);
        comment.AppendChild(new Paragraph(document));
        comment.FirstParagraph!.AppendChild(new Run(document, "Comment words."));
        paragraph.AppendChild(comment);
        paragraph.AppendChild(new Run(document, " more"));
        Run withdrawn = new(document, " withdrawn");
        paragraph.AppendChild(withdrawn);
        paragraph.AppendChild(new Run(document, "."));
        document.StartTrackRevisions("Reviewer", DateTime.Now);
        withdrawn.Remove();
        document.StopTrackRevisions();
        Assert.True(document.HasRevisions);
        string path = _fixture.Temp.File($"annotated-{Guid.NewGuid():N}.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }
}
