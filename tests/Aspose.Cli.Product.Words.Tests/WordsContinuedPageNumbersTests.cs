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

    private CliResult Edit(string operation) =>
        _workspace.RunWithInput($$"""{"ops":[{{operation}}]}""", ["words", "edit", "continued.rtf", "--ops", "-", "--out", "numbered.docx", "--verify", "--output-format", "json", .. License()]);

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
