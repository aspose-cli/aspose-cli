using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The hint of <c>ENCRYPTION_REMOVED</c> names only what this build can do for the product and
/// format at hand: the protectable formats of the command, the command line of another product's
/// edit that encrypts the format written, which runs as given, or that no command encrypts it.
/// </summary>
public sealed class EncryptionRemovedHintCliTests : IDisposable
{
    private const string Variable = "DOC_PASSWORD";
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal) { [Variable] = "hint-secret" };
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("words", "locked.docx", "convert locked.docx --out plain.pdf", "write it as doc, dot, docx, docm, dotx, dotm, odt, ott instead", true)]
    [InlineData("slides", "locked.pptx", "convert locked.pptx --out plain.pdf", "write it as pptx, pptm instead", true)]
    [InlineData("cells", "locked.xlsx", "edit locked.xlsx --set Sheet1!A1=5 --out plain.csv", "write it as xlsx, xltx, xlsm, xltm, xlsb, xls, ods instead", false)]
    public void UnencryptedCopy_HintNamesOnlyWaysThisBuildOffers(string product, string locked, string commandLine, string own, bool encryptable)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        string[] create = product switch
        {
            "cells" => ["cells", "create", locked],
            "slides" => ["slides", "create", locked, "--from-markdown", "source.md"],
            _ => ["words", "create", locked, "--markdown", "source.md"],
        };
        CliResult created = Run([.. create, "--encrypt-env", Variable]);
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult result = Run([product, .. commandLine.Split(' '), "--password-env", Variable]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        string hint = JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED")!["hint"]!.GetValue<string>();
        Assert.Contains(own, hint, StringComparison.Ordinal);
        Match line = Regex.Match(hint, "`aspose-cli ([^`]+)`");
        if (!encryptable)
        {
            Assert.False(line.Success, hint);
            Assert.EndsWith("No command of this build can encrypt csv files.", hint, StringComparison.Ordinal);
            return;
        }

        // The command the hint names runs as given and protects the output with the input's password.
        Assert.True(line.Success, hint);
        Assert.DoesNotContain("No command", hint, StringComparison.Ordinal);
        // A POSIX shell drops a backslash outside quotes, so a Windows path must stay quoted.
        Assert.DoesNotMatch(@"(^|\s)[^'\s]*\\", line.Groups[1].Value);
        CliResult encrypted = Run(Tokens(line.Groups[1].Value));
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("pdf", "inspect", "plain.pdf").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, Run("pdf", "inspect", "plain.pdf", "--password-env", Variable).ExitCode);
    }

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);

    // Splits a command line as a shell does: on spaces outside single or double quotes.
    private static string[] Tokens(string commandLine)
    {
        var tokens = new List<string>();
        var token = new StringBuilder();
        char? quote = null;
        foreach (char character in commandLine)
        {
            if (quote is null && character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == quote)
            {
                quote = null;
            }
            else if (quote is null && character == ' ')
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
            }
            else
            {
                token.Append(character);
            }
        }

        if (token.Length > 0)
        {
            tokens.Add(token.ToString());
        }

        return [.. tokens];
    }
}
