using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Notes;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>query search and replace_text read the same stories for the same scope name.</summary>
public sealed class WordsTextScopeTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsTextScopeTests(WordsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("body", new[] { "Body needle", "Box needle" })]
    [InlineData("headersFooters", new[] { "Header needle", "Footer needle" })]
    [InlineData("footnotes", new[] { "Footnote needle" })]
    [InlineData("comments", new[] { "Comment needle" })]
    public void Search_FindsEachStoryOnlyInItsOwnScope(string scope, string[] expected)
    {
        string input = CreateStories();

        WordsSearchResult result = _fixture.Engine.Search(input, WordsFixture.Search("needle", scope));

        Assert.All(result.Hits, hit => Assert.Equal(scope, hit.Scope));
        Assert.Equal(expected.Order(), result.Hits.SelectMany(static hit => Needles(hit.Snippet)).Order());
    }

    [Fact]
    public void Search_AllReportsEveryStoryOnce()
    {
        string input = CreateStories();

        WordsSearchResult result = _fixture.Engine.Search(input, WordsFixture.Search("needle", "all"));

        Assert.Equal(
            ["Body needle", "Box needle", "Comment needle", "Footer needle", "Footnote needle", "Header needle"],
            result.Hits.SelectMany(static hit => Needles(hit.Snippet)).Order());
    }

    [Fact]
    public void Search_NamesTheSectionOfAHeaderHitAndGivesItNoBlock()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("First body");
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Writeln("Second body needle");
        foreach (Section section in document.Sections)
        {
            var header = new HeaderFooter(document, HeaderFooterType.HeaderPrimary);
            header.AppendChild(new Paragraph(document));
            header.FirstParagraph!.AppendChild(new Run(document, $"Contract {document.Sections.IndexOf(section) + 1} needle"));
            section.HeadersFooters.Add(header);
        }
        string input = _fixture.Temp.File("section-headers.docx");
        document.Save(input);

        WordsSearchResult result = _fixture.Engine.Search(input, WordsFixture.Search("needle", "all"));

        WordsSearchHit body = Assert.Single(result.Hits, static hit => hit.Scope == "body");
        Assert.Equal(2, body.Section);
        Assert.NotNull(body.Block);
        WordsSearchHit[] headers = result.Hits.Where(static hit => hit.Scope == "headersFooters").ToArray();
        Assert.Equal([1, 2], headers.Select(static hit => hit.Section));
        Assert.All(headers, static hit => Assert.Null(hit.Block));
        Assert.Contains("Contract 2 needle", headers[1].Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadersAndFooters_AreNamedAsSetHeaderAndSetFooterNameThem()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Body needle");
        document.FirstSection.PageSetup.DifferentFirstPageHeaderFooter = true;
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Writeln("Contract needle");
        builder.Write("Confidential");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterFirst);
        builder.Write("Cover needle");
        string input = _fixture.Temp.File("named-headers.docx");
        document.Save(input);

        DocumentInfoResult info = _fixture.Engine.GetInfo(input, new DocumentInfoRequest { Details = ["sections"] });
        WordsSearchResult search = _fixture.Engine.Search(input, WordsFixture.Search("needle", "all"));
        WordsEditResult edit = _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new ReplaceTextOp { Scope = "all", Find = "needle", Replace = "pin" }] },
            new WordsEditRequest { OutputPath = _fixture.Temp.File("named-headers-out.docx") });

        Assert.Equivalent(
            new[]
            {
                new HeaderFooterData { Location = "header", Kind = "primary", Paragraphs = ["Contract needle", "Confidential"] },
                new HeaderFooterData { Location = "footer", Kind = "first", Paragraphs = ["Cover needle"] },
            },
            Assert.Single(info.Sections!).HeadersFooters.Where(static item => item.Paragraphs.Any(static text => text.Length > 0)),
            strict: true);
        // Evaluation mode adds a primary footer, which moves the stories of the section around.
        Assert.Equal(
            [(null, null), ("footer", "first"), ("header", "primary")],
            search.Hits.Select(static hit => (hit.Location, hit.Kind)).Order());
        Assert.Equal("block/1", edit.Applied[0].Targets[0]);
        Assert.Equal(["section/1/footer/first", "section/1/header/primary"], edit.Applied[0].Targets.Skip(1).Order());
    }

    [Theory]
    [InlineData("body", new[] { "Body pin", "Box pin" })]
    [InlineData("headersFooters", new[] { "Header pin", "Footer pin" })]
    [InlineData("footnotes", new[] { "Footnote pin" })]
    [InlineData("comments", new[] { "Comment pin" })]
    [InlineData("all", new[] { "Body pin", "Box pin", "Header pin", "Footer pin", "Footnote pin", "Comment pin" })]
    public void ReplaceText_ChangesExactlyTheStoriesSearchReads(string scope, string[] expected)
    {
        string input = CreateStories();
        string output = _fixture.Temp.File($"replaced-{scope}.docx");

        WordsEditResult result = _fixture.Engine.ApplyOps(
            input,
            new WordsOpsBatch { Ops = [new ReplaceTextOp { Scope = scope, Find = "needle", Replace = "pin" }] },
            new WordsEditRequest { OutputPath = output });

        var document = new Document(output);
        Assert.Equal(expected.Length, result.Applied[0].ItemsAffected);
        Assert.Equal(expected.Order(), Words(document.GetText(), "pin").Order());
        Assert.Contains("https://example.com/needle", document.GetText(), StringComparison.Ordinal);
        Assert.Contains("withdrawn needle", document.GetText(), StringComparison.Ordinal);
    }

    private static IEnumerable<string> Needles(string text) => Words(text, "needle");

    /// <summary>The "Label word" pairs in a text, such as "Body needle".</summary>
    private static IEnumerable<string> Words(string text, string word) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\b(Body|Box|Header|Footer|Footnote|Comment) " + word + @"\b")
            .Select(static match => match.Value);

    /// <summary>
    /// A body paragraph with a text box, a footnote, a comment, a hyperlink whose code holds the
    /// word and a tracked deletion of it, plus a header and a footer.
    /// </summary>
    private string CreateStories()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write("Body needle ");
        builder.InsertHyperlink("link", "https://example.com/needle", false);
        builder.InsertFootnote(FootnoteType.Footnote, "Footnote needle");
        Paragraph paragraph = document.FirstSection.Body.FirstParagraph;
        var comment = new Comment(document, "Reviewer", "R", DateTime.Now);
        comment.AppendChild(new Paragraph(document));
        comment.FirstParagraph!.AppendChild(new Run(document, "Comment needle"));
        paragraph.AppendChild(comment);
        var box = new Shape(document, ShapeType.TextBox) { Width = 120, Height = 40, WrapType = WrapType.Inline };
        box.AppendChild(new Paragraph(document));
        box.FirstParagraph.AppendChild(new Run(document, "Box needle"));
        paragraph.AppendChild(box);
        Run withdrawn = new(document, " withdrawn needle");
        paragraph.AppendChild(withdrawn);
        document.StartTrackRevisions("Reviewer", DateTime.Now);
        withdrawn.Remove();
        document.StopTrackRevisions();
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.Write("Header needle");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write("Footer needle");
        string path = _fixture.Temp.File($"stories-{Guid.NewGuid():N}.docx");
        document.Save(path, SaveFormat.Docx);
        return path;
    }
}
