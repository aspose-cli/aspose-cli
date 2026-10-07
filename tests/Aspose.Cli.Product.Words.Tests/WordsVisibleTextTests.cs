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
    public void FullScopeRuns_JoinToTheBlockText()
    {
        string input = CreateAnnotatedParagraph();

        DocumentReadResult read = _fixture.Engine.Read(input, new DocumentReadRequest { Scope = "full" });

        Assert.Equal(Visible, string.Concat(read.Blocks[0].Runs!.Select(static run => run.Text)));
    }

    [Fact]
    public void SearchSnippetsAndFindAddresses_UseTheSameVisibleText()
    {
        string input = CreateAnnotatedParagraph();

        WordsSearchResult link = _fixture.Engine.Search(input, WordsFixture.Search("link text"));
        WordsSearchResult code = _fixture.Engine.Search(input, WordsFixture.Search("HYPERLINK"));
        WordsSearchResult deleted = _fixture.Engine.Search(input, WordsFixture.Search("withdrawn"));
        CliException anchor = Assert.Throws<CliException>(() => _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new SetTextOp { At = new WordsTarget { Find = "example.com" }, Text = "x" }] },
            new WordsEditRequest { Output = TestOutput.At(_fixture.Temp.File("anchor.docx")) }));

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
            Output = new ResolvedDirectory(_fixture.Temp.File($"text-{Guid.NewGuid():N}")),
            What = "text",
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

    [Fact]
    public void ListParagraphs_ReadTheirNumberAsListLabelBesideTheirText()
    {
        string input = CreateNumberedClauses();

        DocumentReadResult read = _fixture.Engine.Read(input, new DocumentReadRequest { Scope = "full" });

        Assert.Equal(["1.", "1.1", "1.2"], read.Blocks.Select(static block => block.ListLabel));
        Assert.Equal("First clause", read.Blocks[1].Text);
        Assert.Equal("First clause", string.Concat(read.Blocks[1].Runs!.Select(static run => run.Text)));
    }

    [Fact]
    public void SearchFindAndExtractedText_ReadTheListNumberBeforeTheText()
    {
        string input = CreateNumberedClauses();
        string output = _fixture.Temp.File($"numbered-{Guid.NewGuid():N}.docx");

        WordsSearchResult search = _fixture.Engine.Search(input, WordsFixture.Search("1.2"));
        _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new SetTextOp { At = new WordsTarget { Find = "1.2 Second" }, Text = "Changed clause" }] },
            new WordsEditRequest { Output = TestOutput.At(output) });
        WordsExtractResult extracted = _fixture.Engine.Extract(output, new WordsExtractRequest
        {
            Output = new ResolvedDirectory(_fixture.Temp.File($"numbered-text-{Guid.NewGuid():N}")),
            What = "text",
        });

        Assert.Equal("1.2 Second clause", Assert.Single(search.Hits).Snippet);
        Assert.Equal("1. Scope\n1.1 First clause\n1.2 Changed clause", File.ReadAllText(Assert.Single(extracted.Items).Path));
    }

    /// <summary>A heading numbered 1. and two clauses numbered 1.1 and 1.2 below it.</summary>
    private string CreateNumberedClauses()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        Aspose.Words.Lists.List list = document.Lists.Add(Aspose.Words.Lists.ListTemplate.NumberDefault);
        list.ListLevels[1].NumberStyle = NumberStyle.Arabic;
        list.ListLevels[1].NumberFormat = "\u0000.\u0001";
        builder.ListFormat.List = list;
        builder.Writeln("Scope");
        builder.ListFormat.ListLevelNumber = 1;
        builder.Writeln("First clause");
        builder.Write("Second clause");
        string path = _fixture.Temp.File($"numbered-{Guid.NewGuid():N}.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
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
