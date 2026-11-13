using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The corrected command line of a mistake keeps a password the caller gave on stdin, which it
/// cannot repeat, as the option of its environment variable with the quoted placeholder every
/// other hint names for it: run with a variable in its place, the command protects its output as
/// the mistaken one asked, never with one protection fewer.
/// </summary>
public sealed class StandardInputPasswordMistakeCliTests
{
    private const string Password = "Mistaken-5f2a";
    private const string Variable = "PW";

    /// <param name="named">Whether the line also names the variable with --encrypt-env, which the correction keeps alone rather than naming the option twice.</param>
    [Theory]
    [InlineData("cells", "n e w.xlsx", false)]
    [InlineData("words", "n e w.docx", false)]
    [InlineData("cells", "n e w.xlsx", true)]
    public void AnOutputGivenAsTheFile_IsCorrectedWithTheEncryptionPassedOnByAVariable(string product, string file, bool named)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);

        // The caller who names the variable has it set.
        var variables = new Dictionary<string, string?> { [Variable] = Password };
        CliResult mistaken = workspace.RunCommandLine(
            $"aspose-cli {product} create --out \"{file}\" --encrypt-stdin{(named ? $" --encrypt-env {Variable}" : "")} --output-format json", variables, Password + "\n");

        Assert.True(mistaken.ExitCode == 2, mistaken.StdOut + mistaken.StdErr);
        string suggestion = JsonNode.Parse(mistaken.StdErr)!["error"]!["details"]!["suggestions"]![0]!.GetValue<string>();
        Assert.Equal($"aspose-cli {product} create \"{file}\" --encrypt-env {(named ? Variable : "\"<password-env>\"")} --output-format json", suggestion);

        CliResult created = workspace.RunCommandLine(suggestion.Replace("<password-env>", Variable, StringComparison.Ordinal), variables);
        Assert.True(created.ExitCode == 0, created.StdOut + created.StdErr);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(workspace.Run(product, "inspect", file, "--output-format", "json").StdErr)!["error"]!["code"]!.GetValue<string>());
        workspace.RunWithEnv(variables, product, "inspect", file, "--password-env", Variable, "--output-format", "json").Succeeded();
    }
}
