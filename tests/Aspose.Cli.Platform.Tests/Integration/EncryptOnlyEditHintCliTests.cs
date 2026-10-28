using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// An edit given <c>--encrypt-env</c> and no operation is refused as before, with the same code
/// and exit code; when its vocabulary has an operation that encrypts, the hint also shows the
/// command that only encrypts: the same command line with that operation in place of
/// <c>--encrypt-env</c>. Running it writes a file that opens only with the password.
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
            $$$"""aspose-cli pdf edit plain.pdf --in-place --output-format json --ops '[{"op":"encrypt_document","userPasswordEnv":"{{{Variable}}}","ownerPasswordEnv":"{{{Variable}}}"}]'""",
            command);
        Assert.EndsWith("`.", hint, StringComparison.Ordinal);

        // The shown command runs as written: its operations are the one single-quoted argument.
        string[] parts = command.Split(" '", 2);
        CliResult encrypted = _workspace.RunWithEnv(Variables, [.. parts[0].Split(' ').Skip(1), parts[1].TrimEnd('\'')]);

        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("pdf", "inspect", "plain.pdf").StdErr)!["error"]!["code"]!.GetValue<string>());
        CliResult opened = Run("pdf", "inspect", "plain.pdf", "--password-env", Variable);
        Assert.True(opened.ExitCode == 0, opened.StdErr);
    }

    /// <summary>
    /// A command line that gives a secret literally, here the input's password, keeps the hint it
    /// had: the command that only encrypts would repeat the secret, which a hint never does.
    /// </summary>
    [Fact]
    public void PdfEditWithALiteralPassword_KeepsItsHintAndNeverRepeatsThePassword()
    {
        const string Literal = "literal-input-secret";
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md").ExitCode);

        CliResult refused = Run("pdf", "edit", "plain.pdf", "--in-place", "--password", Literal, "--encrypt-env", Variable);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(UsageHint, error["hint"]!.GetValue<string>());
        Assert.DoesNotContain(Literal, refused.StdErr, StringComparison.Ordinal);
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

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);
}
