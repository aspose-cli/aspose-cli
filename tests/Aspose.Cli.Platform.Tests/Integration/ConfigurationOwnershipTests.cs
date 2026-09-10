using System.Text.Json;
using Aspose.Cli.TestKit;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

public sealed class ConfigurationOwnershipTests
{
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"productId\":\"another-cli\"}")]
    [InlineData("{\"schemaVersion\":\"1\",\"productId\":\"another-cli\"}")]
    [InlineData("{unfinished")]
    [InlineData("[]")]
    public void DocumentCommand_RejectsForeignOrMalformedConfigurationWithoutWriting(string marker)
    {
        using var workspace = new TempWorkspace();
        string config = workspace.ConfigDirectory;
        Directory.CreateDirectory(config);
        string ownership = Path.Combine(config, ".aspose-cli-config.json");
        PrivateUserStorage.WriteAllText(ownership, marker);
        string output = workspace.File("rejected.xlsx");

        CliResult result = workspace.Run(
            "cells", "create", output, "--output", "json");

        Assert.True(result.ExitCode == 2, result.StdErr);
        using JsonDocument error = JsonDocument.Parse(result.StdErr);
        Assert.Equal("OPTION_INVALID", error.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(output));
        Assert.Equal(marker, File.ReadAllText(ownership));
        Assert.Equal(new[] { ownership }, Directory.GetFiles(config, "*", SearchOption.AllDirectories));
    }
}
