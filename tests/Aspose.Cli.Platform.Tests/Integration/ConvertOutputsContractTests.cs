using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every product's <c>convert</c> reports what it wrote as <c>outputs[]</c>, even one file:
/// <c>{&lt;unit&gt;?, path, format, sizeBytes, fingerprint, encrypted?}</c>, never an <c>output</c> object.
/// </summary>
public sealed class ConvertOutputsContractTests : IDisposable
{
    private static readonly string[] FileFields = ["path", "format", "sizeBytes", "fingerprint", "encrypted"];
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("cells", "book.xlsx", "book.csv", "csv")]
    [InlineData("words", "doc.docx", "doc.pdf", "pdf")]
    [InlineData("slides", "deck.pptx", "deck.pdf", "pdf")]
    [InlineData("pdf", "doc.pdf", "doc.docx", "docx")]
    public void SingleFileConvert_ReportsOneOutputsItem(string product, string input, string output, string format)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nText.\n");
        CliResult created = product switch
        {
            "cells" => _workspace.Run("cells", "create", input),
            "pdf" => _workspace.Run("pdf", "create", input, "--from-markdown", "source.md"),
            _ => _workspace.Run(product, "create", input, "--from-markdown", "source.md"),
        };
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult converted = _workspace.Run(product, "convert", input, "--out", output, "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        JsonObject result = JsonNode.Parse(converted.StdOut)!.AsObject();
        Assert.False(result.ContainsKey("output"), converted.StdOut);
        JsonObject item = Assert.Single(result["outputs"]!.AsArray())!.AsObject();
        Assert.Equal(_workspace.File(output), item["path"]!.GetValue<string>());
        Assert.Equal(format, item["format"]!.GetValue<string>());
        Assert.Equal(new FileInfo(_workspace.File(output)).Length, item["sizeBytes"]!.GetValue<long>());
        Assert.Equal(64, item["fingerprint"]!["sha256"]!.GetValue<string>().Length);
        Assert.All(item.Select(static pair => pair.Key), key => Assert.Contains(key, FileFields));
    }

    /// <summary>A convert writes one document, never one file per unit, so its outputs items declare no unit.</summary>
    [Theory]
    [InlineData("cells")]
    [InlineData("pdf")]
    [InlineData("slides")]
    [InlineData("words")]
    public void ConvertResultSchema_OutputsItemsDeclareOnlyFileFields(string product)
    {
        CliResult schema = _workspace.Run("schema", $"v2/{product}/convert-result");

        Assert.True(schema.ExitCode == 0, schema.StdErr);
        JsonObject document = JsonNode.Parse(schema.StdOut)!.AsObject();
        string reference = document["properties"]!["outputs"]!["items"]!["$ref"]!.GetValue<string>();
        JsonObject item = document["$defs"]![reference["#/$defs/".Length..]]!.AsObject();
        Assert.Equal(FileFields, item["properties"]!.AsObject().Select(static pair => pair.Key));
    }
}
