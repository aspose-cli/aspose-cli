using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Common mistakes in each product's edit are answered with the one fix that works.</summary>
public sealed class MistakeRecoveryCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData("words", "a.docx", """{"op":"replace_text","find":"30","replacement":"15"}""", "replace")]
    [InlineData("slides", "a.pptx", """{"op":"replace_text","find":"30","replacement":"15"}""", "replace")]
    [InlineData("slides", "a.pptx", """{"op":"insert_chart","slide":1,"type":"bar"}""", "kind")]
    [InlineData("slides", "a.pptx", """{"op":"insert_chart","slide":1,"box":{}}""", "rect")]
    [InlineData("cells", "a.xlsx", """{"op":"set_style","range":"A1","style":{"bold":true}}""", "format_range")]
    [InlineData("pdf", "a.pdf", """{"op":"add_watermark","text":"DRAFT"}""", "add_watermark_text")]
    public void EditOperationMistake_SuggestsTheNameThatWorks(string product, string file, string operation, string suggestion)
    {
        // The document is read only after its operations parse, so any bytes do.
        File.WriteAllText(_workspace.File(file), "not a document");
        File.WriteAllText(_workspace.File("ops.json"), $$"""{"ops":[{{operation}}]}""");

        CliResult result = _workspace.Run(product, "edit", file, "--ops", "ops.json", "--output", "json");

        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(suggestion, error["details"]!["suggestion"]!.GetValue<string>());
    }
}
