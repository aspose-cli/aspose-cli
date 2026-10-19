using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
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

    /// <summary>
    /// Converts the product's own fixture document (<see cref="ScenarioFixtures"/>) to PDF, or to
    /// DOCX when the fixture is a PDF.
    /// </summary>
    [Theory]
    [MemberData(nameof(CliContractTests.ProductIds), MemberType = typeof(CliContractTests))]
    public void SingleFileConvert_ReportsOneOutputsItem(string product)
    {
        string primary = ScenarioFixtures.PrimaryFormat(product);
        string format = primary == "pdf" ? "docx" : "pdf";
        string input = "source." + primary;
        string output = "converted." + format;
        File.WriteAllBytes(_workspace.File(input), ScenarioFixtures.Read($"{product}.{primary}"));

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
    [MemberData(nameof(CliContractTests.ProductIds), MemberType = typeof(CliContractTests))]
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
