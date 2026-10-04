using System.Text.Json.Nodes;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Category(TestCategory.Slow)]
    [Fact]
    public void CreateEditAndQuery_RoundTripsThroughTheBuiltCli()
    {
        CliResult capabilities = _workspace.Run(
            "capabilities", "words", "--output", "json");
        Assert.True(capabilities.ExitCode == 0, capabilities.StdErr);
        Assert.Equal(
            ["words", "words compare", "words convert", "words create", "words edit", "words extract", "words inspect", "words query", "words query blocks", "words query search", "words render", "words split"],
            JsonNode.Parse(capabilities.StdOut)!["products"]![0]!["commands"]!
                .AsArray()
                .Select(static command => command!["path"]!.GetValue<string>()));

        File.WriteAllText(
            _workspace.File("source.md"),
            "# Contract\n\nOriginal clause.\n");
        Assert.Equal(0, _workspace.Run(
            "words", "create", "contract.docx", "--markdown", "source.md").ExitCode);

        const string operations =
            """
            {
              "ops": [
                { "op": "set_text", "at": { "block": 2 }, "text": "Updated clause." },
                { "op": "insert_toc", "at": { "block": 2 }, "position": "after" },
                { "op": "set_header", "paragraphs": ["Document header"] }
              ]
            }
            """;
        CliResult edited = _workspace.RunWithInput(
            operations,
            "words", "edit", "contract.docx", "--ops", "-",
            "--in-place", "--verify", "--output", "json");
        CliResult read = _workspace.Run(
            "words", "query", "blocks", "contract.docx", "--blocks", "2", "--output", "json");

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
            "--max-items", "10", "--output", "json");

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
            "words", "convert", "contract.pdf", "--to", "docx", "--out", "contract.docx", "--output", "json");
        CliResult reviewed = _workspace.Run("review", "contract.pdf", "--out", "evidence", "--output", "json");

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
            "review", "findings.docx", "--out", "evidence", "--output", "json");

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
            "review", "findings.docx", "--out", "filtered", "--code", "WORDS_TEXT_TOO_SMALL", "--output", "json");

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
            $$"""{ "ops": [ { "op": "mail_merge", "inline": [{{records}}] } ] }""",
            "words", "edit", "letter.docx", "--ops", "-", "--out", "letters.docx",
            "--verify", "--license-mode", "evaluation", "--output", "json");

        JsonNode edited = JsonNode.Parse(result.StdOut)!;
        JsonNode warning = edited["warnings"]!.AsArray()
            .Single(static item => item!["code"]!.GetValue<string>() == "EVAL_INPUT_TRUNCATED")!;
        Assert.Contains("only its first 4 section(s)", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(edited["verification"]!["ok"]!.GetValue<bool>());
        Assert.Equal("OUTPUT_TRUNCATED", edited["verification"]!["issues"]![0]!["code"]!.GetValue<string>());
        Assert.NotEqual(0, result.ExitCode);
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
            "review", "secret.docx", "--password-env", "REVIEW_PASSWORD", "--output", "json");
        CliResult locked = _workspace.Run("review", "secret.docx", "--out", "locked.review", "--output", "json");

        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr + reviewed.StdOut);
        Assert.True(File.Exists(Path.Combine(_workspace.File("secret.docx.review"), "review.json")));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(locked.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Create_RejectsBlankWithTemplate()
    {
        File.WriteAllBytes(_workspace.File("template.docx"), []);

        CliResult result = _workspace.Run(
            "words", "create", "out.docx", "--blank", "--template", "template.docx", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        Assert.False(File.Exists(_workspace.File("out.docx")));
    }

    [Fact]
    public void QueryBlocks_NextKeepsANonContiguousRangeAndTheSectionFilter()
    {
        File.WriteAllText(_workspace.File("source.md"), "# Title\n\nOne.\n\nTwo.\n\nThree.\n\nFour.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "notes.docx", "--markdown", "source.md").ExitCode);

        CliResult spread = _workspace.Run(
            "words", "query", "blocks", "notes.docx", "--blocks", "1,3,5", "--max-blocks", "1", "--output", "json");
        CliResult section = _workspace.Run(
            "words", "query", "blocks", "notes.docx", "--section", "1", "--blocks", "2-3", "--max-blocks", "1", "--output", "json");

        Assert.True(spread.ExitCode == 0, spread.StdErr);
        Assert.EndsWith(
            " --blocks 3,5 --scope text --max-chars 20000 --max-blocks 1 --output json",
            JsonNode.Parse(spread.StdOut)!["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.True(section.ExitCode == 0, section.StdErr);
        JsonNode read = JsonNode.Parse(section.StdOut)!;
        Assert.Equal(2, Assert.Single(read["blocks"]!.AsArray())!["block"]!.GetValue<int>());
        Assert.EndsWith(
            " --blocks 3 --section 1 --scope text --max-chars 20000 --max-blocks 1 --output json",
            read["window"]!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void QuerySearch_WindowNextReturnsTheFollowingHits()
    {
        File.WriteAllText(_workspace.File("source.md"), "needle one\n\nneedle two\n\nneedle three\n");
        Assert.Equal(0, _workspace.Run("words", "create", "notes.docx", "--markdown", "source.md").ExitCode);

        CliResult first = _workspace.Run(
            "words", "query", "search", "notes.docx", "--pattern", "needle", "--max-hits", "2", "--output", "json");

        Assert.True(first.ExitCode == 0, first.StdErr);
        JsonNode page = JsonNode.Parse(first.StdOut)!;
        Assert.Equal(["needle one", "needle two"], Snippets(page));
        JsonNode window = page["window"]!;
        Assert.Equal("hit", window["unit"]!.GetValue<string>());
        Assert.Equal(2, window["returned"]!.GetValue<int>());
        Assert.True(window["truncated"]!.GetValue<bool>());
        string next = window["next"]!.GetValue<string>();
        Assert.EndsWith(" --pattern needle --scope body --max-hits 2 --skip 2 --output json", next, StringComparison.Ordinal);

        CliResult second = _workspace.RunCommandLine(next);

        Assert.True(second.ExitCode == 0, second.StdErr);
        JsonNode rest = JsonNode.Parse(second.StdOut)!;
        Assert.Equal(["needle three"], Snippets(rest));
        Assert.False(rest["window"]!["truncated"]!.GetValue<bool>());
        Assert.Null(rest["window"]!["next"]);
    }

    [Fact]
    public void Edit_RejectsABadSetDirectiveBeforeReadingOpsFromStdin()
    {
        File.WriteAllText(_workspace.File("source.md"), "# Contract\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--markdown", "source.md").ExitCode);

        CliResult edited = _workspace.RunWithInput(
            "not json",
            "words", "edit", "contract.docx", "--ops", "-", "--set", "Client=Contoso", "--output", "json");

        Assert.Equal(2, edited.ExitCode);
        JsonNode error = JsonNode.Parse(edited.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("--set", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Edit_RejectsTrackChangesWithoutAnAuthorBeforeReadingOpsFromStdin()
    {
        File.WriteAllText(_workspace.File("source.md"), "# Contract\n");
        Assert.Equal(0, _workspace.Run("words", "create", "contract.docx", "--markdown", "source.md").ExitCode);

        CliResult edited = _workspace.RunWithInput(
            "not json",
            "words", "edit", "contract.docx", "--ops", "-", "--track-changes", "--output", "json");

        Assert.Equal(2, edited.ExitCode);
        JsonNode error = JsonNode.Parse(edited.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("--author", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedZip_KeepsItsExactInputErrorThroughTheCli(bool supervised)
    {
        byte[] bytes = "PK\x03\x04invalid synthetic docx"u8.ToArray();
        File.WriteAllBytes(_workspace.File("malformed.docx"), bytes);
        string[] execution = supervised ? ["--timeout", "30"] : [];
        CliResult inspected = _workspace.Run(
            ["words", "inspect", "malformed.docx", "--output", "json", .. execution]);
        Assert.Equal(3, inspected.ExitCode);
        Assert.Equal(string.Empty, inspected.StdOut);
        Assert.Equal("FILE_CORRUPT", JsonNode.Parse(inspected.StdErr)!["error"]!["code"]!.GetValue<string>());

        byte[] retained = "Unrelated existing output"u8.ToArray();
        File.WriteAllBytes(_workspace.File("retained.pdf"), retained);
        CliResult converted = _workspace.Run(
            ["words", "convert", "malformed.docx", "--to", "pdf", "--out", "retained.pdf",
                "--overwrite", "--output", "json", .. execution]);
        Assert.Equal(3, converted.ExitCode);
        Assert.Equal(string.Empty, converted.StdOut);
        Assert.Equal("FILE_CORRUPT", JsonNode.Parse(converted.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(bytes, File.ReadAllBytes(_workspace.File("malformed.docx")));
        Assert.Equal(retained, File.ReadAllBytes(_workspace.File("retained.pdf")));
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
        CliResult compared = _workspace.Run(["words", "compare", "left.docx", "right.docx", "--output", "json", .. option]);

        Assert.True(compared.ExitCode == 0, compared.StdErr);
        Assert.Equal(
            expected,
            string.Join('|', JsonNode.Parse(compared.StdOut)!["samples"]!.AsArray()
                .Select(static sample => $"{sample!["type"]!.GetValue<string>()}:{sample["text"]?.GetValue<string>()}")));
    }

    public void Dispose() => _workspace.Dispose();

    private static IEnumerable<string> Snippets(JsonNode result) =>
        result["hits"]!.AsArray().Select(static hit => hit!["snippet"]!.GetValue<string>());
}
