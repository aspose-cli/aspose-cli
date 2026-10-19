using System.Text.Json.Nodes;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsCliTests : IClassFixture<WordsFixture>, IDisposable
{
    private readonly TempWorkspace _workspace = new();

    /// <summary>
    /// The fixture applies the Words license in a licensed run before a test builds its input with
    /// the SDK, so an input is whole whichever test of the process runs first.
    /// </summary>
    public WordsCliTests(WordsFixture fixture) => ArgumentNullException.ThrowIfNull(fixture);

    [Fact]
    public void Merge_IsRefusedWithEditAsTheSuggestion()
    {
        CliResult refused = _workspace.Run("words", "merge", "a.docx", "b.docx", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal("words edit", error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.Contains("import_sections", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void CreateEditAndQuery_RoundTripsThroughTheBuiltCli()
    {
        File.WriteAllText(
            _workspace.File("source.md"),
            "# Contract\n\nOriginal clause.\n");
        Assert.Equal(0, _workspace.Run(
            "words", "create", "contract.docx", "--from-markdown", "source.md").ExitCode);

        const string operations =
            """
            {
              "ops": [
                { "op": "set_text", "target": { "block": 2 }, "text": "Updated clause." },
                { "op": "insert_toc", "at": { "block": 2 }, "position": "after" },
                { "op": "set_header", "paragraphs": [{ "text": "Document header" }] }
              ]
            }
            """;
        CliResult edited = _workspace.RunWithInput(
            operations,
            "words", "edit", "contract.docx", "--ops", "-",
            "--in-place", "--verify", "--output-format", "json");
        CliResult read = _workspace.Run(
            "words", "read", "contract.docx", "--blocks", "2", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.True(JsonNode.Parse(edited.StdOut)!["verification"]!["ok"]!.GetValue<bool>());
        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.Equal(
            "Updated clause.",
            JsonNode.Parse(read.StdOut)!["blocks"]![0]!["text"]!.GetValue<string>());
        var reopened = new Document(_workspace.File("contract.docx"));
        Assert.Contains(reopened.Range.Fields.Cast<Aspose.Words.Fields.Field>(),
            field => field.Type == Aspose.Words.Fields.FieldType.FieldTOC
                && field.GetFieldCode().Contains("1-3", StringComparison.Ordinal));
        Assert.Contains("Document header",
            reopened.FirstSection.HeadersFooters[HeaderFooterType.HeaderPrimary].GetText(),
            StringComparison.Ordinal);

    }

    [Fact]
    public void Review_ListStyleDocument_ProducesCompleteEvidence()
    {
        string input = _workspace.File("list-style.docx");
        var document = new Document();
        Style listStyle = document.Styles.Add(StyleType.List, "Review checklist");
        Aspose.Words.Lists.List list = document.Lists.Add(listStyle);
        var builder = new DocumentBuilder(document);
        builder.ListFormat.List = list;
        builder.Writeln("Confirm the commercial terms.");
        builder.ListFormat.RemoveNumbers();
        document.Save(input);

        CliResult reviewed = _workspace.Run(
            "review", "list-style.docx", "--out", "evidence",
            "--max-items", "10", "--output-format", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        Assert.DoesNotContain("HOST-COMMAND-0001", reviewed.StdErr, StringComparison.Ordinal);
        JsonNode result = JsonNode.Parse(reviewed.StdOut)!;
        Assert.Equal("words", result["product"]!.GetValue<string>());
        Assert.True(result["coverage"]!["complete"]!.GetValue<bool>());
        Assert.Equal(
            result["coverage"]!["expectedItemCount"]!.GetValue<int>(),
            result["coverage"]!["renderedItemCount"]!.GetValue<int>());
        Assert.True(File.Exists(_workspace.File("evidence/index.html")));
        Assert.True(File.Exists(_workspace.File("evidence/review.json")));
    }

    [Fact]
    public void WordsCommands_ReadADocumentByItsContentWhateverItsExtension()
    {
        var document = new Document();
        new DocumentBuilder(document).Write("Supplier contract.");
        document.Save(_workspace.File("contract.pdf"), SaveFormat.Docx);

        CliResult converted = _workspace.Run(
            "words", "convert", "contract.pdf", "--to", "docx", "--out", "contract.docx", "--output-format", "json");
        CliResult reviewed = _workspace.Run("review", "contract.pdf", "--out", "evidence", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal("docx", JsonNode.Parse(converted.StdOut)!["input"]!["format"]!.GetValue<string>());
        Assert.Equal("FORMAT_MISMATCH", JsonNode.Parse(reviewed.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Review_ReportsDeclaredChecksAndFiltersThemByCode()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Font.Name = "Times New Roman";
        builder.Font.Size = 5;
        builder.Writeln("Fine print.");
        builder.Font.Size = 80;
        builder.Writeln("Banner");
        document.StartTrackRevisions("Reviewer", DateTime.UnixEpoch);
        builder.Font.Size = 12;
        builder.Writeln("Tracked clause.");
        document.StopTrackRevisions();
        var comment = new Comment(document, "Reviewer", "R", DateTime.UnixEpoch);
        comment.SetText("Confirm the clause.");
        builder.CurrentParagraph.AppendChild(comment);
        builder.InsertBreak(BreakType.PageBreak);
        builder.Write("Second page.");
        document.Save(_workspace.File("findings.docx"));

        CliResult reviewed = _workspace.Run(
            "review", "findings.docx", "--out", "evidence", "--output-format", "json");

        Assert.True(reviewed.ExitCode is 0 or 8, reviewed.StdErr);
        JsonArray findings = JsonNode.Parse(reviewed.StdOut)!["findings"]!.AsArray();
        string[] codes = findings
            .Select(static finding => finding!["code"]!.GetValue<string>())
            .ToArray();
        Assert.Contains("WORDS_TEXT_TOO_SMALL", codes);
        Assert.Contains("WORDS_TEXT_TOO_LARGE", codes);
        Assert.Contains("WORDS_REVISIONS_PRESENT", codes);
        Assert.Contains("WORDS_COMMENTS_PRESENT", codes);
        // A page finding's evidence is its page's image; a document finding's is every page.
        string[] Evidence(string code) => findings
            .Single(finding => finding!["code"]!.GetValue<string>() == code)!["evidence"]!.AsArray()
            .Select(static path => Path.GetFileName(path!.GetValue<string>()))
            .ToArray();
        Assert.Equal(["page-0001.png"], Evidence("WORDS_TEXT_TOO_SMALL"));
        Assert.Equal(["page-0001.png", "page-0002.png"], Evidence("WORDS_REVISIONS_PRESENT"));
        Assert.Equal(
            "Disclose the revisions; accept or reject them with accept_revisions or reject_revisions only when the user decides.",
            findings.Single(static finding => finding!["code"]!.GetValue<string>() == "WORDS_REVISIONS_PRESENT")!["hint"]!.GetValue<string>());

        CliResult filtered = _workspace.Run(
            "review", "findings.docx", "--out", "filtered", "--code", "WORDS_TEXT_TOO_SMALL", "--output-format", "json");

        Assert.True(filtered.ExitCode is 0 or 8, filtered.StdErr);
        JsonNode result = JsonNode.Parse(filtered.StdOut)!;
        Assert.All(result["findings"]!.AsArray(), static finding =>
            Assert.Equal("WORDS_TEXT_TOO_SMALL", finding!["code"]!.GetValue<string>()));
        Assert.Equal(codes.Count(static code => code != "WORDS_TEXT_TOO_SMALL"),
            result["filter"]!["omittedFindingCount"]!.GetValue<int>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Edit_DisclosesAnOutputEvaluationModeCutShortWhenSavingIt()
    {
        // Evaluation mode keeps about 200 paragraphs of a document it lays out or saves, so of
        // five copies of a 60-paragraph letter only the first four, the fourth cut short, remain.
        var template = new Document();
        var builder = new DocumentBuilder(template);
        builder.InsertField("MERGEFIELD Name");
        builder.Writeln();
        for (int index = 1; index < 60; index++)
        {
            builder.Writeln($"Clause {index}.");
        }
        template.Save(_workspace.File("letter.docx"));
        string records = string.Join(",", Enumerable.Range(1, 5).Select(static index => $$"""{ "Name": "Person {{index}}" }"""));

        CliResult result = _workspace.RunWithInput(
            $$"""{ "ops": [ { "op": "mail_merge", "items": [{{records}}] } ] }""",
            "words", "edit", "letter.docx", "--ops", "-", "--out", "letters.docx",
            "--verify", "--license-mode", "evaluation", "--output-format", "json");

        // The input was whole, so the write pipeline's disclosure names the notice the output
        // ends with; no product check says it a second time. Verification reports the output
        // incomplete, so the edit exits 8 with the output still written.
        JsonNode edited = JsonNode.Parse(result.StdOut)!;
        JsonNode[] warnings = [.. edited["warnings"]!.AsArray().Select(static item => item!)];
        JsonNode disclosure = warnings.Single(static item => item["code"]!.GetValue<string>() == "EVALUATION_MODE");
        Assert.Contains("evaluation mode cut the content short", disclosure["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(warnings, static item => item["code"]!.GetValue<string>() == "EVALUATION_INPUT_TRUNCATED");
        JsonNode verification = edited["verification"]!;
        Assert.False(verification["ok"]!.GetValue<bool>());
        Assert.Equal(["OUTPUT_TRUNCATED"], verification["issues"]!.AsArray().Select(static issue => issue!["code"]!.GetValue<string>()));
        Assert.True(result.ExitCode == 8, result.StdErr + result.StdOut);
        Assert.True(File.Exists(_workspace.File("letters.docx")));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void Review_OpensAPasswordEncryptedDocument()
    {
        var document = new Document();
        new DocumentBuilder(document).Writeln("Confidential clause.");
        document.Save(_workspace.File("secret.docx"), new Aspose.Words.Saving.OoxmlSaveOptions(SaveFormat.Docx) { Password = "review-secret" });

        CliResult reviewed = _workspace.RunWithEnv(
            new Dictionary<string, string?> { ["REVIEW_PASSWORD"] = "review-secret" },
            "review", "secret.docx", "--password-env", "REVIEW_PASSWORD", "--output-format", "json");
        CliResult locked = _workspace.Run("review", "secret.docx", "--out", "locked.review", "--output-format", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr + reviewed.StdOut);
        Assert.True(File.Exists(Path.Combine(_workspace.File("secret.docx.review"), "review.json")));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// An encrypted template is refused with the convert that writes an unencrypted Flat OPC
    /// copy of it, and that copy, run as the hint says, serves as the template with its styles.
    /// </summary>
    [Fact]
    public void Create_FromAnEncryptedTemplate_PointsToAnUnencryptedCopyThatServesAsTheTemplate()
    {
        var template = new Document();
        Style brand = template.Styles.Add(StyleType.Paragraph, "Brand Heading");
        brand.Font.Size = 31;
        new DocumentBuilder(template).Writeln("Letterhead.");
        template.Save(_workspace.File("locked.docx"), new Aspose.Words.Saving.OoxmlSaveOptions(SaveFormat.Docx) { Password = "template-secret" });
        File.WriteAllText(_workspace.File("body.txt"), "Dear customer,\n");

        CliResult refused = _workspace.Run(
            "words", "create", "letter.docx", "--template", "locked.docx", "--from-text", "body.txt", "--output-format", "json");

        Assert.Equal(3, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("PASSWORD_REQUIRED", error["code"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        Assert.Contains("encrypted template", hint, StringComparison.Ordinal);
        Assert.Contains("ask the user for an unencrypted copy", hint, StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("letter.docx")));

        // Run the convert the hint names, with the password in the variable it leaves to fill.
        string command = System.Text.RegularExpressions.Regex.Match(hint, "'aspose-cli (words convert [^']+)'").Groups[1].Value;
        string[] convert = [.. command.Replace("<password-env>", "TEMPLATE_PASSWORD", StringComparison.Ordinal).Split(' ')];
        CliResult copied = _workspace.RunWithEnv(
            new Dictionary<string, string?> { ["TEMPLATE_PASSWORD"] = "template-secret" }, [.. convert, "--output-format", "json"]);
        Assert.True(copied.ExitCode == 0, copied.StdErr);
        string copy = convert[Array.IndexOf(convert, "--out") + 1];

        CliResult created = _workspace.Run(
            "words", "create", "letter.docx", "--template", copy, "--from-text", "body.txt", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        Style kept = new Document(_workspace.File("letter.docx")).Styles["Brand Heading"]!;
        Assert.Equal(31, kept.Font.Size);
    }

    [Fact]
    public void Read_NextKeepsANonContiguousRangeAndTheSectionFilter()
    {
        File.WriteAllText(_workspace.File("source.md"), "# Title\n\nOne.\n\nTwo.\n\nThree.\n\nFour.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "notes.docx", "--from-markdown", "source.md").ExitCode);

        // Both queries only read the document, so they run at once.
        CliResult spread = null!, section = null!;
        Parallel.Invoke(
            () => spread = _workspace.Run(
                "words", "read", "notes.docx", "--blocks", "1,3,5", "--max-blocks", "1", "--output-format", "json"),
            () => section = _workspace.Run(
                "words", "read", "notes.docx", "--section", "1", "--blocks", "2-3", "--max-blocks", "1", "--output-format", "json"));

        Assert.True(spread.ExitCode == 0, spread.StdErr);
        Assert.EndsWith(
            " --blocks 3,5 --content text --max-chars 20000 --max-blocks 1 --output-format json",
            JsonNode.Parse(spread.StdOut)!["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.True(section.ExitCode == 0, section.StdErr);
        JsonNode read = JsonNode.Parse(section.StdOut)!;
        Assert.Equal(2, Assert.Single(read["blocks"]!.AsArray())!["block"]!.GetValue<int>());
        Assert.EndsWith(
            " --blocks 3 --section 1 --content text --max-chars 20000 --max-blocks 1 --output-format json",
            read["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Search_WindowNextReturnsTheFollowingHits()
    {
        File.WriteAllText(_workspace.File("source.md"), "needle one\n\nneedle two\n\nneedle three\n");
        Assert.Equal(0, _workspace.Run("words", "create", "notes.docx", "--from-markdown", "source.md").ExitCode);

        CliResult first = _workspace.Run(
            "words", "search", "notes.docx", "--pattern", "needle", "--max-hits", "2", "--output-format", "json");

        Assert.True(first.ExitCode == 0, first.StdErr);
        JsonNode page = JsonNode.Parse(first.StdOut)!;
        Assert.Equal(["needle one", "needle two"], HitTexts(page));
        JsonNode window = page["window"]!;
        Assert.Equal("hit", window["unit"]!.GetValue<string>());
        Assert.Equal(2, window["returned"]!.GetValue<int>());
        Assert.True(window["truncated"]!.GetValue<bool>());
        string next = window["next"]!.GetValue<string>();
        Assert.EndsWith(" --pattern needle --scope body --max-hits 2 --skip 2 --output-format json", next, StringComparison.Ordinal);

        CliResult second = _workspace.RunCommandLine(next);

        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode rest = JsonNode.Parse(second.StdOut)!;
        Assert.Equal(["needle three"], HitTexts(rest));
        Assert.False(rest["window"]!["truncated"]!.GetValue<bool>());
        Assert.Null(rest["window"]!["next"]);
    }

    [Theory]
    [InlineData(null, "deletion:合同期限为三年|insertion:合同期限为五年")]
    [InlineData("char", "deletion:三|insertion:五")]
    public void Compare_GranularityCharMarksASingleChangedChineseCharacter(string? granularity, string expected)
    {
        foreach ((string name, string text) in new[] { ("left.docx", "合同期限为三年。"), ("right.docx", "合同期限为五年。") })
        {
            var document = new Document();
            new DocumentBuilder(document).Write(text);
            document.Save(_workspace.File(name));
        }

        string[] option = granularity is null ? [] : ["--granularity", granularity];
        CliResult compared = _workspace.Run(["words", "compare", "left.docx", "right.docx", "--output-format", "json", .. option]);

        Assert.True(compared.ExitCode == 0, compared.StdErr);
        Assert.Equal(
            expected,
            string.Join('|', JsonNode.Parse(compared.StdOut)!["samples"]!.AsArray()
                .Select(static sample => $"{sample!["kind"]!.GetValue<string>()}:{sample["text"]?.GetValue<string>()}")));
    }

    /// <summary>
    /// A replace_text that matches nothing says so with a hint the caller can follow as written,
    /// which the run's hint check verifies: the wider scope is the op's own field.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ReplaceText_WithNoMatch_HintsAtTheOpsScope()
    {
        File.WriteAllText(_workspace.File("source.md"), "Party A pays.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--from-markdown", "source.md").ExitCode);

        CliResult edited = _workspace.RunWithInput(
            """{ "ops": [ { "op": "replace_text", "find": "Party Z", "replace": "Party Y" } ] }""",
            "words", "edit", "contract.docx", "--ops", "-", "--out", "edited.docx", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        JsonNode warning = JsonNode.Parse(edited.StdOut)!["warnings"]!.AsArray()
            .Single(static item => item!["code"]!.GetValue<string>() == "OP_NO_EFFECT")!;
        Assert.Contains("\"scope\": \"all\"", warning["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A replace_text regex reads ^, $, \A, \z and the x option at each paragraph, as words
    /// search does (WORDS-REGEX-ANCHORS), so it replaces every match search finds.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(@"^(\d)\.(\d)", "x Scope|x Terms|x Notices|See 1.4 later")]
    [InlineData(@"(?m)^1\.\d", "x Scope|x Terms|x Notices|See 1.4 later")]
    [InlineData(@"(Scope|Terms|later)$", "1.1 x|1.2 x|1.3 Notices|See 1.4 x")]
    [InlineData(@"\A1\.\d", "x Scope|x Terms|x Notices|See 1.4 later")]
    [InlineData(@"(Notices|later)\z", "1.1 Scope|1.2 Terms|1.3 x|See 1.4 x")]
    [InlineData(@"(?x) ^ 1 \. \d  # a clause number", "x Scope|x Terms|x Notices|See 1.4 later")]
    public void ReplaceText_AnchorsAtEachParagraphAsSearchDoes(string find, string expected)
    {
        File.WriteAllText(_workspace.File("source.md"), "1.1 Scope\n\n1.2 Terms\n\n1.3 Notices\n\nSee 1.4 later\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--from-markdown", "source.md").ExitCode);

        CliResult searched = _workspace.Run("words", "search", "contract.docx", "--pattern", find, "--regex", "--output-format", "json");
        CliResult edited = EditWith(new JsonObject { ["op"] = "replace_text", ["find"] = find, ["replace"] = "x", ["regex"] = true });

        Assert.True(searched.ExitCode == 0, searched.StdErr);
        Assert.True(edited.ExitCode == 0, edited.StdErr);
        int hits = JsonNode.Parse(searched.StdOut)!["hits"]!.AsArray().Count;
        Assert.Equal(hits, JsonNode.Parse(edited.StdOut)!["applied"]![0]!["itemsAffected"]!.GetValue<int>());
        CliResult read = _workspace.Run("words", "read", "edited.docx", "--output-format", "json");
        Assert.Equal(
            expected,
            string.Join('|', JsonNode.Parse(read.StdOut)!["blocks"]!.AsArray().Select(static block => block!["text"]!.GetValue<string>())
                .Where(static text => text.Length > 0 && !text.Contains("Aspose", StringComparison.Ordinal))));
    }

    /// <summary>
    /// An escaped ^ or $, a negated class and a $ inside a class are no anchors, and a literal
    /// find is never read as a pattern, so each replaces what it matches.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(@"\^2", true, "Cost x and $5 [a]")]
    [InlineData(@"\$5", true, "Cost ^2 and x [a]")]
    [InlineData(@"[$]5", true, "Cost ^2 and x [a]")]
    [InlineData(@"\[[^b]\]", true, "Cost ^2 and $5 x")]
    [InlineData("^2", false, "Cost x and $5 [a]")]
    [InlineData("(?i)COST", true, "x ^2 and $5 [a]")]
    [InlineData("(?i:COST)", true, "x ^2 and $5 [a]")]
    public void ReplaceText_AcceptsCharactersThatAreNoAnchor(string find, bool regex, string expected)
    {
        File.WriteAllText(_workspace.File("source.txt"), "Cost ^2 and $5 [a]\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--from-text", "source.txt").ExitCode);

        CliResult edited = EditWith(new JsonObject { ["op"] = "replace_text", ["find"] = find, ["replace"] = "x", ["regex"] = regex });

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        CliResult read = _workspace.Run("words", "read", "edited.docx", "--output-format", "json");
        Assert.Contains(
            JsonNode.Parse(read.StdOut)!["blocks"]!.AsArray().Select(static block => block!["text"]!.GetValue<string>()),
            text => text == expected);
    }

    private CliResult EditWith(JsonObject op)
    {
        File.WriteAllText(_workspace.File("ops.json"), new JsonObject { ["ops"] = new JsonArray(op) }.ToJsonString());
        return _workspace.Run("words", "edit", "contract.docx", "--ops", "ops.json", "--out", "edited.docx", "--output-format", "json");
    }

    /// <summary>
    /// Each hit carries its 1-based occurrence among the matches of the document, as a PDF search
    /// hit does, so two hits in one paragraph are told apart; a later window keeps the numbers.
    /// </summary>
    [Fact]
    public void Search_NumbersEachMatchOfTheDocument()
    {
        File.WriteAllText(_workspace.File("source.md"), "Party A pays Party B, and Party B signs.\n\nParty B keeps a copy.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--from-markdown", "source.md").ExitCode);

        CliResult all = _workspace.Run("words", "search", "contract.docx", "--pattern", "Party B", "--output-format", "json");
        CliResult later = _workspace.Run("words", "search", "contract.docx", "--pattern", "Party B", "--skip", "1", "--output-format", "json");

        Assert.True(all.ExitCode == 0, all.StdErr);
        Assert.Equal(["1:1", "1:2", "2:3"], Occurrences(JsonNode.Parse(all.StdOut)!));
        Assert.True(later.ExitCode == 0, later.StdErr);
        Assert.Equal(["1:2", "2:3"], Occurrences(JsonNode.Parse(later.StdOut)!));

        static IEnumerable<string> Occurrences(JsonNode result) => result["hits"]!.AsArray()
            .Select(static hit => $"{hit!["block"]!.GetValue<int>()}:{hit["occurrence"]?.GetValue<int>()}");
    }

    /// <summary>
    /// Merge data that names one column or property twice cannot say which value a field takes,
    /// so the merge refuses it as invalid data.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("data.csv", "Name,Name\nAva,Noah\n")]
    [InlineData("data.json", """[{"Name":"Ava","Name":"Noah"}]""")]
    public void MailMerge_RefusesDataThatNamesAFieldTwice(string dataName, string data)
    {
        var template = new Document();
        new DocumentBuilder(template).InsertField("MERGEFIELD Name");
        template.Save(_workspace.File("letter.docx"));
        File.WriteAllText(_workspace.File(dataName), data);

        CliResult result = _workspace.RunWithInput(
            $$"""{ "ops": [ { "op": "mail_merge", "path": "{{dataName}}" } ] }""",
            "words", "edit", "letter.docx", "--ops", "-", "--out", "letters.docx", "--output-format", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("MERGE_DATA_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("'Name'", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("letters.docx")));
    }

    /// <summary>
    /// A replace_text expression that backtracks catastrophically stops at its execution budget
    /// and writes nothing, although the engine's replace ignores the budget. The budget judges the
    /// text the replace searches: text a tracked change deletes is not there, so the forty letters
    /// around a deleted one are one run of letters.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceText_StopsARegexThatRunsPastItsBudget(bool deletedLetterBetween)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Write(new string('a', 20));
        builder.Write(deletedLetterBetween ? "x" : string.Empty);
        builder.Write(new string('a', 20) + "!");
        if (deletedLetterBetween)
        {
            document.StartTrackRevisions("Ann", new DateTime(2026, 9, 1));
            document.Range.Replace("x", string.Empty);
            document.StopTrackRevisions();
        }

        document.Save(_workspace.File("runaway.docx"));

        CliResult result = ReplaceRunaway();

        Assert.Equal(9, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPERATION_TIMEOUT", error["code"]!.GetValue<string>());
        Assert.Equal("regex", error["details"]!["phase"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("replaced.docx")));
    }

    /// <summary>
    /// A field's code is not text the replace searches, so letters that would run past the
    /// budget in a hyperlink's address do not stop an edit whose text they are not.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ReplaceText_DoesNotJudgeTheBudgetOnAFieldCode()
    {
        // The address alone runs the expression past its budget; the shown text "ab" matches it.
        const string Find = "(a+)+b";
        string address = "https://example.com/" + new string('a', 40) + "!";
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(
            () => Aspose.Cli.Sdk.Text.SafeRegex.Create(Find, caseSensitive: true).IsMatch(address));
        var document = new Document();
        new DocumentBuilder(document).InsertHyperlink("ab", address, false);
        document.Save(_workspace.File("runaway.docx"));

        CliResult result = ReplaceRunaway(Find);

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.True(File.Exists(_workspace.File("replaced.docx")));
    }

    // Runs the expression over runaway.docx; it must end well within the regex budget's reach.
    private CliResult ReplaceRunaway(string find = "(a+)+b")
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        CliResult result = _workspace.RunWithInput(
            $$"""{ "ops": [ { "op": "replace_text", "find": "{{find}}", "replace": "b", "regex": true } ] }""",
            "words", "edit", "runaway.docx", "--ops", "-", "--out", "replaced.docx", "--output-format", "json");
        clock.Stop();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"took {clock.Elapsed}");
        return result;
    }

    public void Dispose() => _workspace.Dispose();

    private static IEnumerable<string> HitTexts(JsonNode result) =>
        result["hits"]!.AsArray().Select(static hit => hit!["context"]!.GetValue<string>());
}
