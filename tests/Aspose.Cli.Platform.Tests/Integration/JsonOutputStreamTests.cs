using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>A JSON run writes one document: a result to stdout or an error to stderr, and nothing else.</summary>
public sealed class JsonOutputStreamTests
{
    [Fact]
    public void Json_SuccessAndFailureKeepTheirSingleDocumentContracts()
    {
        using var workspace = new TempWorkspace();
        CliResult success = workspace.Run("capabilities", "--output-format", "json");
        Assert.Equal(0, success.ExitCode);
        Assert.Empty(success.StdErr);
        Assert.NotNull(JsonNode.Parse(success.StdOut)!["schema"]);

        CliResult failure = workspace.Run("unknown-startup-command", "--output-format", "json");
        Assert.NotEqual(0, failure.ExitCode);
        Assert.Empty(failure.StdOut);
        Assert.Equal("USAGE_ERROR", JsonNode.Parse(failure.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Default_IsJsonWithoutStartupNoise()
    {
        using var workspace = new TempWorkspace();
        CliResult result = workspace.Run("capabilities");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StdErr);
        Assert.NotNull(JsonNode.Parse(result.StdOut)!["schema"]);
    }
}