using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The hint that writes a file without --encrypt and then encrypts it in place names the output's
/// password by a placeholder of its own beside the input's: run as written with a different
/// variable in each, the output opens with the second password and not with the first.
/// </summary>
public sealed partial class EncryptedAfterwardsHintCliTests
{
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal)
    {
        ["INPUT_PASSWORD"] = "Input-Secret-5e6f",
        ["OUTPUT_PASSWORD"] = "Output-Secret-7a8b",
    };

    [Category(TestCategory.Slow)]
    [Fact]
    public void TheHintsTwoPasswords_AreKeptApart()
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        workspace.RunWithEnv(Variables, "words", "create", "enc.docx", "--encrypt-env", "INPUT_PASSWORD", "--output-format", "json").Succeeded();

        CliResult refused = workspace.Run("words", "convert", "enc.docx", "--to", "pdf", "--out", "e.pdf", "--password", "Input-Secret-5e6f", "--encrypt", "Output-Secret-7a8b", "--output-format", "json");

        string hint = JsonNode.Parse(refused.StdErr)!["error"]!["hint"]!.GetValue<string>();
        string[] commands = [.. Command().Matches(hint).Select(static match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)];
        Assert.Equal(2, commands.Length);
        Assert.Contains("--password-env \"<password-env>\"", commands[0], StringComparison.Ordinal);
        Assert.Contains("\"userPasswordEnv\":\"<encrypt-env>\"", commands[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", hint, StringComparison.Ordinal);
        foreach (string command in commands)
        {
            CliResult ran = workspace.RunCommandLine(command.Replace("<password-env>", "INPUT_PASSWORD", StringComparison.Ordinal)
                .Replace("<encrypt-env>", "OUTPUT_PASSWORD", StringComparison.Ordinal), Variables);
            Assert.True(ran.ExitCode == 0, command + "\n" + ran.StdErr);
        }

        workspace.RunWithEnv(Variables, "pdf", "inspect", "e.pdf", "--password-env", "OUTPUT_PASSWORD", "--output-format", "json").Succeeded();
        Assert.Equal("PASSWORD_INVALID", JsonNode.Parse(workspace.RunWithEnv(Variables, "pdf", "inspect", "e.pdf", "--password-env", "INPUT_PASSWORD", "--output-format", "json").StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    // A command a hint quotes: in single quotes, or in backticks when it holds single quotes.
    [GeneratedRegex("`(aspose-cli [^`]*)`|'(aspose-cli [^']*)'")]
    private static partial Regex Command();
}
