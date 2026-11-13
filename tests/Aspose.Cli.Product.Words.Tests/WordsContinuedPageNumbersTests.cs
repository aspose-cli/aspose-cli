using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Words;
using Aspose.Words.Fields;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// set_page_numbers on a section that shows an earlier section's primary header or footer: the
/// section is numbered with that section when the operation numbers it too, and otherwise on a
/// copy of the story it shows, which becomes its own. Every section keeps showing what it showed,
/// with the page number added, and continues what it continued. Read back from the written file.
/// Evaluation mode gives every section a header and footer of its own as it loads, so no section
/// continues one there: those cases need a license, and no scenario, which CI runs without one,
/// can show them.
/// </summary>
public sealed partial class WordsContinuedPageNumbersTests : IClassFixture<WordsFixture>, IDisposable
{
    private const string Continuation = "Evaluation mode gives every section a header and footer of its own as it loads, so no section continues one.";
    private readonly TempWorkspace _workspace = new();

    /// <summary>The fixture applies the Words license in a licensed run before a test reads an output with the SDK.</summary>
    public WordsContinuedPageNumbersTests(WordsFixture fixture) => ArgumentNullException.ThrowIfNull(fixture);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// Sections 2 and 3 continue section 1's footer or header "Y". Each section reads back as
    /// whether the story it shows is its own and that story's lines, its page numbers as #; the
    /// numbered sections read back the operation's location, alignment and text, every section its
    /// start, a selected one that follows another's numbered story included. The targets list the
    /// sections whose own story or numbering changed, not one that only shows another's.
    /// </summary>
    [Theory]
    [InlineData("footer", """{"op":"set_page_numbers"}""", "own:Y / #|continues:Y / #|continues:Y / #", "section/1")]
    [InlineData("footer", """{"op":"set_page_numbers","start":5}""", "own:Y / #|continues:Y / #|continues:Y / #", "section/1,section/2,section/3")]
    [InlineData("footer", """{"op":"set_page_numbers","section":3}""", "own:Y|continues:Y|own:Y / #", "section/3")]
    [InlineData("footer", """{"op":"set_page_numbers","section":2}""", "own:Y|own:Y / #|continues:Y / #", "section/2")]
    [InlineData("header", """{"op":"set_page_numbers","location":"header"}""", "own:Y / #|continues:Y / #|continues:Y / #", "section/1")]
    [InlineData("footer", """{"op":"set_page_numbers","text":"Page {n} of {N}"}""", "own:Y / Page # of #|continues:Y / Page # of #|continues:Y / Page # of #", "section/1")]
    [InlineData("footer", """{"op":"set_page_numbers","section":3,"alignment":"right"}""", "own:Y|continues:Y|own:Y / #", "section/3")]
    public void SetPageNumbers_KeepsWhatEachSectionShows(string location, string operation, string expected, string targets)
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued(location, "Y"));

        CliResult edited = Edit(operation);

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        Assert.Equal(targets, string.Join(",", JsonNode.Parse(edited.StdOut)!["applied"]![0]!["targets"]!.AsArray().Select(static target => target!.GetValue<string>())));
        Assert.Equal(expected, Shown("numbered.docx", location == "header" ? HeaderFooterType.HeaderPrimary : HeaderFooterType.FooterPrimary));
        JsonNode op = JsonNode.Parse(operation)!;
        string[] lines = expected.Split('|');
        JsonNode[] sections = Sections("numbered.docx");
        for (int index = 0; index < lines.Length; index++)
        {
            JsonNode numbers = sections[index]["pageNumbers"]!;
            Assert.Equal(
                lines[index].Contains('#', StringComparison.Ordinal) ? (location, op["alignment"]?.GetValue<string>() ?? "center", op["text"]?.GetValue<string>()) : (null, null, null),
                (numbers["location"]?.GetValue<string>(), numbers["alignment"]?.GetValue<string>(), numbers["text"]?.GetValue<string>()));
            Assert.Equal(op["start"]?.GetValue<int>(), numbers["start"]?.GetValue<int>());
        }
    }

    /// <summary>
    /// Section 1's footer "Y" holds the page number and sections 2 and 3 continue it. Moving a
    /// section's number to the header clears it from the footer that section shows, a copy when
    /// another section owns that footer, so each page shows its number once, and section 1 keeps
    /// its footer; two moves in one batch each clear theirs. Each operation's targets are its own
    /// sections, not section 1, whose footer now numbers fewer pages. The lines read back are the
    /// footers, then the headers, each section's own or the one it continues, page numbers as #.
    /// </summary>
    [Theory]
    [InlineData("""{"op":"set_page_numbers","section":3,"location":"header"}""", "own:Y #|continues:Y #|own:", "continues:|continues:|own:Y #", "section/3")]
    [InlineData("""{"op":"set_page_numbers","section":2,"location":"header"}""", "own:Y #|own:|continues:", "continues:|own:Y #|continues:Y #", "section/2")]
    [InlineData("""{"op":"set_page_numbers","section":3,"location":"header"},{"op":"set_page_numbers","section":2,"location":"header"}""", "own:Y #|own:|own:", "continues:|own:Y #|own:Y #", "section/3|section/2")]
    public void MovingTheNumber_ClearsWhatEachSectionShowedWhereItLeft(string operations, string footers, string headers, string targets)
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued("footer", @"Y {\field{\*\fldinst PAGE}{\fldrslt 1}}"));

        CliResult edited = Edit(operations);

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.Equal(targets, string.Join("|", result["applied"]!.AsArray().Select(static applied => string.Join(",", applied!["targets"]!.AsArray().Select(static target => target!.GetValue<string>())))));
        Assert.Equal(footers, Shown("numbered.docx", HeaderFooterType.FooterPrimary));
        Assert.Equal(headers, Shown("numbered.docx", HeaderFooterType.HeaderPrimary));
    }

    /// <summary>
    /// A section whose footer's fields an operation really changes is reported, whether text
    /// replaces the page number or a number is added; only the page a PAGE field last showed is
    /// no change. One section with its own footer, so this runs without a license too.
    /// </summary>
    [Theory]
    [InlineData(@"Page {\field{\*\fldinst PAGE}{\fldrslt 1}}", """{"op":"set_footer","paragraphs":[{"text":"Z"}]}""")]
    [InlineData("Y", """{"op":"set_page_numbers"}""")]
    public void ChangingAFootersFields_ReportsItsSection(string footer, string operation)
    {
        File.WriteAllText(_workspace.File("continued.rtf"), @"{\rtf1\ansi{\footer\pard " + footer + @"\par}\pard Section 1.\par}");

        CliResult edited = Edit(operation);

        Assert.True(edited.ExitCode is 0 or 8, edited.StdErr + edited.StdOut);
        Assert.Equal(["section/1"], JsonNode.Parse(edited.StdOut)!["applied"]![0]!["targets"]!.AsArray().Select(static target => target!.GetValue<string>()));
    }

    /// <summary>
    /// --verify reads back where each set_page_numbers left a section's number: an output that
    /// still shows a page number in that location is a PAGE_NUMBERS_MISMATCH issue at the section,
    /// one that shows none is no issue, and a later set_page_numbers for the section decides instead.
    /// </summary>
    [Fact]
    public void Verify_ReadsBackTheLocationTheNumberLeft()
    {
        static Document Output(bool header, bool footer)
        {
            var document = new Document();
            var builder = new DocumentBuilder(document);
            builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
            builder.Write("H ");
            if (header)
            {
                builder.InsertField("PAGE");
            }

            builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
            builder.Write("F ");
            if (footer)
            {
                builder.InsertField("PAGE");
            }

            return document;
        }

        Document edited = Output(header: true, footer: false);
        var numbers = new Aspose.Cli.Product.Words.Engine.Editing.WordsPageNumberLosses([]);
        numbers.Numbered(new SetPageNumbersOp { Id = "op-0001", Location = "header" }, edited.FirstSection, HeaderFooterType.FooterPrimary);

        var issue = Assert.Single(numbers.Uncleared(Output(header: true, footer: true)));
        Assert.Equal(("PAGE_NUMBERS_MISMATCH", "section/1"), (issue.Code, issue.Location));
        Assert.Contains("footer there still shows a page number", issue.Message, StringComparison.Ordinal);
        Assert.Empty(numbers.Uncleared(Output(header: true, footer: false)));
        numbers.Numbered(new SetPageNumbersOp { Id = "op-0002", Location = "footer" }, edited.FirstSection, HeaderFooterType.HeaderPrimary);
        Assert.Empty(numbers.Uncleared(Output(header: false, footer: true)));
        Assert.Single(numbers.Uncleared(Output(header: true, footer: true)));
    }

    /// <summary>
    /// A copy keeps no bookmark: its name marks one place in the document, which the story it was
    /// copied from keeps, so that section still holds it and is not reported changed.
    /// </summary>
    [Fact]
    public void SetPageNumbers_LeavesABookmarkInTheStoryItCopies()
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued("footer", @"{\*\bkmkstart foot}Y{\*\bkmkend foot}"));

        CliResult edited = Edit("""{"op":"set_page_numbers","section":3}""");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        Assert.Equal(["section/3"], JsonNode.Parse(edited.StdOut)!["applied"]![0]!["targets"]!.AsArray().Select(static target => target!.GetValue<string>()));
        var document = new Document(_workspace.File("numbered.docx"));
        Assert.Equal(["foot"], document.Sections[0].HeadersFooters[HeaderFooterType.FooterPrimary].Range.Bookmarks.Cast<Bookmark>().Select(static bookmark => bookmark.Name));
        Assert.Empty(document.Sections[2].HeadersFooters[HeaderFooterType.FooterPrimary].Range.Bookmarks.Cast<Bookmark>());
        Assert.Equal("own:Y|continues:Y|own:Y / #", Shown("numbered.docx", HeaderFooterType.FooterPrimary));
    }

    /// <summary>
    /// The copy a section numbers is the story it showed as it was: its words, right tab stop,
    /// DATE field and picture, and its paragraph's alignment, while the section between keeps
    /// continuing the first's.
    /// </summary>
    [Fact]
    public void SetPageNumbers_NumbersAnExactCopyOfTheStoryASectionShowed()
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued("footer",
            @"\qr\tqr\tx4320 Y\tab{\field{\*\fldinst DATE \\@ ""yyyy""}{\fldrslt 2026}}{\pict\pngblip\picw1\pich1\picwgoal240\pichgoal240 " + Png + "}"));

        CliResult edited = Edit("""{"op":"set_page_numbers","section":3}""");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        var document = new Document(_workspace.File("numbered.docx"));
        Assert.Null(document.Sections[1].HeadersFooters[HeaderFooterType.FooterPrimary]);
        Paragraph copy = document.Sections[2].HeadersFooters[HeaderFooterType.FooterPrimary].FirstParagraph;
        Assert.Equal(Look(document.FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary].FirstParagraph), Look(copy));
        Assert.EndsWith("|ParagraphAlignment.Right|216/Right|FieldDate|1", Look(copy), StringComparison.Ordinal);

        static string Look(Paragraph paragraph) =>
            $"{paragraph.ToString(SaveFormat.Text).Trim()}|ParagraphAlignment.{paragraph.ParagraphFormat.Alignment}"
            + $"|{string.Join(",", Enumerable.Range(0, paragraph.ParagraphFormat.TabStops.Count).Select(index => $"{paragraph.ParagraphFormat.TabStops[index].Position}/{paragraph.ParagraphFormat.TabStops[index].Alignment}"))}"
            + $"|{string.Join(",", paragraph.Range.Fields.Cast<Field>().Select(static field => field.Type))}|{paragraph.GetChildNodes(NodeType.Shape, true).Count}";
    }

    /// <summary>The first-page footers, which only primary ones are numbered beside, show and continue what they did.</summary>
    [Fact]
    public void SetPageNumbers_LeavesFirstPageFootersAsTheyWere()
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued("footer", "Y", @"\titlepg{\footerf\pard First\par}"));

        CliResult edited = Edit("""{"op":"set_page_numbers"}""");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.Equal("own:First|continues:First|continues:First", Shown("continued.rtf", HeaderFooterType.FooterFirst));
        Assert.Equal(Shown("continued.rtf", HeaderFooterType.FooterFirst), Shown("numbered.docx", HeaderFooterType.FooterFirst));
        Assert.Equal("own:Y / #|continues:Y / #|continues:Y / #", Shown("numbered.docx", HeaderFooterType.FooterPrimary));
    }

    /// <summary>
    /// Sections with footers of their own, and a single section, are numbered in their own footers,
    /// as they were before a section's continued footer was copied. Evaluation mode gives them the
    /// same footers, so this runs without a license too.
    /// </summary>
    [Theory]
    [InlineData(@"{\footer\pard A\par}\pard Section 1.\par\sect\sectd{\footer\pard B\par}\pard Section 2.\par\sect\sectd{\footer\pard C\par}\pard Section 3.\par", "A / {PAGE}|B / {PAGE}|C / {PAGE}")]
    [InlineData(@"{\footer\pard A\par}\pard Section 1.\par", "A / {PAGE}")]
    public void SetPageNumbers_NumbersFootersOfTheirOwnInThem(string sections, string expected)
    {
        File.WriteAllText(_workspace.File("continued.rtf"), @"{\rtf1\ansi" + sections + "}");

        CliResult edited = Edit("""{"op":"set_page_numbers"}""");

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        Assert.Equal(expected, string.Join("|", Sections("numbered.docx").Select(static section => string.Join(" / ",
            section["headersFooters"]!.AsArray().Single(static story => story!["location"]!.GetValue<string>() == "footer" && story["kind"]!.GetValue<string>() == "primary")!
                ["paragraphs"]!.AsArray().Select(static paragraph => paragraph!["text"]!.GetValue<string>())))));
    }

    /// <summary>
    /// A PAGE or NUMPAGES field in the body is content: refreshing its result or making the text
    /// of it bold reports its block, while only the page a header's or footer's number last
    /// showed is no change. One section, so this runs without a license too.
    /// </summary>
    [Theory]
    [InlineData(@"\pard This report has {\field{\*\fldinst NUMPAGES}{\fldrslt 9}} pages.\par\pard Two.\par", """{"op":"refresh_fields"}""")]
    [InlineData(@"\pard Total: {\field{\*\fldinst NUMPAGES}{\fldrslt 1}}\par\pard Two.\par", """{"op":"format_text","target":{"block":1},"match":"1","bold":true}""")]
    public void ChangingAPageFieldInTheBody_ReportsItsBlock(string body, string operation)
    {
        File.WriteAllText(_workspace.File("continued.rtf"), @"{\rtf1\ansi" + body + "}");

        CliResult edited = Edit(operation);

        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal(["block/1"], result["applied"]![0]!["targets"]!.AsArray().Select(static target => target!.GetValue<string>()));
        Assert.DoesNotContain(result["warnings"]?.AsArray() ?? [], static warning => warning!["code"]!.GetValue<string>() == "OP_NO_EFFECT");
    }

    /// <summary>
    /// Under --best-effort, a set_page_numbers refused for a DATE field beside the number leaves
    /// --verify nothing to read back for it: the number stays in the footer, and no
    /// PAGE_NUMBERS_MISMATCH says it is still there. One section, so this runs without a license too.
    /// </summary>
    [Fact]
    public void ARefusedMove_LeavesNothingToReadBack()
    {
        File.WriteAllText(_workspace.File("continued.rtf"), @"{\rtf1\ansi{\footer\pard " + NumberAndDate + @"\par}\pard Body.\par}");

        CliResult edited = Edit("""{"op":"set_page_numbers","location":"header"}""", "--best-effort");

        Assert.True(edited.ExitCode == 8, edited.StdErr + edited.StdOut);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal("failed", result["applied"]![0]!["status"]!.GetValue<string>());
        Assert.DoesNotContain(result["verification"]!["issues"]!.AsArray(), static issue => issue!["code"]!.GetValue<string>() == "PAGE_NUMBERS_MISMATCH");
        Assert.Equal("footer", Sections("numbered.docx")[0]["pageNumbers"]!["location"]!.GetValue<string>());
    }

    /// <summary>
    /// Under --best-effort, the same refusal for section 3, which continues section 1's footer,
    /// changes nothing: section 3 gains no header or footer of its own and still continues, and no
    /// PAGE_NUMBERS_MISMATCH is read back for it.
    /// </summary>
    [Fact]
    public void ARefusedMove_LeavesTheSectionContinuing()
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued("footer", NumberAndDate));

        CliResult edited = Edit("""{"op":"set_page_numbers","section":3,"location":"header"}""", "--best-effort");

        Assert.True(edited.ExitCode == 8, edited.StdErr + edited.StdOut);
        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.Equal("failed", result["applied"]![0]!["status"]!.GetValue<string>());
        Assert.DoesNotContain(result["verification"]!["issues"]!.AsArray(), static issue => issue!["code"]!.GetValue<string>() == "PAGE_NUMBERS_MISMATCH");
        Assert.Empty(Sections("numbered.docx")[2]["headersFooters"]!.AsArray());
    }

    // A page number and a DATE field in one paragraph, which a move refuses to clear.
    private const string NumberAndDate = @"Page {\field{\*\fldinst PAGE}{\fldrslt 1}} | {\field{\*\fldinst DATE \\@ ""yyyy-MM-dd""}{\fldrslt 2026-01-01}}";

    /// <summary>
    /// --verify reads back each operation's sections: moving the whole document's number to the
    /// header and then section 1's back to the footer brings sections 2 and 3, which continue
    /// section 1, back with it, where the first operation left them no number, so the edit exits 8
    /// naming them.
    /// </summary>
    [Fact]
    public void Verify_ReportsSectionsALaterMoveBroughtBack()
    {
        TestLicense.Require(Continuation);
        File.WriteAllText(_workspace.File("continued.rtf"), Continued("footer", @"Y {\field{\*\fldinst PAGE}{\fldrslt 1}}"));

        CliResult edited = Edit("""{"op":"set_page_numbers","location":"header"},{"op":"set_page_numbers","section":1,"location":"footer"}""");

        Assert.True(edited.ExitCode == 8, edited.StdErr + edited.StdOut);
        Assert.Equal(["PAGE_NUMBERS_MISMATCH@section/2", "PAGE_NUMBERS_MISMATCH@section/3"], JsonNode.Parse(edited.StdOut)!["verification"]!["issues"]!.AsArray()
            .Select(static issue => $"{issue!["code"]}@{issue["location"]}"));
    }

    private CliResult Edit(string operation, params string[] options) =>
        _workspace.RunWithInput($$"""{"ops":[{{operation}}]}""", ["words", "edit", "continued.rtf", "--ops", "-", "--out", "numbered.docx", "--verify", "--output-format", "json", .. options, .. License()]);

    private JsonNode[] Sections(string path)
    {
        CliResult inspected = _workspace.Run(["words", "inspect", path, "--detail", "sections", "--output-format", "json", .. License()]);
        Assert.True(inspected.ExitCode == 0, inspected.StdErr + inspected.StdOut);
        return [.. JsonNode.Parse(inspected.StdOut)!["sections"]!.AsArray().Select(static section => section!)];
    }

    private static string[] License() => TestLicense.Path is { } path ? ["--license", path] : [];

    // Each section's story of a type: whether it is its own or one it continues, and that story's lines, page numbers as #.
    private string Shown(string path, HeaderFooterType type) =>
        string.Join("|", new Document(_workspace.File(path)).Sections.Cast<Section>().Select(section =>
        {
            Section? owner = section;
            while (owner is not null && owner.HeadersFooters[type] is null)
            {
                owner = owner.PreviousSibling as Section;
            }

            string lines = owner is null ? string.Empty : string.Join(" / ", owner.HeadersFooters[type].Paragraphs.Cast<Paragraph>()
                .Select(static paragraph => Digits().Replace(paragraph.ToString(SaveFormat.Text).Trim(), "#")));
            return $"{(section.HeadersFooters[type] is null ? "continues" : "own")}:{lines}";
        }));

    // Three sections, the second and third continuing the first's header or footer of one paragraph.
    private static string Continued(string location, string paragraph, string more = "") =>
        @"{\rtf1\ansi" + more + @"{\" + location + @"\pard " + paragraph + @"\par}\pard Section 1.\par\sect\sectd\pard Section 2.\par\sect\sectd\pard Section 3.\par}";

    // A one-pixel PNG, in RTF's hexadecimal.
    private const string Png = "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4890000000d4944415478da63f8cfc0f01f0005000201ffa2a39c780000000049454e44ae426082";

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();
}
