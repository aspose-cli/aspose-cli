using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>
/// Aspose.Cells adds any bytes as a picture, so the CLI checks every image before the engine
/// sees it.
/// </summary>
public sealed class CellsImageInputTests
{
    [Theory]
    [InlineData("text")]
    [InlineData("cut")]
    public void InsertImage_RefusesAFileThatIsNotAnIntactImageAsAnInvalidOperation(string kind)
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("cells", "create", "source.xlsx", "--sheets", "Data").ExitCode);
        byte[] content = kind == "text"
            ? "not an image"u8.ToArray()
            : ResourceHttpServer.Image[..(ResourceHttpServer.Image.Length / 2)];
        File.WriteAllBytes(workspace.File("logo.png"), content);
        string ops = System.Text.Json.JsonSerializer.Serialize(new
        {
            ops = new object[]
            {
                new { op = "set_values", sheet = "Data", range = "A1", values = new[] { new[] { "Logo" } } },
                new { op = "insert_image", sheet = "Data", at = "B2", path = "logo.png" },
            },
        });

        CliResult edited = workspace.Run("cells", "edit", "source.xlsx", "--ops", ops, "--out", "result.xlsx", "--output", "json");

        Assert.True(edited.ExitCode == 4, edited.StdErr);
        JsonNode error = JsonNode.Parse(edited.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(1, error["details"]!["index"]!.GetValue<int>());
        Assert.Equal("insert_image", error["details"]!["op"]!.GetValue<string>());
        Assert.False(File.Exists(workspace.File("result.xlsx")));
    }
}
