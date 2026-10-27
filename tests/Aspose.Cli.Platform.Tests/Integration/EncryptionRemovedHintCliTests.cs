using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The hint of <c>ENCRYPTION_REMOVED</c> names only what this build can do for the product and
/// format at hand: the command again with <c>--encrypt-env</c>, the protectable formats of the
/// command, the path of the edit or conversion that encrypts the format written, or that no command
/// encrypts it. It names a command by its path alone, never an operation document; the product's
/// Skill says how to encrypt with it.
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
    [InlineData("words", "locked.docx", "convert locked.docx --out plain.pdf", "write it as doc, dot, docx, docm, dotx, dotm, odt, ott instead", "encrypt the pdf file with 'aspose-cli pdf edit'")]
    [InlineData("slides", "locked.pptx", "convert locked.pptx --out plain.pdf", "write it as pptx, pptm instead", "encrypt the pdf file with 'aspose-cli pdf edit'")]
    [InlineData("cells", "locked.xlsx", "edit locked.xlsx --set Sheet1!A1=5 --out plain.csv", "write it as xlsx, xltx, xlsm, xltm, xlsb, xls, ods instead", null)]
    [InlineData("pdf", "locked.pdf", "convert locked.pdf --out plain.md", "No command of this build can encrypt md files.", null)]
    public void UnencryptedCopy_HintNamesOnlyWaysThisBuildOffers(string product, string locked, string commandLine, string own, string? encryptor)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        string[] create = product switch
        {
            "cells" => ["cells", "create", locked],
            "slides" => ["slides", "create", locked, "--from-markdown", "source.md"],
            "pdf" => ["pdf", "create", locked, "--from-markdown", "source.md"],
            _ => ["words", "create", locked, "--from-markdown", "source.md"],
        };
        CliResult created = Run([.. create, "--encrypt-env", Variable]);
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult result = Run([product, .. commandLine.Split(' '), "--password-env", Variable]);

        Assert.True(result.ExitCode == 0, result.StdErr);
        string hint = Hint(result);
        Assert.Contains(own, hint, StringComparison.Ordinal);
        AssertNamesNoOperation(hint);
        if (encryptor is null)
        {
            Assert.DoesNotContain("'aspose-cli", hint, StringComparison.Ordinal);
            string written = Path.GetExtension(commandLine.Split(' ')[^1])[1..];
            Assert.EndsWith($"No command of this build can encrypt {written} files.", hint, StringComparison.Ordinal);
            return;
        }

        Assert.Contains(encryptor, hint, StringComparison.Ordinal);
        Assert.DoesNotContain("No command", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// A PDF converted to an Office format no edit can encrypt: the hint names the conversion of
    /// that product, which takes <c>--encrypt-env</c>. When that conversion reads PDF itself, as
    /// words convert does, the hint converts the locked input instead of the output, a second
    /// save that would embed every font of a docx that pdf convert wrote (PDF-DOCX-EMBED-FONTS).
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("docx", "If the output must stay protected, convert the pdf input itself to docx, not the docx file this command wrote, with 'aspose-cli words convert --encrypt-env'.")]
    [InlineData("xlsx", "If the output must stay protected, encrypt the xlsx file with 'aspose-cli cells convert'.")]
    [InlineData("pptx", "If the output must stay protected, encrypt the pptx file with 'aspose-cli slides convert'.")]
    public void PdfConvertedToAnOfficeFormat_HintNamesTheConversionThatEncryptsIt(string format, string expected)
    {
        CreateEncryptedPdf();

        CliResult result = Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--out", $"plain.{format}");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal(expected, Hint(result));
    }

    /// <summary>
    /// The conversion the hint of a PDF converted to docx names, run on the locked input with
    /// <c>--encrypt-env</c>, writes an encrypted docx without embedding every font.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void PdfConvertedToDocx_ConversionTheHintNamesEncryptsFromTheInput()
    {
        CreateEncryptedPdf();
        string hint = Hint(Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--out", "plain.docx"));
        const string Named = "the pdf input itself to docx, not the docx file this command wrote, with 'aspose-cli ";
        int at = hint.IndexOf(Named, StringComparison.Ordinal);
        Assert.True(at >= 0, hint);
        string[] path = hint[(at + Named.Length)..].Split(" --encrypt-env'", 2)[0].Split(' ');

        CliResult encrypted = Run([.. path, "locked.pdf", "--password-env", Variable, "--to", "docx", "--out", "plain.encrypted.docx", "--encrypt-env", Variable]);

        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
        Assert.DoesNotContain("FONTS_EMBEDDED", encrypted.StdOut, StringComparison.Ordinal);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("words", "inspect", "plain.encrypted.docx").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, Run("words", "inspect", "plain.encrypted.docx", "--password-env", Variable).ExitCode);
    }

    /// <summary>
    /// PDF/A forbids encryption: the hint of a conversion to PDF/A says the output cannot be
    /// encrypted and stay PDF/A, and names the edit that encrypts a copy of it, never the PDF/A
    /// file itself.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void PdfConvertedToPdfA_HintKeepsTheArchiveAndEncryptsACopy()
    {
        CreateEncryptedPdf();

        CliResult result = Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--to", "pdfa-2b", "--out", "archive.pdf");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("A pdfa-2b file cannot be encrypted and stay pdfa-2b. If the output must stay protected, encrypt a pdf copy of it with 'aspose-cli pdf edit'.", Hint(result));
        // The copy is named by the format it was written in, not by the extension pdf shares.
        Assert.StartsWith("The 'pdfa-2b' output is not encrypted", JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED")!["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    // locked.pdf: a PDF whose open password the variable holds.
    private void CreateEncryptedPdf()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        CliResult created = Run("pdf", "create", "locked.pdf", "--from-markdown", "source.md", "--encrypt-env", Variable);
        Assert.True(created.ExitCode == 0, created.StdErr);
    }

    /// <summary>
    /// When the batch decrypts a PDF, an unencrypted output is what was asked for: the hint says
    /// so and suggests no way to encrypt the output again but the edit that encrypts.
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
        Assert.EndsWith("To protect it, encrypt it with 'aspose-cli pdf edit' instead of decrypting it.", hint, StringComparison.Ordinal);
        AssertNamesNoOperation(hint);
        Assert.DoesNotContain("--encrypt-env", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("must stay protected", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pages imported from a PDF with an open password: the hint runs the edit again with
    /// <c>--encrypt-env</c>, and claims no option keeps the source's password.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ImportFromProtectedPdf_HintRunsTheEditAgainWithEncryptEnv()
    {
        CreateProtected();

        CliResult result = Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"import_pages","path":"protected.pdf","pages":"1","at":1,"sourcePasswordEnv":"{{{UserVariable}}}"}]""",
            "--out", "imported.pdf");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("If the output must stay protected, run the command again with --encrypt-env naming an environment variable that holds a password, "
            + "which encrypts the output with that password to open it and grants every permission.", Hint(result));
    }

    /// <summary>
    /// A PDF with only an owner password opens without one and only restricts what may be done:
    /// the hint of a split names the edit that restores that protection alone, not an option that
    /// would make the parts need a password to open.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void RestrictedPdf_HintNamesAnOwnerPasswordOnly()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md").ExitCode);
        // Readers may print and copy without the owner password.
        Assert.Equal(0, Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true,"copy":true}}]""",
            "--out", "restricted.pdf").ExitCode);

        CliResult result = Run("pdf", "split", "restricted.pdf", "--by", "pages", "--out-dir", "parts");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("If the output must stay protected, encrypt the pdf file with 'aspose-cli pdf edit' and an owner password only, "
            + "which restricts it as the input was and keeps it opening without a password.", Hint(result));
    }

    /// <summary>
    /// A DOCX copy of a PDF with only an owner password cannot be restricted that way: only a
    /// conversion encrypts DOCX, and with a password to open, which the hint says.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void RestrictedPdfToDocx_HintSaysTheOutputWouldNeedAPasswordToOpen()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md").ExitCode);
        Assert.Equal(0, Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true,"copy":true}}]""",
            "--out", "restricted.pdf").ExitCode);

        CliResult result = Run("words", "convert", "restricted.pdf", "--to", "docx", "--out", "restricted.docx");

        Assert.True(result.ExitCode == 0, result.StdErr);
        Assert.Equal("If the output must stay protected, run the command again with --encrypt-env naming an environment variable that holds a password, "
            + "which encrypts the output with that password to open it, although the input opens without one.", Hint(result));
    }

    // protected.pdf: an open password, a different owner password, and only printing allowed.
    private void CreateProtected()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n\n# Two\n\nSecond part.\n");
        Assert.Equal(0, Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md").ExitCode);
        CliResult encrypted = Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","userPasswordEnv":"{{{UserVariable}}}","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true}}]""",
            "--out", "protected.pdf");
        Assert.True(encrypted.ExitCode == 0, encrypted.StdErr);
    }

    private static string Hint(CliResult result) => JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray()
        .Single(static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED")!["hint"]!.GetValue<string>();

    // A hint names a command by its path, never an operation or an operation document to run.
    private static void AssertNamesNoOperation(string hint)
    {
        Assert.DoesNotContain("\"ops\"", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("encrypt_document", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("`", hint, StringComparison.Ordinal);
    }

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);
}
