using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every product that offers <c>--encrypt-env</c> checks the format id of the output it is
/// about to write: an output that cannot carry a password is refused with
/// <c>OPTION_INVALID</c> naming that format, and nothing is published. When an edit of another
/// product encrypts that format, the hint writes the output without the password, then encrypts it.
/// </summary>
public sealed partial class EncryptedOutputContractTests
{
    private const string PasswordVariable = "ASPOSE_CLI_TEST_ENCRYPT_PASSWORD";

    private static readonly Dictionary<string, (string[]? Setup, string[] Publish, string Output, string Format)> Cases =
        new(StringComparer.Ordinal)
        {
            ["cells convert"] = (["cells", "create", "book.xlsx"],
                ["cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv"], "book.csv", "csv"),
            ["slides convert"] = (["slides", "create", "deck.pptx"],
                ["slides", "convert", "deck.pptx", "--to", "odp", "--out", "deck.odp"], "deck.odp", "odp"),
            ["words create"] = (null, ["words", "create", "doc.rtf", "--from-text", "content.txt"], "doc.rtf", "rtf"),
        };

    public static TheoryData<string> Commands => [.. Cases.Keys];

    [Theory]
    [MemberData(nameof(Commands))]
    public void AnUnprotectableOutputIsRefusedAndNothingIsPublished(string command)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("content.txt"), "Encrypted output fixture");
        (string[]? setup, string[] publish, string output, string format) = Cases[command];
        if (setup is not null)
        {
            CliResult prepared = workspace.Run([.. setup, "--output-format", "json"]);
            Assert.True(prepared.ExitCode == 0, prepared.StdErr);
        }

        string[] before = Directory.GetFileSystemEntries(workspace.Path);
        CliResult refused = workspace.RunWithEnv(
            new Dictionary<string, string?> { [PasswordVariable] = "output-secret" },
            [.. publish, "--encrypt-env", PasswordVariable, "--output-format", "json"]);

        Assert.True(refused.ExitCode != 0, refused.StdOut);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--encrypt-env", error["details"]!["option"]!.GetValue<string>());
        Assert.Contains($"'{format}'", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("output-secret", refused.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File(output)));
        Assert.Equal(before.Order(), Directory.GetFileSystemEntries(workspace.Path).Order());
    }

    /// <summary>
    /// A conversion to PDF given <c>--encrypt-env</c> is refused as before; its hint converts
    /// without the option, then encrypts the PDF in place with the PDF edit and the variable the
    /// caller named. Run as written, the two commands write a PDF that needs that password.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("words", "doc.docx", null)]
    [InlineData("cells", "book.xlsx", "book.pdf")]
    [InlineData("slides", "deck.pptx", "deck.pdf")]
    public void ConversionToPdfWithAnOutputPassword_HintConvertsThenEncryptsThePdf(string product, string input, string? output)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        var variables = new Dictionary<string, string?> { [PasswordVariable] = "output-secret" };
        File.WriteAllText(workspace.File("content.md"), "# One\n\nFirst part.\n");
        CliResult created = workspace.Run([product, "create", input, .. product == "cells" ? Array.Empty<string>() : ["--from-markdown", "content.md"], "--output-format", "json"]);
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult refused = workspace.RunWithEnv(variables,
            [product, "convert", input, "--to", "pdf", .. output is null ? Array.Empty<string>() : ["--out", output], "--encrypt-env", PasswordVariable, "--output-format", "json"]);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        string[] commands = [.. QuotedCommand().Matches(hint).Select(static match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)];
        Assert.Equal(2, commands.Length);
        Assert.StartsWith($"aspose-cli {product} convert ", commands[0], StringComparison.Ordinal);
        Assert.DoesNotContain("--encrypt", commands[0], StringComparison.Ordinal);
        Assert.StartsWith("aspose-cli pdf edit ", commands[1], StringComparison.Ordinal);
        string pdf = workspace.File(output ?? System.IO.Path.ChangeExtension(input, ".pdf"));
        foreach (string command in commands)
        {
            CliResult ran = workspace.RunCommandLine(command, variables);
            Assert.True(ran.ExitCode == 0, $"{command}\n{ran.StdErr}");
        }

        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(workspace.Run("pdf", "inspect", pdf, "--output-format", "json").StdErr)!["error"]!["code"]!.GetValue<string>());
        CliResult opened = workspace.RunWithEnv(variables, "pdf", "inspect", pdf, "--password-env", PasswordVariable, "--output-format", "json");
        Assert.True(opened.ExitCode == 0, opened.StdErr);
    }

    [GeneratedRegex(@"`(aspose-cli [^`]+)`|(?<![\p{L}\p{N}])'(aspose-cli [^'`]+)'(?![\p{L}\p{N}])")]
    private static partial Regex QuotedCommand();
}
