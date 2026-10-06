using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// Every product that offers <c>--encrypt-env</c> checks the format id of the output it is
/// about to write: an output that cannot carry a password is refused with
/// <c>OPTION_INVALID</c> naming that format, and nothing is published.
/// </summary>
public sealed class EncryptedOutputContractTests
{
    private const string PasswordVariable = "ASPOSE_CLI_TEST_ENCRYPT_PASSWORD";

    private static readonly Dictionary<string, (string[]? Setup, string[] Publish, string Output, string Format)> Cases =
        new(StringComparer.Ordinal)
        {
            ["cells convert"] = (["cells", "create", "book.xlsx"],
                ["cells", "convert", "book.xlsx", "--to", "csv", "--out", "book.csv"], "book.csv", "csv"),
            ["slides convert"] = (["slides", "create", "deck.pptx"],
                ["slides", "convert", "deck.pptx", "--to", "odp", "--out", "deck.odp"], "deck.odp", "odp"),
            ["words create"] = (null, ["words", "create", "doc.pdf", "--text", "content.txt"], "doc.pdf", "pdf"),
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
            CliResult prepared = workspace.Run([.. setup, "--output", "json"]);
            Assert.True(prepared.ExitCode == 0, prepared.StdErr);
        }

        string[] before = Directory.GetFileSystemEntries(workspace.Path);
        CliResult refused = workspace.RunWithEnv(
            new Dictionary<string, string?> { [PasswordVariable] = "output-secret" },
            [.. publish, "--encrypt-env", PasswordVariable, "--output", "json"]);

        Assert.True(refused.ExitCode != 0, refused.StdOut);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--encrypt-env", error["details"]!["option"]!.GetValue<string>());
        Assert.Contains($"'{format}'", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("output-secret", refused.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File(output)));
        Assert.Equal(before.Order(), Directory.GetFileSystemEntries(workspace.Path).Order());
    }
}
