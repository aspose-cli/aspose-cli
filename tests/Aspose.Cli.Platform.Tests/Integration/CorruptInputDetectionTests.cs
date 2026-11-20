using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A product command whose input it cannot open reports its own corrupt input.
/// </summary>
[Category(TestCategory.Slow)]
public sealed class CorruptInputDetectionTests
{
    [Theory]
    [InlineData("empty.docx", "")]
    [InlineData("damaged.docx", "PK\u0003\u0004 truncated header")]
    public void RoutedCommand_OnContentNoProductRecognizes_IsCorruptAsInTheProduct(string file, string content)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File(file), content);

        CliResult result = workspace.Run("words", "read", file, "--output-format", "json");

        // The extension names words and no product recognizes the content: a damaged file, not a renamed one.
        Assert.Equal(3, result.ExitCode);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Equal(workspace.File(file), error["details"]!["path"]!.GetValue<string>());
        Assert.Null(error["details"]!["reason"]);
    }
}
