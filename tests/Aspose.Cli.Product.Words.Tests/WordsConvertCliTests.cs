using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>What <c>words convert</c> says through the CLI.</summary>
public sealed class WordsConvertCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>--pages with a format that writes the whole document is refused for that reason.</summary>
    [Theory]
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

    /// <summary>A fixed-layout HTML output is a fixed-page format, so --pages writes only the pages it names.</summary>
    [Fact]
    public void PagesWithFixedLayoutHtml_WritesOnlyThosePages()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        string[] words = ["Alpha", "Bravo", "Charlie", "Delta"];
        for (int index = 0; index < words.Length; index++)
        {
            if (index > 0)
            {
                builder.InsertBreak(BreakType.PageBreak);
            }

            builder.Write(words[index]);
        }

        document.Save(_workspace.File("four.docx"));

        CliResult converted = _workspace.Run("words", "convert", "four.docx", "--to", "html-fixed", "--pages", "2-3", "--out", "pages.html", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal("2-3", JsonNode.Parse(converted.StdOut)!["pages"]!.GetValue<string>());
        string html = File.ReadAllText(_workspace.File("pages.html"));
        Assert.Equal(2, Regex.Matches(html, @"class=""awdiv awpage""").Count);
        Assert.Contains("Bravo", html, StringComparison.Ordinal);
        Assert.Contains("Charlie", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Delta", html, StringComparison.Ordinal);
    }
}
