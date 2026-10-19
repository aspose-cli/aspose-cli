using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Notes;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>words search and replace_text read the same stories for the same scope name.</summary>
public sealed class WordsTextScopeTests : IClassFixture<WordsFixture>
{
    private readonly WordsFixture _fixture;

    public WordsTextScopeTests(WordsFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("body", new[] { "Body needle", "Box needle" })]
    [InlineData("headers-footers", new[] { "Header needle", "Footer needle" })]
    [InlineData("footnotes", new[] { "Footnote needle" })]
    [InlineData("comments", new[] { "Comment needle" })]
    public void Search_FindsEachStoryOnlyInItsOwnScope(string scope, string[] expected)
    {
        string input = CreateStories();

        WordsSearchResult result = WordsSearch.Run(_fixture.Session, WordsFixture.Search(input, "needle", scope));

        Assert.All(result.Hits, hit => Assert.Equal(scope, hit.Scope));
        Assert.Equal(expected.Length, result.Hits.Count);
        Assert.Equal(expected.Order(), result.Hits.SelectMany(static hit => Needles(hit.Context)).Distinct().Order());
    }

    [Fact]
    public void Search_AllReportsEveryStoryOnce()
    {
        string input = CreateStories();

        WordsSearchResult result = WordsSearch.Run(_fixture.Session, WordsFixture.Search(input, "needle", "all"));

        Assert.Equal(6, result.Hits.Count);
        Assert.Equal(
            ["Body needle", "Box needle", "Comment needle", "Footer needle", "Footnote needle", "Header needle"],
            result.Hits.SelectMany(static hit => Needles(hit.Context)).Distinct().Order());
    }

    [Fact]
    public void ExtractedText_HoldsOnlyTheBodyAndAnyLineSearchReadsTheOtherStories()
    {
        var document = new Document(CreateStories());
        var builder = new DocumentBuilder(document);
        builder.MoveTo(document.FirstSection.Body.FirstParagraph);
        builder.InsertFootnote(FootnoteType.Endnote, "Endnote needle");
        string input = _fixture.Temp.File($"stories-endnote-{Guid.NewGuid():N}.docx");
        document.Save(input, SaveFormat.Docx);

        WordsExtractResult extracted = WordsExtract.Run(_fixture.Session, new WordsExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(_fixture.Temp.File($"stories-text-{Guid.NewGuid():N}")),
            What = "text",
        });

        string text = File.ReadAllText(Assert.Single(extracted.Outputs).Path);
        Assert.Contains("Body needle", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Needles(text), static needle => needle is not ("Body needle" or "Box needle"));
        Assert.DoesNotContain("Endnote needle", text, StringComparison.Ordinal);
        // Evaluation mode adds its own header line, so only the lines of the stories are compared.
        Assert.Equal(
            [("comments", "Comment needle"), ("footnotes", "Endnote needle"), ("footnotes", "Footnote needle"),
                ("headers-footers", "Footer needle"), ("headers-footers", "Header needle")],
            AnyLine(input, WordsTextScopes.All).Where(static line => line.Scope != WordsTextScopes.Body));
        Assert.Equal([("footnotes", "Endnote needle"), ("footnotes", "Footnote needle")], AnyLine(input, WordsTextScopes.Footnotes));
    }

    /// <summary>The scope and text of each line an any-line search returns that holds a needle, in order.</summary>
    private IEnumerable<(string Scope, string Text)> AnyLine(string input, string scope) =>
        WordsSearch.Run(_fixture.Session, new WordsSearchRequest
            {
                Input = input,
                Query = new SearchQuery(TextSearch.Create(".+", regex: true, caseSensitive: false), 100, scope),
            })
            .Hits.Where(static hit => hit.Text.Contains("needle", StringComparison.Ordinal))
            .Select(static hit => (hit.Scope, hit.Text.Trim())).Order();

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

        WordsSearchResult result = WordsSearch.Run(_fixture.Session, WordsFixture.Search(input, "needle", "all"));

        WordsSearchHit body = Assert.Single(result.Hits, static hit => hit.Scope == "body");
        Assert.Equal(2, body.Section);
        Assert.NotNull(body.Block);
        WordsSearchHit[] headers = result.Hits.Where(static hit => hit.Scope == "headers-footers").ToArray();
        Assert.Equal([1, 2], headers.Select(static hit => hit.Section));
        Assert.All(headers, static hit => Assert.Null(hit.Block));
        Assert.Contains("Contract 2 needle", headers[1].Context, StringComparison.Ordinal);
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

        WordsInspectResult info = WordsInspect.Run(_fixture.Session, new WordsInspectRequest { Input = input, Details = ["sections"] });
        WordsSearchResult search = WordsSearch.Run(_fixture.Session, WordsFixture.Search(input, "needle", "all"));
        WordsEditResult edit = WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Scope = "all", Find = "needle", Replace = "pin" }] },
                Output = TestOutput.At(_fixture.Temp.File("named-headers-out.docx")),
            });

        // In any order: evaluation mode's primary footer moves the stories of the section around.
        Assert.Equal(
            ["footer/first: Cover needle", "header/primary: Contract needle|Confidential"],
            Assert.Single(info.Sections!).HeadersFooters.Where(static item => item.Paragraphs.Any(static paragraph => paragraph.Text.Length > 0))
                .Select(static item => $"{item.Location}/{item.Kind}: {string.Join("|", item.Paragraphs.Select(static paragraph => paragraph.Text))}")
                .Order(StringComparer.Ordinal));
        // Evaluation mode adds a primary footer, which moves the stories of the section around.
        Assert.Equal(
            [(null, null), ("footer", "first"), ("header", "primary")],
            search.Hits.Select(static hit => (hit.Location, hit.Kind)).Order());
        // A header or footer counts as its section.
        Assert.Equal(["block/1", "section/1"], edit.Applied[0].Targets);
    }

    /// <summary>
    /// A replace takes each match the engine finds from left to right, without overlaps, so a
    /// run of spaces or letters loses half of itself in one pass.
    /// </summary>
    [Theory]
    [InlineData("a    b", "  ", " ", false, "a  b")]
    [InlineData("aaaa", "aa", "a", false, "aa")]
    [InlineData("Year 1999", @"\d\d", "XX", true, "Year XXXX")]
    public void ReplaceText_TakesMatchesLeftToRightWithoutOverlap(string text, string find, string replace, bool regex, string expected)
    {
        var document = new Document();
        new DocumentBuilder(document).Write(text);
        string name = Guid.NewGuid().ToString("N");
        string input = _fixture.Temp.File($"overlap-{name}.docx");
        document.Save(input);
        string output = _fixture.Temp.File($"overlap-{name}-out.docx");

        WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = find, Replace = replace, Regex = regex }] },
                Output = TestOutput.At(output),
            });

        Assert.Contains(
            new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>().Select(static paragraph => paragraph.GetText().Trim()),
            paragraph => paragraph == expected);
    }

    /// <summary>
    /// A whole-word, case-insensitive replace takes only the words themselves, in document order,
    /// up to maxReplacementCount.
    /// </summary>
    [Theory]
    [InlineData(null, "dog dog catalog dog.|dog's tail")]
    [InlineData(2, "dog dog catalog cat.|cat's tail")]
    public void ReplaceText_TakesWholeWordsInOrder(int? maximum, string expected)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Cat cat catalog cat.");
        builder.Write("cat's tail");
        string input = _fixture.Temp.File($"words-{maximum}.docx");
        document.Save(input);
        string output = _fixture.Temp.File($"words-{maximum}-out.docx");

        WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "cat", Replace = "dog", WholeWord = true, MaxReplacementCount = maximum }] },
                Output = TestOutput.At(output),
            });

        Assert.Equal(
            expected,
            string.Join('|', new Document(output).FirstSection.Body.Paragraphs.Cast<Paragraph>().Select(static paragraph => paragraph.GetText().Trim())
                .Where(static text => text.Length > 0 && !text.Contains("Aspose", StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("body", new[] { "Body pin", "Box pin" })]
    [InlineData("headers-footers", new[] { "Header pin", "Footer pin" })]
    [InlineData("footnotes", new[] { "Footnote pin" })]
    [InlineData("comments", new[] { "Comment pin" })]
    [InlineData("all", new[] { "Body pin", "Box pin", "Header pin", "Footer pin", "Footnote pin", "Comment pin" })]
    public void ReplaceText_ChangesExactlyTheStoriesSearchReads(string scope, string[] expected)
    {
        string input = CreateStories();
        string output = _fixture.Temp.File($"replaced-{scope}.docx");

        WordsEditResult result = WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Scope = scope, Find = "needle", Replace = "pin" }] },
                Output = TestOutput.At(output),
            });

        var document = new Document(output);
        // Each block and section whose text changed counts once, however many matches it held.
        Assert.Equal(result.Applied[0].Targets.Count, result.Applied[0].ItemsAffected);
        Assert.Equal(scope == "headers-footers", result.Applied[0].Targets.All(static target => target.StartsWith("section/", StringComparison.Ordinal)));
        Assert.Equal(expected.Order(), Words(document.GetText(), "pin").Order());
        Assert.Contains("https://example.com/needle", document.GetText(), StringComparison.Ordinal);
        Assert.Contains("withdrawn needle", document.GetText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// replace_text replaces exactly the matches words search reports for the same pattern and
    /// scope, anchors included: each paragraph, comment and note is one text, as search reads it
    /// (WORDS-REGEX-ANCHORS).
    /// </summary>
    [Theory]
    [InlineData(@"^\w+")]
    [InlineData(@"\w+$")]
    [InlineData(@"(?m)^\w+ \w+")]
    [InlineData(@"e\b")]
    [InlineData(@"\Aneedle|needle\z")]
    public void ReplaceText_ReplacesTheMatchesSearchFinds(string pattern)
    {
        string input = CreateStories();
        string output = _fixture.Temp.File($"marked-{Guid.NewGuid():N}.docx");
        WordsSearchResult found = WordsSearch.Run(_fixture.Session, Search(input, pattern));

        WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Scope = WordsTextScopes.All, Find = pattern, Regex = true, Replace = "<$0>" }] },
                Output = TestOutput.At(output),
            });
        WordsSearchResult marked = WordsSearch.Run(_fixture.Session, Search(output, "<[^<>]+>"));

        Assert.NotEmpty(found.Hits);
        Assert.Equal(
            found.Hits.Select(static hit => (hit.Scope, hit.Text)),
            marked.Hits.Select(static hit => (hit.Scope, hit.Text[1..^1])));

        static WordsSearchRequest Search(string input, string pattern) => new()
        {
            Input = input,
            Query = new SearchQuery(TextSearch.Create(pattern, regex: true, caseSensitive: false), 100, WordsTextScopes.All),
        };
    }

    /// <summary>
    /// A match that starts before a hyperlink or a REF field and ends in its result puts the new
    /// text where the match starts, outside the field, which stays and still updates.
    /// </summary>
    [Theory]
    [InlineData("hyperlink", "get")]
    [InlineData("ref", "Target")]
    public void ReplaceText_IntoAFieldResult_WritesTheNewTextBeforeTheField(string kind, string updated)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.StartBookmark("Clause");
        builder.Write("Target");
        builder.EndBookmark("Clause");
        builder.Writeln();
        builder.Write("See clause ");
        Aspose.Words.Fields.Field inserted = kind == "ref" ? builder.InsertField("REF Clause") : builder.InsertHyperlink("Target", "https://example.com/", false);
        builder.Write(" now.");
        inserted.Update();
        string input = _fixture.Temp.File($"field-{kind}-{Guid.NewGuid():N}.docx");
        document.Save(input);
        string output = _fixture.Temp.File($"field-{kind}-{Guid.NewGuid():N}-out.docx");

        WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "clause Tar", Replace = "Zed" }] },
                Output = TestOutput.At(output),
            });

        var edited = new Document(output);
        Run replaced = edited.GetChildNodes(NodeType.Run, true).Cast<Run>().Single(static run => run.Text.Contains("Zed", StringComparison.Ordinal));
        Paragraph paragraph = replaced.ParentParagraph;
        // Evaluation mode adds a hyperlink of its own in another paragraph.
        Aspose.Words.Fields.Field field = Assert.Single(paragraph.Range.Fields.Cast<Aspose.Words.Fields.Field>());
        Assert.True(paragraph.IndexOf(replaced) < paragraph.IndexOf(field.Start), "The new text lies inside the field.");
        field.Update();
        Assert.Equal(updated, field.Result);
    }

    /// <summary>
    /// The replacement is text: an ampersand or caret code such as &amp;p or ^p, and a line
    /// feed, stay the characters they are and break no paragraph, section or page.
    /// </summary>
    [Theory]
    [InlineData("A&pB")]
    [InlineData("A&mB")]
    [InlineData("A&bB")]
    [InlineData("A&&B")]
    [InlineData("A^pB")]
    public void ReplaceText_InsertsTheReplacementAsText(string replace)
    {
        var document = new Document();
        new DocumentBuilder(document).Write("Party X pays.");
        string input = _fixture.Temp.File($"codes-{Guid.NewGuid():N}.docx");
        document.Save(input);
        string output = _fixture.Temp.File($"codes-{Guid.NewGuid():N}-out.docx");

        WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "X", Replace = replace }] },
                Output = TestOutput.At(output),
            });

        var edited = new Document(output);
        Assert.Equal(1, edited.Sections.Count);
        Assert.Contains(
            edited.FirstSection.Body.Paragraphs.Cast<Paragraph>().Select(static paragraph => paragraph.GetText().TrimEnd('\r', '\f')),
            text => text == $"Party {replace} pays.");
    }

    [Fact]
    public void ReplaceText_DrawsNewEastAsianTextInTheEastAsianFontOfItsParagraph()
    {
        // As a PDF loads: the digits in runs whose fonts, East Asian included, are all Calibri.
        var document = new Document();
        Paragraph paragraph = document.FirstSection.Body.FirstParagraph;
        foreach ((string text, string font) in new[] { ("合同总价人民币", "Microsoft YaHei"), (" 1,860,000.00 ", "Calibri"), ("元（大写：壹佰捌拾陆万元整 ）。", "Microsoft YaHei") })
        {
            var run = new Run(document, text);
            run.Font.Name = font;
            paragraph.AppendChild(run);
        }
        string input = _fixture.Temp.File("amount.docx");
        document.Save(input);
        string output = _fixture.Temp.File("amount-out.docx");

        WordsEdit.Run(
            _fixture.Session,
            new WordsEditRequest
            {
                Input = input,
                Batch = new WordsOpsBatch { Ops = [new ReplaceTextOp { Find = "1,860,000.00 元（大写：壹佰捌拾陆万元整 ）", Replace = "1,920,000.00 元（大写：壹佰玖拾贰万元整）" }] },
                Output = TestOutput.At(output),
            });

        Run replaced = new Document(output).GetChildNodes(NodeType.Run, true).Cast<Run>()
            .Single(static run => run.Text.Contains("壹佰玖拾贰", StringComparison.Ordinal));
        Assert.Equal("Microsoft YaHei", replaced.Font.NameFarEast);
        Assert.Equal("Calibri", replaced.Font.Name);
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
