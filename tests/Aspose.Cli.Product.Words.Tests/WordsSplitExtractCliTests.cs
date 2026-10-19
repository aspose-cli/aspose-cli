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
    [InlineData("images", "png")]
    [InlineData("tables", "csv")]
    public void Extract_ReportsEachFileAsAnOutput(string what, string format)
    {
        WriteDocument();

        CliResult extracted = _workspace.Run("words", "extract", "report.docx", "--what", what, "--out-dir", what, "--output-format", "json");

        Assert.True(extracted.ExitCode == 0, extracted.StdErr);
        JsonObject result = JsonNode.Parse(extracted.StdOut)!.AsObject();
        Assert.Equal(what, result["what"]!.GetValue<string>());
        Assert.False(result.ContainsKey("items"));
        JsonArray outputs = result["outputs"]!.AsArray();
        Assert.All(outputs, output =>
        {
            string path = output!["path"]!.GetValue<string>();
            Assert.False(output.AsObject().ContainsKey("kind"));
            Assert.Equal(new FileInfo(path).Length, output["sizeBytes"]!.GetValue<long>());
            Assert.Equal(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),
                output["fingerprint"]!["sha256"]!.GetValue<string>());
        });
        Assert.Contains(outputs, output => output!["format"]!.GetValue<string>() == format && output["block"]?.GetValue<int>() >= 1);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitByHeading1_WithOneHeading_Warns()
    {
        WriteChapters(1);

        CliResult split = _workspace.Run("words", "split", "book.docx", "--by", "heading1", "--out-dir", "parts", "--output-format", "json");

        Assert.True(split.ExitCode == 0, split.StdErr + split.StdOut);
        JsonNode result = JsonNode.Parse(split.StdOut)!;
        Assert.Equal(2, result["outputs"]!.AsArray().Count);
        JsonNode warning = Assert.Single(
            result["warnings"]!.AsArray(),
            static item => item!["code"]!.GetValue<string>() == "SPLIT_SINGLE_HEADING")!;
        string hint = warning["hint"]!.GetValue<string>();
        Assert.Contains("""{"op":"set_style","target":{"block":""", hint, StringComparison.Ordinal);
        Assert.Contains("""style":"Heading 1"}""", hint, StringComparison.Ordinal);
        Assert.Contains("--by sections", hint, StringComparison.Ordinal);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitByHeading1_WithTwoHeadings_DoesNotWarn()
    {
        WriteChapters(2);

        CliResult split = _workspace.Run("words", "split", "book.docx", "--by", "heading1", "--out-dir", "parts", "--output-format", "json");

        Assert.True(split.ExitCode == 0, split.StdErr + split.StdOut);
        Assert.DoesNotContain("SPLIT_SINGLE_HEADING", split.StdOut, StringComparison.Ordinal);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ExtractImages_InEvaluationMode_SkipsTheWatermarkWithoutDisclosure()
    {
        // An evaluation save draws its watermark picture into the header beside the document's own image.
        byte[] png = Convert.FromBase64String(Png);
        File.WriteAllBytes(_workspace.File("image.png"), png);
        File.WriteAllText(_workspace.File("source.md"), "# Title\n\nSome text.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "plain.docx", "--from-markdown", "source.md", "--license-mode", "evaluation").ExitCode);
        Assert.Equal(0, _workspace.RunWithInput(
            """{"ops":[{"op":"insert_image","at":{"block":1},"position":"after","path":"image.png"}]}""",
            "words", "edit", "plain.docx", "--ops", "-", "--out", "image.docx", "--license-mode", "evaluation").ExitCode);
        Assert.True(new Document(_workspace.File("image.docx")).GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>()
            .Count(static shape => shape.HasImage) >= 2);

        CliResult extracted = _workspace.Run(
            "words", "extract", "image.docx", "--what", "images", "--out-dir", "images", "--license-mode", "evaluation", "--output-format", "json");

        Assert.True(extracted.ExitCode == 0, extracted.StdErr + extracted.StdOut);
        JsonNode result = JsonNode.Parse(extracted.StdOut)!;
        JsonNode output = Assert.Single(result["outputs"]!.AsArray())!;
        Assert.EndsWith("image-001.png", output["path"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(png, File.ReadAllBytes(output["path"]!.GetValue<string>()));
        Assert.Equal("evaluation", result["license"]!["mode"]!.GetValue<string>());
        Assert.DoesNotContain("EVALUATION_MODE", result["warnings"]?.ToJsonString() ?? string.Empty, StringComparison.Ordinal);
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

    /// <summary>An introduction paragraph, then the given number of Heading 1 chapters with a paragraph each.</summary>
    private void WriteChapters(int chapters)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Introduction.");
        for (int chapter = 1; chapter <= chapters; chapter++)
        {
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
            builder.Writeln($"Chapter {chapter}");
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
            builder.Writeln($"Body {chapter}.");
        }

        document.Save(_workspace.File("book.docx"), SaveFormat.Docx);
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
