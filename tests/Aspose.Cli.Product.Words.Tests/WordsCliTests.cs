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
                { "op": "set_text", "at": { "block": 2 }, "text": "Updated clause." }
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

    public void Dispose() => _workspace.Dispose();
}
