using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every product's <c>create</c> reports the files it read as <c>inputs[]</c>, in command-line
/// order: <c>{path, format, sizeBytes, fingerprint, encrypted, option}</c>, one item per file,
/// and an empty list when it read none.
/// </summary>
public sealed class CreateInputsContractTests : IDisposable
{
    private static readonly string[] SourceFields = ["path", "format", "sizeBytes", "fingerprint", "encrypted", "option"];
    private readonly TempWorkspace _workspace = new();

    public CreateInputsContractTests()
    {
        File.WriteAllText(_workspace.File("outline.md"), "# Title\n\n## Second\n\n- point\n");
        File.WriteAllText(_workspace.File("notes.txt"), "First line\nSecond line\n");
        File.WriteAllBytes(_workspace.File("b.png"), ResourceHttpServer.Image);
        File.WriteAllBytes(_workspace.File("a.png"), ResourceHttpServer.Image);
    }

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("cells create book.xlsx")]
    [InlineData("cells create book.xlsx --sheets Data,Summary")]
    [InlineData("slides create deck.pptx")]
    [InlineData("words create doc.docx")]
    [InlineData("words create doc.docx --title Notes")]
    public void CreateFromNoFile_ReportsEmptyInputs(string commandLine)
    {
        JsonArray inputs = Inputs(commandLine);

        Assert.Empty(inputs);
    }

    [Theory]
    [InlineData("slides create deck.pptx --from-markdown outline.md --template brand.pptx", "--from-markdown:outline.md:md", "--template:brand.pptx:pptx")]
    [InlineData("slides create deck.pptx --template brand.pptx --from-markdown outline.md", "--template:brand.pptx:pptx", "--from-markdown:outline.md:md")]
    [InlineData("slides create deck.pptx --template brand.pptx", "--template:brand.pptx:pptx")]
    [InlineData("words create doc.docx --template brand.docx --from-markdown outline.md", "--template:brand.docx:docx", "--from-markdown:outline.md:md")]
    [InlineData("words create doc.docx --from-text notes.txt --template brand.docx", "--from-text:notes.txt:txt", "--template:brand.docx:docx")]
    [InlineData("words create doc.docx --from-markdown outline.md", "--from-markdown:outline.md:md")]
    [InlineData("pdf create doc.pdf --from-images b.png a.png", "--from-images:b.png:png", "--from-images:a.png:png")]
    [InlineData("pdf create doc.pdf --from-markdown outline.md", "--from-markdown:outline.md:md")]
    [InlineData("pdf create doc.pdf --from-text notes.txt", "--from-text:notes.txt:txt")]
    public void CreateFromFiles_ReportsEachFileWithItsOptionInCommandLineOrder(string commandLine, params string[] expected)
    {
        Require(_workspace.Run("slides", "create", "brand.pptx"));
        Require(_workspace.Run("words", "create", "brand.docx"));

        JsonArray inputs = Inputs(commandLine);

        Assert.Equal(expected, inputs.Select(static item => $"{item!["option"]}:{Path.GetFileName(item!["path"]!.GetValue<string>())}:{item!["format"]}"));
        Assert.All(inputs, item =>
        {
            JsonObject source = item!.AsObject();
            string path = source["path"]!.GetValue<string>();
            Assert.Equal(SourceFields, source.Select(static pair => pair.Key));
            Assert.True(Path.IsPathFullyQualified(path), path);
            Assert.Equal(new FileInfo(path).Length, source["sizeBytes"]!.GetValue<long>());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), source["fingerprint"]!["sha256"]!.GetValue<string>());
            Assert.False(source["encrypted"]!.GetValue<bool>());
        });
    }

    private JsonArray Inputs(string commandLine)
    {
        CliResult created = Require(_workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]));
        JsonObject result = JsonNode.Parse(created.StdOut)!.AsObject();
        Assert.False(result.ContainsKey("template"), created.StdOut);
        Assert.False(result.ContainsKey("markdown"), created.StdOut);
        return result["inputs"]!.AsArray();
    }

    private static CliResult Require(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return result;
    }
}
