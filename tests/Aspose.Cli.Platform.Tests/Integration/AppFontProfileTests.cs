using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

public sealed class AppFontProfileTests
{
    [Fact]
    public void RunningApp_RejectsReuseWithADifferentExplicitFontProfile()
    {
        using var workspace = new TempWorkspace();
        string firstRoot = workspace.File("fonts-a");
        string secondRoot = workspace.File("fonts-b");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        try
        {
            CliResult started = workspace.Run(
                "app", "--no-open", "--font-dir", firstRoot, "--output", "json");
            Assert.Equal(0, started.ExitCode);

            CliResult mismatch = workspace.Run(
                "app", "--no-open", "--font-dir", secondRoot, "--output", "json");

            Assert.Equal((int)ExitCode.Usage, mismatch.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(mismatch.StdOut));
            Assert.Equal(
                ErrorCodes.OptionInvalid.Name,
                JsonNode.Parse(mismatch.StdErr)!["error"]!["code"]!.GetValue<string>());
            Assert.DoesNotContain(firstRoot, mismatch.StdErr, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secondRoot, mismatch.StdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _ = workspace.Run("app", "stop", "--output", "json");
        }
    }
}
