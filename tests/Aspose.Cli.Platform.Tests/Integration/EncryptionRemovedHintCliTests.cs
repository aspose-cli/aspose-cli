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
    private const string UserVariable = "DOC_USER_PASSWORD";
    private const string OwnerVariable = "DOC_OWNER_PASSWORD";
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal)
    {
        [Variable] = "hint-secret",
        [UserVariable] = "hint-user",
        [OwnerVariable] = "hint-owner",
    };
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
            _ => ["words", "create", locked, "--from-markdown", "source.md"],
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

    /// <summary>
    /// When the batch decrypts a PDF, an unencrypted output is what was asked for: the hint says
    /// so and suggests no way to encrypt the output again but the operation that encrypts.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void DecryptedPdf_HintSaysTheOutputIsExpected()
    {
        CreateProtected();

        CliResult result = Run("pdf", "edit", "protected.pdf", "--password-env", OwnerVariable, "--ops", """[{"op":"decrypt_document"}]""", "--out", "decrypted.pdf");

        Assert.True(result.ExitCode == 0, result.StdErr);
        string hint = Hint(result);
        Assert.Contains("expected result", hint, StringComparison.Ordinal);
        Assert.Contains("encrypt_document", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--encrypt-env", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("must stay protected", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("`aspose-cli", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pages imported from a PDF with an open password, a different owner password and only
    /// printing allowed: the command line the hint names restores both passwords and the
    /// permissions, and the hint claims no option keeps the source's password.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ImportFromProtectedPdf_HintRestoresItsPasswordsAndPermissions()
    {
        CreateProtected();
        JsonObject granted = Permissions("protected.pdf", UserVariable);

        CliResult result = Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"import_pages","path":"protected.pdf","pages":"1","at":1,"sourcePasswordEnv":"{{{UserVariable}}}"}]""",
            "--out", "imported.pdf");

        Assert.True(result.ExitCode == 0, result.StdErr);
        string hint = Hint(result);
        Assert.DoesNotContain("keeps the input's password", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("expected result", hint, StringComparison.Ordinal);
        Match line = Regex.Match(hint, "`aspose-cli ([^`]+)`");
        Assert.True(line.Success, hint);
        CliResult encrypted = Run(Tokens(line.Groups[1].Value
            .Replace("<user-password-env>", UserVariable, StringComparison.Ordinal)
            .Replace("<owner-password-env>", OwnerVariable, StringComparison.Ordinal)));
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);

        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("pdf", "inspect", "imported.pdf").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(granted.ToJsonString(), Permissions("imported.pdf", UserVariable).ToJsonString());
        Assert.True(Permissions("imported.pdf", OwnerVariable)["ownerAccess"]!.GetValue<bool>());
    }

    /// <summary>
    /// A PDF with only an owner password opens without one and only restricts what may be done:
    /// the hint of a split names the edit that restores that protection alone, which runs as
    /// given, not an option that would make the parts need a password to open.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void RestrictedPdf_HintNamesAnOwnerPasswordOnly()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-text", "source.md").ExitCode);
        // Readers may print and copy without the owner password.
        Assert.Equal(0, Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true,"copy":true}}]""",
            "--out", "restricted.pdf").ExitCode);
        JsonObject granted = Permissions("restricted.pdf", variable: null);

        CliResult result = Run("pdf", "split", "restricted.pdf", "--by", "pages", "--out-dir", "parts");

        Assert.True(result.ExitCode == 0, result.StdErr);
        string hint = Hint(result);
        Assert.DoesNotContain("keeps the input's password", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("expected result", hint, StringComparison.Ordinal);
        Assert.Contains("owner password only", hint, StringComparison.Ordinal);
        Match line = Regex.Match(hint, "`aspose-cli ([^`]+)`");
        Assert.True(line.Success, hint);
        Assert.DoesNotContain("userPasswordEnv", line.Value, StringComparison.Ordinal);
        string part = Path.GetRelativePath(_workspace.Path, Directory.GetFiles(_workspace.File("parts"), "*.pdf").Order(StringComparer.Ordinal).First());
        CliResult encrypted = Run(Tokens(line.Groups[1].Value
            .Replace("<file>", part, StringComparison.Ordinal)
            .Replace("<owner-password-env>", OwnerVariable, StringComparison.Ordinal)));
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        // The part opens without a password and grants what the input granted.
        JsonObject restored = Permissions(part, variable: null);
        Assert.True(restored["print"]!.GetValue<bool>() && restored["copy"]!.GetValue<bool>() && !restored["modify"]!.GetValue<bool>(), restored.ToJsonString());
        Assert.Equal(granted.ToJsonString(), restored.ToJsonString());
    }

    // protected.pdf: an open password, a different owner password, and only printing allowed.
    private void CreateProtected()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n\n# Two\n\nSecond part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-text", "source.md").ExitCode);
        CliResult encrypted = Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","userPasswordEnv":"{{{UserVariable}}}","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true}}]""",
            "--out", "protected.pdf");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
    }

    private static string Hint(CliResult result) => JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray()
        .Single(static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED")!["hint"]!.GetValue<string>();

    // The permissions of an encrypted PDF as inspect reports them, opened with the password the variable holds.
    private JsonObject Permissions(string path, string? variable)
    {
        CliResult inspected = variable is null
            ? Run("pdf", "inspect", path, "--detail", "permissions")
            : Run("pdf", "inspect", path, "--detail", "permissions", "--password-env", variable);
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        JsonNode result = JsonNode.Parse(inspected.StdOut)!;
        Assert.True(result["input"]!["encrypted"]!.GetValue<bool>());
        return result["permissions"]!.AsObject();
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
