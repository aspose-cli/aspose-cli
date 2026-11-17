using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Fields;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// set_header and set_footer replace a header or footer with its fields: when the primary one
/// they replace held a page number or page count (PAGE, NUMPAGES, SECTIONPAGES) and, once the
/// batch has run, the section's primary header and footer hold none, the edit reports
/// PAGE_NUMBERS_REMOVED, which leaves the output incomplete, so --verify reports it as an issue
/// and exits 8.
/// </summary>
public sealed class WordsPageNumbersRemovedTests : IClassFixture<WordsFixture>, IDisposable
{
    private readonly WordsFixture _fixture;
    private readonly TempWorkspace _workspace = new();

    public WordsPageNumbersRemovedTests(WordsFixture fixture) => _fixture = fixture;

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// The page numbers are reported removed for a PAGE, NUMPAGES or SECTIONPAGES field in the
    /// replaced primary footer or header, with paragraphs or Markdown, and in the primary footer a
    /// later section continues; not when the batch numbers the pages again with set_page_numbers
    /// in either location, when the replaced footer held no page number, when the operation
    /// replaced the other story, when the header still numbers the pages, or for a first-page or
    /// even-page footer, whose pages no operation numbers again.
    /// </summary>
    [Theory]
    [InlineData("page-footer", "section/1")]
    [InlineData("numpages-footer", "section/1")]
    [InlineData("sectionpages-header", "section/1")]
    [InlineData("markdown", "section/1")]
    [InlineData("first-page", null)]
    [InlineData("even-page", null)]
    [InlineData("continued", "section/2")]
    [InlineData("renumbered", null)]
    [InlineData("renumbered-in-header", null)]
    [InlineData("unnumbered", null)]
    [InlineData("other-story", null)]
    [InlineData("kept-in-header", null)]
    public void ReplacingAHeaderOrFooter_ReportsThePageNumbersItRemoved(string example, string? location)
    {
        if (example == "continued")
        {
            TestLicense.Require("Evaluation mode writes a footer of its own into every section, which then continues none.");
        }
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Body text.");
        WordsOp[] ops = Example(source, builder, example);
        string input = _fixture.Temp.File($"numbered-{example}.docx");
        source.Save(input, SaveFormat.Docx);

        WordsEditResult result = WordsEdit.Run(_fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = ops },
            Output = TestOutput.At(_fixture.Temp.File($"numbered-{example}-out.docx")),
            Verify = true,
        });

        Warning[] removed = [.. (result.Warnings ?? []).Where(static warning => warning.Code.Name == "PAGE_NUMBERS_REMOVED")];
        VerificationIssue[] issues = [.. result.Verification!.Issues.Where(static issue => issue.Code == "PAGE_NUMBERS_REMOVED")];
        if (location is null)
        {
            Assert.Empty(removed);
            Assert.Empty(issues);
            Assert.True(result.Verification.Ok, string.Join("; ", result.Verification.Issues.Select(static issue => issue.Message)));
            return;
        }

        Warning warning = Assert.Single(removed);
        Assert.True(warning.AffectsCompleteness);
        Assert.Equal(location, warning.Location);
        Assert.Equal("op-0001", warning.Details?["id"]?.GetValue<string>());
        Assert.Contains("which held the page numbers", warning.Message, StringComparison.Ordinal);

        VerificationIssue issue = Assert.Single(issues);
        Assert.Equal((warning.Message, warning.Location), (issue.Message, issue.Location));
        Assert.False(result.Verification.Ok);
        Assert.True(result.HasFailures);
    }

    /// <summary>The input of an example and the batch that replaces its header or footer.</summary>
    private static WordsOp[] Example(Document source, DocumentBuilder builder, string example)
    {
        SetFooterOp confidential = new() { Paragraphs = [new WordsParagraphInput { Text = "Confidential" }] };
        switch (example)
        {
            case "numpages-footer":
                builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
                builder.Write("Pages: ");
                builder.InsertField("NUMPAGES");
                return [confidential];
            case "sectionpages-header":
                builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
                builder.InsertField("SECTIONPAGES");
                return [new SetHeaderOp { Paragraphs = [new WordsParagraphInput { Text = "Annual report" }] }];
            case "markdown":
                Numbered(builder, HeaderFooterType.FooterPrimary);
                return [new SetFooterOp { Markdown = "Confidential" }];
            case "first-page":
                Numbered(builder, HeaderFooterType.FooterFirst);
                source.FirstSection.PageSetup.DifferentFirstPageHeaderFooter = true;
                return [new SetFooterOp { Kind = "first", Paragraphs = [new WordsParagraphInput { Text = "Cover" }] }];
            case "even-page":
                Numbered(builder, HeaderFooterType.FooterEven);
                source.FirstSection.PageSetup.OddAndEvenPagesHeaderFooter = true;
                return [new SetFooterOp { Kind = "even", Paragraphs = [new WordsParagraphInput { Text = "Even" }] }];
            case "continued":
                Numbered(builder, HeaderFooterType.FooterPrimary);
                builder.MoveToDocumentEnd();
                builder.InsertBreak(BreakType.SectionBreakNewPage);
                builder.Write("Second section.");
                Assert.Null(source.LastSection.HeadersFooters[HeaderFooterType.FooterPrimary]);
                return [new SetFooterOp { Section = 2, Paragraphs = [new WordsParagraphInput { Text = "Appendix" }] }];
            case "renumbered":
                Numbered(builder, HeaderFooterType.FooterPrimary);
                return [confidential, new SetPageNumbersOp()];
            case "renumbered-in-header":
                Numbered(builder, HeaderFooterType.FooterPrimary);
                return [confidential, new SetPageNumbersOp { Location = "header" }];
            case "unnumbered":
                builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
                builder.Write("Draft");
                return [confidential];
            case "other-story":
                Numbered(builder, HeaderFooterType.FooterPrimary);
                return [new SetHeaderOp { Paragraphs = [new WordsParagraphInput { Text = "Annual report" }] }];
            case "kept-in-header":
                Numbered(builder, HeaderFooterType.HeaderPrimary);
                Numbered(builder, HeaderFooterType.FooterPrimary);
                return [confidential];
            default:
                Numbered(builder, HeaderFooterType.FooterPrimary);
                return [confidential];
        }

        static void Numbered(DocumentBuilder builder, HeaderFooterType type)
        {
            builder.MoveToHeaderFooter(type);
            builder.Write("Page ");
            builder.InsertField("PAGE");
        }
    }

    /// <summary>
    /// The built-in design numbers the pages in its footer: replacing the footer with text exits
    /// 8 under --verify and 0 without it, both reporting PAGE_NUMBERS_REMOVED.
    /// </summary>
    [Fact]
    public void Edit_ReplacingTheBuiltInDesignsNumberedFooter_FailsVerification()
    {
        File.WriteAllText(_workspace.File("source.md"), "# Contract\n\nOriginal clause.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--from-markdown", "source.md").ExitCode);
        const string footer = """{"op":"set_footer","paragraphs":[{"text":"Confidential"}]}""";

        CliResult verified = _workspace.RunWithInput($$"""{"ops":[{{footer}}]}""",
            "words", "edit", "contract.docx", "--ops", "-", "--out", "verified.docx", "--verify", "--output-format", "json");
        CliResult plain = _workspace.RunWithInput($$"""{"ops":[{{footer}}]}""",
            "words", "edit", "contract.docx", "--ops", "-", "--out", "plain.docx", "--output-format", "json");

        Assert.True(verified.ExitCode == 8, verified.StdErr + verified.StdOut);
        JsonNode result = JsonNode.Parse(verified.StdOut)!;
        JsonNode warning = result["warnings"]!.AsArray().Single(static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED")!;
        Assert.True(warning["affectsCompleteness"]!.GetValue<bool>());
        Assert.Equal("section/1", warning["location"]!.GetValue<string>());
        Assert.False(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.Contains(result["verification"]!["issues"]!.AsArray(), static issue => issue!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED");
        Assert.True(plain.ExitCode == 0, plain.StdErr + plain.StdOut);
        Assert.Contains(JsonNode.Parse(plain.StdOut)!["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED");
    }

    /// <summary>
    /// Every section after the first continues its footer. A section that shows a footer an
    /// operation replaced loses its page numbers with it and is named: its own, one it continues,
    /// or the one it comes to continue when the replacement gives an earlier section a footer of
    /// its own, however the batch orders the replacements, by the last operation that replaced the
    /// footer it shows. A section that keeps showing a numbered footer keeps its numbers, and a
    /// later set_page_numbers reports nothing. Read back, exactly the named sections have no page
    /// number. Evaluation
    /// mode gives every section a footer of its own as it loads, so no section continues one there
    /// and no scenario, which CI runs without a license, can show it.
    /// </summary>
    [Theory]
    [InlineData(3, """[{"op":"set_footer","section":1,"paragraphs":[{"text":"Y"}]}]""", "op-0001: 1, 2, 3", "1, 2, 3")]
    [InlineData(3, """[{"op":"set_footer","section":2,"paragraphs":[{"text":"X"}]}]""", "op-0001: 2, 3", "2, 3")]
    [InlineData(3, """[{"op":"set_footer","section":1,"paragraphs":[{"text":"Y"}]},{"op":"set_footer","section":2,"paragraphs":[{"text":"X"}]}]""", "op-0001: 1|op-0002: 2, 3", "1, 2, 3")]
    [InlineData(3, """[{"op":"set_footer","section":2,"paragraphs":[{"text":"X"}]},{"op":"set_footer","section":1,"paragraphs":[{"text":"Y"}]}]""", "op-0001: 2, 3|op-0002: 1", "1, 2, 3")]
    [InlineData(4, """[{"op":"set_footer","section":3,"paragraphs":[{"text":"Z"}]}]""", "op-0001: 3, 4", "3, 4")]
    [InlineData(3, """[{"op":"set_footer","section":1,"paragraphs":[{"text":"Y"}]},{"op":"set_page_numbers","location":"footer"}]""", "", "")]
    public void ReplacingAFooterASectionContinues_NamesThatSectionToo(int sections, string footers, string named, string unnumbered)
    {
        TestLicense.Require("Evaluation mode gives every section a footer of its own as it loads, so no section continues one.");
        File.WriteAllText(_workspace.File("continued.rtf"), ContinuedFooter(sections));
        Assert.All(PageNumbers("continued.rtf"), static numbers => Assert.Equal(("footer", "right", "Page {n}"), Read(numbers)));
        JsonArray ops = JsonNode.Parse(footers)!.AsArray();

        CliResult reported = EditContinued(ops, "reported.docx");

        Assert.True(reported.ExitCode == (named.Length == 0 ? 0 : 8), reported.StdErr + reported.StdOut);
        JsonNode[] removed = [.. JsonNode.Parse(reported.StdOut)!["warnings"]?.AsArray().Select(static item => item!)
            .Where(static item => item["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED") ?? []];
        Assert.Equal(named, string.Join("|", removed.Select(static warning =>
            $"{warning["details"]!["id"]}: {Regex.Match(warning["message"]!.GetValue<string>(), @"section\(s\) ([\d, ]*\d)").Groups[1].Value}")));
        Assert.Equal(unnumbered, string.Join(", ", PageNumbers("reported.docx").Select(static (numbers, index) => (numbers, index))
            .Where(static item => item.numbers["location"] is null).Select(static item => item.index + 1)));

        static (string?, string?, string?) Read(JsonNode numbers) =>
            (numbers["location"]?.GetValue<string>(), numbers["alignment"]?.GetValue<string>(), numbers["text"]?.GetValue<string>());
    }

    // What each section's primary footer shows, its own or the one it continues, a line per paragraph, and whether it is its own.
    private (bool Own, string[] Lines)[] Footers(string path) =>
        [.. new Document(_workspace.File(path)).Sections.Cast<Section>().Select(static section =>
        {
            Section? shown = section;
            while (shown is not null && shown.HeadersFooters[HeaderFooterType.FooterPrimary] is null)
            {
                shown = shown.PreviousSibling as Section;
            }

            string[] lines = shown is null ? [] : [.. shown.HeadersFooters[HeaderFooterType.FooterPrimary].Paragraphs.Cast<Paragraph>()
                .Select(static paragraph => paragraph.ToString(SaveFormat.Text).Trim())];
            return (section.HeadersFooters[HeaderFooterType.FooterPrimary] is not null, lines);
        })];

    private CliResult EditContinued(JsonArray ops, string output) =>
        _workspace.RunWithInput(new JsonObject { ["ops"] = ops.DeepClone() }.ToJsonString(),
            "words", "edit", "continued.rtf", "--ops", "-", "--out", output, "--verify", "--license", TestLicense.Path!, "--output-format", "json");

    // Each section's page numbers, as inspect reads them.
    private JsonNode[] PageNumbers(string path)
    {
        CliResult inspected = _workspace.Run("words", "inspect", path, "--detail", "sections", "--license", TestLicense.Path!, "--output-format", "json");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr + inspected.StdOut);
        return [.. JsonNode.Parse(inspected.StdOut)!["sections"]!.AsArray().Select(static section => section!["pageNumbers"]!)];
    }

    // Sections that each continue the first's footer, "Page {n}" aligned right: RTF writes them without a footer of their own.
    private static string ContinuedFooter(int sections) =>
        @"{\rtf1\ansi{\footer\pard\qr Page {\field{\*\fldinst PAGE}{\fldrslt 1}}\par}\pard Section 1.\par"
        + string.Concat(Enumerable.Range(2, sections - 1).Select(static number => $@"\sect\sectd\pard Section {number}.\par")) + "}";
}
