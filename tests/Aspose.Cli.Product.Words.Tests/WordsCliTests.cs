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
            JsonNode.Parse(spread.StdOut)!["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
        Assert.True(section.ExitCode == 0, section.StdErr);
        JsonNode read = JsonNode.Parse(section.StdOut)!;
        Assert.Equal(2, Assert.Single(read["blocks"]!.AsArray())!["i"]!.GetValue<int>());
        Assert.EndsWith(
            " --blocks 3 --section 1 --scope text --max-chars 20000 --max-blocks 1 --output json",
            read["next"]!.GetValue<string>(),
            StringComparison.Ordinal);
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

    [Theory]
    [InlineData("convert", "--encrypt")]
    [InlineData("convert", "--encrypt-env")]
    [InlineData("create", "--encrypt-env")]
    [InlineData("edit", "--encrypt-env")]
    public void EncryptingAnUnprotectableOutputNamesTheOptionThatWasPassed(string verb, string option)
    {
        new DocumentBuilder().Document.Save(_workspace.File("source.docx"));
        string secret = option == "--encrypt" ? "document-secret" : "DOCUMENT_PASSWORD";
        string[] arguments = verb switch
        {
            "convert" => ["words", "convert", "source.docx", "--to", "pdf", option, secret],
            "create" => ["words", "create", "result.pdf", "--blank", option, secret],
            _ => ["words", "edit", "source.docx", "--set", "bookmark:Name=Value", "--out", "result.pdf", option, secret],
        };

        CliResult result = _workspace.RunWithEnv(
            new Dictionary<string, string?> { ["DOCUMENT_PASSWORD"] = "document-secret" },
            [.. arguments, "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
    }

    public void Dispose() => _workspace.Dispose();
}
