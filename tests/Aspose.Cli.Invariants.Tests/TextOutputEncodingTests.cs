using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// The text a conversion writes is UTF-8 without a byte order mark, whichever engine writes it:
/// the write pipeline removes the mark an engine starts text, Markdown or HTML with. So a page
/// still says it is UTF-8, its non-English text reads back as UTF-8, and the size the result
/// reports is that of the published file. Every step of a scenario or a generated
/// case is also checked for a mark it did not ask for (<see cref="ScenarioContract.ByteOrderMark"/>).
/// </summary>
public sealed class TextOutputEncodingTests : IDisposable
{
    private const string Title = "季度报告 Quarterly";

    private readonly TempWorkspace _workspace = new();

    public TextOutputEncodingTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Conversions_WriteUtf8WithoutAByteOrderMark()
    {
        File.WriteAllText(_workspace.File("source.md"), $"# {Title}\n\n{Title}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.Equal(0, _workspace.Run("words", "create", "document.docx", "--from-markdown", "source.md").ExitCode);
        Assert.Equal(0, _workspace.Run("slides", "create", "deck.pptx").ExitCode);
        string slide = _workspace.Run("slides", "read", "deck.pptx", "--kind", "slide", "--output-format", "json").Json()["objects"]![0]!["at"]!.GetValue<string>();
        var box = new JsonObject
        {
            ["add"] = "autoShape",
            ["in"] = slide,
            ["args"] = new JsonObject { ["shapeType"] = "Rectangle", ["x"] = 36, ["y"] = 36, ["width"] = 600, ["height"] = 80 },
            ["props"] = new JsonObject { ["text"] = Title },
        };
        Assert.Equal(0, _workspace.Run("slides", "edit", "deck.pptx", "--in-place", "--ops", new JsonObject { ["ops"] = new JsonArray(box) }.ToJsonString()).ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "create", "book.xlsx").ExitCode);
        Assert.Equal(0, _workspace.Run("cells", "edit", "book.xlsx", "--in-place", "--set", $"Sheet1!A1={Title}").ExitCode);
        Assert.Equal(0, _workspace.Run("pdf", "create", "document.pdf", "--from-markdown", "source.md").ExitCode);

        (string Product, string Input, string Target)[] conversions =
        [
            ("words", "document.docx", "txt"), ("words", "document.docx", "md"), ("pdf", "document.pdf", "md"),
            ("slides", "deck.pptx", "html"), ("cells", "book.xlsx", "html"),
        ];
        foreach ((string product, string input, string target) in conversions)
        {
            string output = $"{product}.{target}";
            CliResult converted = _workspace.Run(product, "convert", input, "--to", target, "--out", output, "--output-format", "json");
            Assert.True(converted.ExitCode == 0, $"{product} {target}: {converted.StdErr}");
            byte[] bytes = File.ReadAllBytes(_workspace.File(output));

            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), $"{output} starts with a UTF-8 byte order mark.");
            JsonNode result = converted.Json();
            JsonNode published = (result["outputs"]?.AsArray() ?? [result["output"]]).Single(file =>
                string.Equals(file?["path"]?.GetValue<string>(), _workspace.File(output), StringComparison.OrdinalIgnoreCase))!;
            Assert.Equal(bytes.LongLength, published["sizeBytes"]!.GetValue<long>());
            if (target == "html")
            {
                string page = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
                Assert.Matches("(?i)<meta[^>]*charset\\s*=\\s*\"?utf-8", page);
                // The page's text, without its markup: Slides draws a text as runs of glyphs, one
                // element per font the engine falls back to, such as one for the CJK characters.
                string text = WebUtility.HtmlDecode(Regex.Replace(page, "<[^>]*>", string.Empty));
                Assert.Contains(Title, text, StringComparison.Ordinal);
            }
        }
    }
}
