using System.Text.Json.Nodes;
using Aspose.Words;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// <c>words split</c> and <c>words extract</c> through the built CLI, so their real JSON results
/// are validated against the generated split-result and extract-result schemas.
/// </summary>
public sealed class WordsSplitExtractCliTests : IDisposable
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void Split_BySection_WritesOnePartPerSection()
    {
        WriteDocument();

        CliResult split = _workspace.Run("words", "split", "report.docx", "--by", "sections", "--out-dir", "parts", "--output-format", "json");

        Assert.True(split.ExitCode == 0, split.StdErr);
        JsonArray outputs = JsonNode.Parse(split.StdOut)!["outputs"]!.AsArray();
        Assert.Equal([1, 2], outputs.Select(static output => output!["section"]!.GetValue<int>()));
        Assert.All(outputs, static output => Assert.True(File.Exists(output!["path"]!.GetValue<string>())));
        Assert.All(outputs, static output => Assert.Equal("docx", output!["format"]!.GetValue<string>()));
    }

    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("images", "image")]
    [InlineData("tables", "table")]
    public void Extract_WritesEveryItemOfTheKind(string what, string kind)
    {
        WriteDocument();

        CliResult extracted = _workspace.Run("words", "extract", "report.docx", "--what", what, "--out-dir", what, "--output-format", "json");

        Assert.True(extracted.ExitCode == 0, extracted.StdErr);
        JsonObject result = JsonNode.Parse(extracted.StdOut)!.AsObject();
        Assert.Equal(what, result["what"]!.GetValue<string>());
        JsonArray items = result["items"]!.AsArray();
        Assert.All(items, item =>
        {
            Assert.Equal(kind, item!["kind"]!.GetValue<string>());
            Assert.True(File.Exists(item["path"]!.GetValue<string>()));
        });
        Assert.Contains(items, static item => item!["block"]?.GetValue<int>() >= 1);
    }

    [Fact]
    public void Extract_BomWithoutTables_IsAnInvalidOption()
    {
        WriteDocument();

        CliResult extracted = _workspace.Run("words", "extract", "report.docx", "--what", "text", "--bom", "--out-dir", "text", "--output-format", "json");

        Assert.Equal(2, extracted.ExitCode);
        Assert.Equal("OPTION_INVALID", JsonNode.Parse(extracted.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(Directory.Exists(_workspace.File("text")));
    }

    /// <summary>Two sections: a paragraph with an image, then a one-cell table.</summary>
    private void WriteDocument()
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("First section.");
        builder.InsertImage(Convert.FromBase64String(Png));
        builder.InsertBreak(BreakType.SectionBreakNewPage);
        builder.StartTable();
        builder.InsertCell();
        builder.Write("Cell");
        builder.EndRow();
        builder.EndTable();
        document.Save(_workspace.File("report.docx"), SaveFormat.Docx);
    }
}
