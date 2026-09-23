using System.Text.Json.Nodes;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

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
            result["coverage"]!["expectedItems"]!.GetValue<int>(),
            result["coverage"]!["renderedItems"]!.GetValue<int>());
        Assert.True(File.Exists(_workspace.File("evidence/index.html")));
        Assert.True(File.Exists(_workspace.File("evidence/review.json")));
    }

    [Fact]
    public void CreateFromMarkdown_TakesStylesPageSetupAndFooterFromTheBundledTemplate()
    {
        CliResult installed = _workspace.Run(
            "skill", "install", "aspose-cli-words", "--target", "skills", "--output", "json");
        Assert.True(installed.ExitCode == 0, installed.StdErr);
        string template = _workspace.File(
            Path.Combine("skills", "aspose-cli-words", "assets", "templates", "default-a4.docx"));
        File.WriteAllText(_workspace.File("brief.md"), "# Brief\n\nPlain **bold** and `code`.\n");

        CliResult created = _workspace.Run(
            "words", "create", "brief.docx", "--markdown", "brief.md",
            "--template", template, "--output", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        var document = new Document(_workspace.File("brief.docx"));
        Section section = Assert.Single(document.Sections.Cast<Section>());
        Assert.Equal(PaperSize.A4, section.PageSetup.PaperSize);
        Assert.Contains(
            section.HeadersFooters[HeaderFooterType.FooterPrimary].Range.Fields.Cast<Aspose.Words.Fields.Field>(),
            static field => field.Type == Aspose.Words.Fields.FieldType.FieldPage);
        // Evaluation mode may add a banner paragraph; select the authored paragraphs by text.
        Paragraph[] paragraphs = section.Body.Paragraphs.Cast<Paragraph>().ToArray();
        Paragraph heading = Assert.Single(paragraphs, static paragraph => paragraph.GetText().Trim() == "Brief");
        Assert.Equal(StyleIdentifier.Heading1, heading.ParagraphFormat.StyleIdentifier);
        Assert.True(heading.Runs[0].Font.Bold);
        Run[] runs = Assert.Single(paragraphs, static paragraph => paragraph.GetText().StartsWith("Plain", StringComparison.Ordinal))
            .Runs.Cast<Run>().ToArray();
        Assert.Equal(["bold"], runs.Where(static run => run.Font.Bold).Select(static run => run.Text));
        Assert.Equal("InlineCode", Assert.Single(runs, static run => run.Text == "code").Font.StyleName);
        Assert.DoesNotContain(paragraphs, static paragraph => !paragraph.HasChildNodes);
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

    public void Dispose() => _workspace.Dispose();
}
