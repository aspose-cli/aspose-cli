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
    [InlineData("page-footer", "section/1", """{"op":"set_page_numbers","location":"footer","alignment":"left","text":"Page {n}"}""")]
    [InlineData("numpages-footer", "section/1", """{"op":"set_page_numbers","location":"footer"}""")]
    [InlineData("sectionpages-header", "section/1", """{"op":"set_page_numbers","location":"header"}""")]
    [InlineData("markdown", "section/1", """{"op":"set_page_numbers","location":"footer","alignment":"left","text":"Page {n}"}""")]
    [InlineData("first-page", null, null)]
    [InlineData("even-page", null, null)]
    [InlineData("continued", "section/2", """{"op":"set_page_numbers","location":"footer","section":2,"alignment":"left","text":"Page {n}"}""")]
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
        Assert.Equal([renumbering], Regex.Matches(warning.Hint!, SetPageNumbers).Select(static match => match.Value));

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
        string[] operations = [.. Regex.Matches(warning["hint"]!.GetValue<string>(), SetPageNumbers).Select(static match => match.Value)];
        Assert.Equal(["""{"op":"set_page_numbers","location":"footer","section":2,"alignment":"left","text":"Page {n}"}"""], operations);

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

        string operation = Regex.Match(warning["hint"]!.GetValue<string>(), SetPageNumbers).Value;
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

    /// <summary>
    /// Every section after the first continues its footer. A section that shows a footer an
    /// operation replaced loses its page numbers with it and is named: its own, one it continues,
    /// or the one it comes to continue when the replacement gives an earlier section a footer of
    /// its own, however the batch orders the replacements, by the last operation that replaced the
    /// footer it shows. A section that keeps showing a numbered footer keeps its numbers, and a
    /// later set_page_numbers reports nothing. Read back, exactly the named sections have no page
    /// number. Each hint, run as it is after its operation, numbers only the sections whose footer
    /// the operation wrote, so every footer shows what it showed before with the page number as
    /// it read added, every section continues what it continued, and --verify passes. Evaluation
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
        if (named.Length == 0)
        {
            return;
        }

        var renumbered = new JsonArray();
        for (int index = 0; index < ops.Count; index++)
        {
            renumbered.Add(ops[index]!.DeepClone());
            string id = $"op-{index + 1:D4}";
            foreach (JsonNode warning in removed.Where(warning => warning["details"]!["id"]!.GetValue<string>() == id))
            {
                foreach (Match operation in Regex.Matches(warning["hint"]!.GetValue<string>(), SetPageNumbers))
                {
                    renumbered.Add(JsonNode.Parse(operation.Value));
                }
            }
        }

        CliResult fixedResult = EditContinued(renumbered, "renumbered.docx");

        Assert.True(fixedResult.ExitCode == 0, fixedResult.StdErr + fixedResult.StdOut);
        Assert.True(JsonNode.Parse(fixedResult.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        Assert.All(PageNumbers("renumbered.docx"), static numbers => Assert.Equal(("footer", "right", "Page {n}"), Read(numbers)));
        (bool Own, string[] Lines)[] before = Footers("reported.docx"), after = Footers("renumbered.docx");
        string[] lost = unnumbered.Split(", ");
        Assert.All(Enumerable.Range(0, sections), index =>
        {
            Assert.Equal(before[index].Own, after[index].Own);
            bool renumberedHere = lost.Contains($"{index + 1}");
            Assert.Equal(before[index].Lines, renumberedHere ? after[index].Lines[..^1] : after[index].Lines);
            Assert.True(!renumberedHere || after[index].Lines[^1].StartsWith("Page ", StringComparison.Ordinal), string.Join(" / ", after[index].Lines));
        });

        static (string?, string?, string?) Read(JsonNode numbers) =>
            (numbers["location"]?.GetValue<string>(), numbers["alignment"]?.GetValue<string>(), numbers["text"]?.GetValue<string>());
    }

    /// <summary>
    /// The page number shares its paragraph with a field no set_page_numbers text writes, which
    /// replacing the footer drops: the hint numbers the pages again without the paragraph's words
    /// and names the field, rather than give a text that is refused, empty, or that fixes the
    /// field's last result as words. Run as it is, it exits 0, passes --verify and numbers the
    /// pages where they were. Words with {n} and {N} alone come back as they read.
    /// </summary>
    [Theory]
    [InlineData(@"Page {\field{\*\fldinst PAGE}{\fldrslt 1}} of {\field{\*\fldinst SECTIONPAGES}{\fldrslt 1}}", "SECTIONPAGES", null)]
    [InlineData(@"{\field{\*\fldinst IF {\field{\*\fldinst PAGE}{\fldrslt 1}} > 1 ""Page {\field{\*\fldinst PAGE}{\fldrslt 1}}"" """"}{\fldrslt }}", "IF", null)]
    [InlineData(@"Page {\field{\*\fldinst PAGE}{\fldrslt 1}} of {\field{\*\fldinst NUMPAGES}{\fldrslt 1}} - {\field{\*\fldinst DATE \\@ ""yyyy-MM-dd""}{\fldrslt 2026-01-01}}", "DATE", null)]
    [InlineData(@"Page {\field{\*\fldinst PAGE}{\fldrslt 1}} of {\field{\*\fldinst NUMPAGES}{\fldrslt 1}}", null, "Page {n} of {N}")]
    public void ReplacingAFooterWhoseNumberSitsBesideAnotherField_RenumbersWithoutWordsNoTextWrites(string footer, string? field, string? text)
    {
        File.WriteAllText(_workspace.File("footer.rtf"), @"{\rtf1\ansi{\footer\pard\qr " + footer + @"\par}\pard Body text.\par}");
        const string confidential = """{"op":"set_footer","paragraphs":[{"text":"Confidential"}]}""";

        CliResult reported = _workspace.RunWithInput($$"""{"ops":[{{confidential}}]}""",
            "words", "edit", "footer.rtf", "--ops", "-", "--out", "reported.docx", "--output-format", "json");

        Assert.True(reported.ExitCode == 0, reported.StdErr + reported.StdOut);
        string hint = JsonNode.Parse(reported.StdOut)!["warnings"]!.AsArray().Single(static item => item!["code"]!.GetValue<string>() == "PAGE_NUMBERS_REMOVED")!["hint"]!.GetValue<string>();
        string operation = Assert.Single(Regex.Matches(hint, SetPageNumbers)).Value;
        Assert.Equal(text, JsonNode.Parse(operation)!["text"]?.GetValue<string>());
        Assert.Equal(field is not null, hint.Contains($"the {field} field, which no set_page_numbers text writes", StringComparison.Ordinal));

        CliResult renumbered = _workspace.RunWithInput($$"""{"ops":[{{confidential}},{{operation}}]}""",
            "words", "edit", "footer.rtf", "--ops", "-", "--out", "renumbered.docx", "--verify", "--output-format", "json");

        Assert.True(renumbered.ExitCode == 0, renumbered.StdErr + renumbered.StdOut);
        Assert.True(JsonNode.Parse(renumbered.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        CliResult inspected = _workspace.Run("words", "inspect", "renumbered.docx", "--detail", "sections", "--output-format", "json");
        JsonNode numbers = JsonNode.Parse(inspected.StdOut)!["sections"]![0]!["pageNumbers"]!;
        Assert.Equal(("footer", "right", text), (numbers["location"]?.GetValue<string>(), numbers["alignment"]?.GetValue<string>(), numbers["text"]?.GetValue<string>()));
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

    // A set_page_numbers operation in a hint, whose strings may hold braces, such as {n}.
    private const string SetPageNumbers = """\{"op":"set_page_numbers"(?:"[^"]*"|[^}"])*\}""";
}
