using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>What <c>words convert</c> says through the CLI.</summary>
public sealed class WordsConvertCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// --pages with a format that writes the whole document is refused for that reason: a
    /// fixed-layout HTML output is no flow format.
    /// </summary>
    [Theory]
    [InlineData("html-fixed", "page.html")]
    [InlineData("docx", "page.docx")]
    public void PagesWithAWholeDocumentFormat_SaysTheOutputHoldsTheWholeDocument(string format, string output)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nText.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "doc.docx", "--from-markdown", "source.md").ExitCode);

        CliResult converted = _workspace.Run("words", "convert", "doc.docx", "--to", format, "--pages", "1", "--out", output, "--output-format", "json");

        JsonNode error = JsonNode.Parse(converted.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        string message = error["message"]!.GetValue<string>();
        Assert.DoesNotContain("flow format", message, StringComparison.Ordinal);
        Assert.Contains($"a '{format}' output holds the whole document", message, StringComparison.Ordinal);
    }
}
