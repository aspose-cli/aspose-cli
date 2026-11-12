using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// An edit given <c>--encrypt-env</c> and no operation is refused as before, with the same code
/// and exit code; when its product declares the operation that encrypts as <c>--encrypt-env</c>
/// does, the hint also shows the command that only encrypts: the same command line with that
/// operation in place of <c>--encrypt-env</c>. Running it writes a file that opens only with the
/// password and grants what <c>--encrypt-env</c> grants.
/// </summary>
public sealed class EncryptOnlyEditHintCliTests : IDisposable
{
    private const string Variable = "DOC_PASSWORD";
    private const string UsageHint = "Run the command again with --help for usage; 'aspose-cli --help' lists the commands and 'aspose-cli docs' the documentation topics.";
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal) { [Variable] = "encrypt-only-secret" };
    private readonly TempWorkspace _workspace = new();

    public EncryptOnlyEditHintCliTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Theory]
    [InlineData(null, "USAGE_ERROR", 2)]
    [InlineData("""{"ops":[]}""", "OPS_INVALID", 4)]
    public void PdfEditWithoutOperations_HintShowsTheEditThatOnlyEncrypts(string? ops, string code, int exitCode)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md").ExitCode);

        CliResult refused = Run(["pdf", "edit", "plain.pdf", "--in-place", "--encrypt-env", Variable, .. ops is null ? Array.Empty<string>() : ["--ops", ops]]);

        Assert.Equal(exitCode, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal(code, error["code"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        const string Shown = "To only encrypt, run `";
        int at = hint.IndexOf(Shown, StringComparison.Ordinal);
        Assert.True(at >= 0, hint);
        string command = hint[(at + Shown.Length)..].Split('`', 2)[0];
        Assert.Equal(
            $"aspose-cli pdf edit \"{_workspace.File("plain.pdf")}\" --in-place --ops "
                + $$$"""'[{"op":"encrypt_document","userPasswordEnv":"{{{Variable}}}","ownerPasswordEnv":"{{{Variable}}}","permissions":{"print":true,"copy":true,"modify":true,"annotate":true,"fillForms":true,"extractAccessibility":true,"assemble":true,"printHighResolution":true}}]' --output-format json""",
            command);
        Assert.EndsWith("`.", hint, StringComparison.Ordinal);

        // The shown command runs as written: its operations are the one single-quoted argument.
        CliResult encrypted = _workspace.RunCommandLine(command, Variables);

        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("pdf", "inspect", "plain.pdf").StdErr)!["error"]!["code"]!.GetValue<string>());
        CliResult opened = Run("pdf", "inspect", "plain.pdf", "--password-env", Variable);
        Assert.True(opened.ExitCode == 0, opened.StdErr);
    }

    /// <summary>
    /// The edit the hint shows to only encrypt writes the passwords and permissions that
    /// <c>--encrypt-env</c> writes after an operation: the file grants readers every permission.
    /// </summary>
    [Fact]
    public void PdfEditThatOnlyEncrypts_GrantsThePermissionsEncryptEnvGrants()
    {
        File.WriteAllBytes(_workspace.File("shown.pdf"), ScenarioFixtures.Read("pdf.pdf"));
        File.WriteAllBytes(_workspace.File("option.pdf"), ScenarioFixtures.Read("pdf.pdf"));

        CliResult refused = Run("pdf", "edit", "shown.pdf", "--in-place", "--encrypt-env", Variable);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        const string Shown = "To only encrypt, run `";
        int at = hint.IndexOf(Shown, StringComparison.Ordinal);
        Assert.True(at >= 0, hint);
        string command = hint[(at + Shown.Length)..].Split('`', 2)[0];
        // The shown command runs as written: its operations are the one single-quoted argument.
        CliResult onlyEncrypted = _workspace.RunCommandLine(command, Variables);
        Assert.True(onlyEncrypted.ExitCode == 0, onlyEncrypted.StdErr);
        CliResult encrypted = Run("pdf", "edit", "option.pdf", "--in-place", "--ops", """[{"op":"set_properties","title":"Encrypted"}]""", "--encrypt-env", Variable);
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);

        JsonNode expected = Permissions("option.pdf");
        Assert.All(
            ["print", "copy", "modify", "annotate", "fillForms", "extractAccessibility", "assemble", "printHighResolution"],
            name => Assert.True(expected[name]!.GetValue<bool>(), $"--encrypt-env does not grant {name}: {expected.ToJsonString()}"));
        JsonNode actual = Permissions("shown.pdf");
        Assert.True(JsonNode.DeepEquals(expected, actual), $"--encrypt-env: {expected.ToJsonString()}; the shown edit: {actual.ToJsonString()}");
    }

    /// <summary>
    /// A command line that gives the input's password literally never has it repeated: the command
    /// that only encrypts passes it on by a placeholder for the variable that holds it, and run with
    /// the placeholder filled in, it encrypts the file.
    /// </summary>
    [Fact]
    public void PdfEditWithALiteralPassword_HintPassesItOnByAPlaceholderAndNeverRepeatsIt()
    {
        const string Literal = "literal-input-secret";
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md").ExitCode);

        CliResult refused = Run("pdf", "edit", "plain.pdf", "--in-place", "--password", Literal, "--encrypt-env", Variable);

        Assert.Equal(2, refused.ExitCode);
        Assert.DoesNotContain(Literal, refused.StdErr, StringComparison.Ordinal);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        string hint = error["hint"]!.GetValue<string>();
        string shown = UsageHint + " To only encrypt, run `";
        Assert.StartsWith(shown, hint, StringComparison.Ordinal);
        string command = hint[shown.Length..].Split('`', 2)[0];
        Assert.Contains(" --password-env \"<password-env>\" ", command, StringComparison.Ordinal);

        CliResult encrypted = _workspace.RunCommandLine(command.Replace("<password-env>", Variable, StringComparison.Ordinal), Variables);

        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("pdf", "inspect", "plain.pdf").StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// A vocabulary without an operation that encrypts keeps the hint it had: Cells here, and Slides
    /// and Words alike, whose edits share the same skeleton and have no such operation either.
    /// </summary>
    [Theory]
    [InlineData(null, "USAGE_ERROR", 2, UsageHint)]
    [InlineData("""{"ops":[]}""", "OPS_INVALID", 4, "Fix the named operation; 'aspose-cli schema v2/cells/ops' documents every operation.")]
    public void CellsEditWithoutOperations_KeepsItsHint(string? ops, string code, int exitCode, string expected)
    {
        Assert.Equal(0, Run("cells", "create", "book.xlsx").ExitCode);

        CliResult refused = Run(["cells", "edit", "book.xlsx", "--in-place", "--encrypt-env", Variable, .. ops is null ? Array.Empty<string>() : ["--ops", ops]]);

        Assert.Equal(exitCode, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal(code, error["code"]!.GetValue<string>());
        Assert.Equal(expected, error["hint"]!.GetValue<string>());
    }

    // The passwords and permissions that pdf inspect reads from a file it opens with the password.
    private JsonNode Permissions(string file)
    {
        CliResult inspected = Run("pdf", "inspect", file, "--password-env", Variable, "--detail", "permissions");
        Assert.True(inspected.ExitCode == 0, inspected.StdErr);
        return JsonNode.Parse(inspected.StdOut)!["permissions"]!;
    }

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);
}
