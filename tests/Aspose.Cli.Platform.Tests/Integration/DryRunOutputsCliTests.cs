using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// A dry run checks every file the run would write, not only the output it names: the numbered
/// parts a selection names, and, when how many parts there are depends on the document, the
/// existing files the run may write. It refuses an existing one as the run does, naming the
/// option that chooses another path.
/// </summary>
public sealed class DryRunOutputsCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public DryRunOutputsCliTests()
    {
        // A dry run reads no document, so any bytes do.
        File.WriteAllText(_workspace.File("a.pdf"), "not a document");
        File.WriteAllText(_workspace.File("a.docx"), "not a document");
    }

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("pdf render a.pdf --out r.png --pages 1-3", "r.2.png", "--out", false)]
    [InlineData("pdf render a.pdf --out r.png --pages 2", "r.png", "--out", false)]
    [InlineData("pdf render a.pdf --out r.png", "r.1.png", "--out", true)]
    [InlineData("pdf render a.pdf --out r.png", "r.png", "--out", true)]
    [InlineData("pdf split a.pdf --by pages --groups 1-2 --groups 3-4 --out-dir parts", "parts/a.2.pdf", "--out-dir", false)]
    [InlineData("pdf split a.pdf --by pages --pages 1-4 --every 2 --out-dir parts", "parts/a.2.pdf", "--out-dir", false)]
    [InlineData("pdf split a.pdf --by pages --every 2 --out-dir parts", "parts/a.1.pdf", "--out-dir", true)]
    [InlineData("pdf convert a.pdf --to docx --out c.docx", "c.docx", "--out", false)]
    [InlineData("words split a.docx --by pages --pages 1-2 --out-dir parts", "parts/a.2.docx", "--out-dir", false)]
    public void DryRun_RefusesAFileTheRunWouldReplace(string commandLine, string existing, string option, bool dependsOnDocument)
    {
        string path = _workspace.File(existing);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "kept");

        CliResult result = Run(commandLine);

        Assert.True(result.ExitCode == 5, result.StdErr + result.StdOut);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OUTPUT_EXISTS", error["code"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(path), error["details"]!["path"]!.GetValue<string>());
        Assert.EndsWith($"choose a different path with {option}.", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(dependsOnDocument, error["message"]!.GetValue<string>().Contains("depend", StringComparison.Ordinal));
        Assert.Equal("kept", File.ReadAllText(path));

        Assert.Equal(0, Run(commandLine + " --overwrite").ExitCode);
    }

    [Theory]
    [InlineData("pdf render a.pdf --out r.png --pages 1-3", "r.png", new[] { "r.1.png", "r.2.png", "r.3.png" })]
    [InlineData("pdf render a.pdf --out r.png --pages 9-10", "r.1.png", new[] { "r.09.png", "r.10.png" })]
    [InlineData("pdf split a.pdf --by pages --groups 1-2 --groups 3-4 --out-dir parts", "parts/a.3.pdf", new[] { "parts/a.1.pdf", "parts/a.2.pdf" })]
    [InlineData("pdf split a.pdf --by pages --pages 2,5 --out-dir parts", "parts/a.1.pdf", new[] { "parts/a.2.pdf", "parts/a.5.pdf" })]
    [InlineData("words split a.docx --by pages --pages 2,5 --out-dir parts", "parts/a.1.docx", new[] { "parts/a.2.docx", "parts/a.5.docx" })]
    public void DryRun_ListsTheNumberedPartsASelectionNames(string commandLine, string unrelated, string[] parts)
    {
        string path = _workspace.File(unrelated);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "kept");

        CliResult result = Run(commandLine);

        Assert.True(result.ExitCode == 0, result.StdErr);
        JsonNode dryRun = JsonNode.Parse(result.StdOut)!;
        Assert.Equal(["input", "output"], dryRun["checked"]!.AsArray().Select(static check => check!.GetValue<string>()));
        Assert.Equal(
            parts.Select(part => Path.GetFullPath(_workspace.File(part))),
            dryRun["paths"]!.AsArray().Select(static part => part!.GetValue<string>()));
    }

    private CliResult Run(string commandLine) =>
        _workspace.Run([.. commandLine.Split(' '), "--dry-run", "--output-format", "json"]);
}
