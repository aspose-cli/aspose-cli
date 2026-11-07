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
    [InlineData("page-footer", "section/1", """{"op":"set_page_numbers","location":"footer"}""")]
    [InlineData("numpages-footer", "section/1", """{"op":"set_page_numbers","location":"footer"}""")]
    [InlineData("sectionpages-header", "section/1", """{"op":"set_page_numbers","location":"header"}""")]
    [InlineData("markdown", "section/1", """{"op":"set_page_numbers","location":"footer"}""")]
    [InlineData("first-page", null, null)]
    [InlineData("even-page", null, null)]
    [InlineData("continued", "section/2", """{"op":"set_page_numbers","location":"footer","section":2}""")]
    [InlineData("renumbered", null, null)]
    [InlineData("renumbered-in-header", null, null)]
    [InlineData("unnumbered", null, null)]
    [InlineData("other-story", null, null)]
    [InlineData("kept-in-header", null, null)]
    public void ReplacingAHeaderOrFooter_ReportsThePageNumbersItRemoved(string example, string? location, string? renumbering)
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
        Assert.Equal([renumbering], Regex.Matches(warning.Hint!, """\{"op":"set_page_numbers"[^}]*\}""").Select(static match => match.Value));

        VerificationIssue issue = Assert.Single(issues);
        Assert.Equal((warning.Message, warning.Location, warning.Hint), (issue.Message, issue.Location, issue.Hint));
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
    /// When only some of the sections a set_footer replaced lose their page numbers, the hint
    /// numbers each of those by its section: run as it is, it gives the footer that lost them a
    /// page number again, leaves the cover's footer without one and keeps the page number another
    /// section shows in its header where it is.
    /// </summary>
    [Fact]
    public void Edit_TheHintNumbersOnlyTheSectionsThatLostTheirPageNumbers()
    {
        var source = new Document();
        var builder = new DocumentBuilder(source);
        builder.Write("Cover.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write("Cover footer");
        builder.MoveToDocumentEnd();
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Chapter.");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write("Page ");
        builder.InsertField("PAGE");
        builder.MoveToDocumentEnd();
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.Write("Appendix.");
        builder.MoveToHeaderFooter(HeaderFooterType.HeaderPrimary);
        builder.InsertField("PAGE");
        builder.MoveToHeaderFooter(HeaderFooterType.FooterPrimary);
        builder.Write("Appendix footer");
        source.Save(_workspace.File("sections.docx"), SaveFormat.Docx);
        const string footer = """{"op":"set_footer","paragraphs":[{"text":"Confidential"}]}""";

        CliResult reported = _workspace.RunWithInput($$"""{"ops":[{{footer}}]}""",
            "words", "edit", "sections.docx", "--ops", "-", "--out", "reported.docx", "--output-format", "json");

        Assert.True(reported.ExitCode == 0, reported.StdErr + reported.StdOut);
        JsonNode warning = JsonNode.Parse(reported.StdOut)!["warnings"]!.AsArray().Single(static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED")!;
        Assert.Equal("section/2", warning["location"]!.GetValue<string>());
        string[] operations = [.. Regex.Matches(warning["hint"]!.GetValue<string>(), """\{"op":"set_page_numbers"[^}]*\}""").Select(static match => match.Value)];
        Assert.Equal(["""{"op":"set_page_numbers","location":"footer","section":2}"""], operations);

        CliResult renumbered = _workspace.RunWithInput($$"""{"ops":[{{footer}},{{string.Join(",", operations)}}]}""",
            "words", "edit", "sections.docx", "--ops", "-", "--out", "renumbered.docx", "--verify", "--output-format", "json");

        Assert.True(renumbered.ExitCode == 0, renumbered.StdErr + renumbered.StdOut);
        Section[] sections = [.. new Document(_workspace.File("renumbered.docx")).Sections.Cast<Section>()];
        Assert.False(HoldsPage(sections[0].HeadersFooters[HeaderFooterType.FooterPrimary]));
        Assert.True(HoldsPage(sections[1].HeadersFooters[HeaderFooterType.FooterPrimary]));
        Assert.True(HoldsPage(sections[2].HeadersFooters[HeaderFooterType.HeaderPrimary]));
        Assert.False(HoldsPage(sections[2].HeadersFooters[HeaderFooterType.FooterPrimary]));

        static bool HoldsPage(HeaderFooter? story) =>
            story is not null && story.Range.Fields.Cast<Field>().Any(static field => field.Type == FieldType.FieldPage);
    }

    /// <summary>
    /// The built-in design numbers the pages in its footer: replacing the footer with text exits
    /// 8 under --verify and 0 without it, both reporting PAGE_NUMBERS_REMOVED; the batch with the
    /// operation the hint gives, run as it is, exits 0 with the page numbers back in the footer.
    /// </summary>
    [Fact]
    public void Edit_ReplacingTheBuiltInDesignsNumberedFooter_FailsVerificationUntilThePagesAreNumberedAgain()
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

        string operation = Regex.Match(warning["hint"]!.GetValue<string>(), """\{"op":"set_page_numbers"[^}]*\}""").Value;
        Assert.NotEmpty(operation);
        CliResult renumbered = _workspace.RunWithInput($$"""{"ops":[{{footer}},{{operation}}]}""",
            "words", "edit", "contract.docx", "--ops", "-", "--out", "renumbered.docx", "--verify", "--output-format", "json");

        Assert.True(renumbered.ExitCode == 0, renumbered.StdErr + renumbered.StdOut);
        JsonNode fixedResult = JsonNode.Parse(renumbered.StdOut)!;
        Assert.DoesNotContain(fixedResult["warnings"]?.AsArray() ?? [], static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED");
        Assert.True(fixedResult["verification"]!["ok"]!.GetValue<bool>());
        HeaderFooter numbered = new Document(_workspace.File("renumbered.docx")).FirstSection.HeadersFooters[HeaderFooterType.FooterPrimary];
        Assert.Contains("Confidential", numbered.GetText(), StringComparison.Ordinal);
        Assert.Contains(numbered.Range.Fields.Cast<Field>(), static field => field.Type == FieldType.FieldPage);
    }
}
