using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// An input another program holds open without sharing is the same input error in the commands
/// that route a file to its product as in the product commands themselves.
/// </summary>
public sealed class LockedInputTests
{
    [Fact]
    public void RoutedCommands_ReportAHeldInputAsLocked()
    {
        using var workspace = new TempWorkspace();
        workspace.Run("cells", "create", "book.xlsx", "--sheets", "Data", "--output-format", "json").Succeeded();
        string path = workspace.File("book.xlsx");

        CliResult[] results;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            results =
            [
                workspace.Run("cells", "inspect", "book.xlsx", "--output-format", "json"),
                workspace.Run("review", "book.xlsx", "--output-format", "json"),
                workspace.Run("fonts", "check", "book.xlsx", "--output-format", "json"),
            ];
        }

        Assert.All(results, result =>
        {
            Assert.Equal(3, result.ExitCode);
            JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
            Assert.Equal("FILE_LOCKED", error["code"]!.GetValue<string>());
            Assert.Equal(path, error["details"]!["path"]!.GetValue<string>());
        });
    }
}
