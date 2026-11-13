using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The hint of <c>ENCRYPTION_REMOVED</c> gives one step, after the reason for it, as commands that
/// run as written and name only what this build can do: the command again with
/// <c>--encrypt-env</c> and <c>--overwrite</c>, the format the command protects instead, the edit
/// that encrypts the file in place, or the conversion of the input itself, splitting a page
/// selection first. A step that writes an encrypted copy beside the unencrypted file ends by
/// telling to delete that file. Each step runs as the hint writes it, on the default output path
/// and on an output that already existed, and protects what it writes.
/// </summary>
public sealed partial class EncryptionRemovedHintCliTests : IDisposable
{
    private const string Variable = "DOC_PASSWORD";
    private const string UserVariable = "DOC_USER_PASSWORD";
    private const string OwnerVariable = "DOC_OWNER_PASSWORD";
    private const string DeleteCopy = "Once the encrypted copy exists, delete the unencrypted file this command wrote.";
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal)
    {
        [Variable] = "hint-secret",
        [UserVariable] = "hint-user",
        [OwnerVariable] = "hint-owner",
    };
    private readonly TempWorkspace _workspace = new();

    public EncryptionRemovedHintCliTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// No command encrypts csv: the hint converts the workbook to xlsx with its password instead,
    /// then says to delete the csv. Run as written, it writes an xlsx that needs the password.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkbookConvertedToCsv_HintWritesItAsAnEncryptedWorkbook(bool existingOutput)
    {
        Created(Run("cells", "create", "locked.xlsx", "--encrypt-env", Variable));

        CliResult result = Run(["cells", "convert", "locked.xlsx", "--password-env", Variable, "--to", "csv", .. Output("plain.csv", existingOutput)]);

        string hint = Hint(result);
        Assert.StartsWith("No command of this build can encrypt csv files. If the output must stay protected, run '", hint, StringComparison.Ordinal);
        Assert.EndsWith("', which writes it as xlsx encrypted with a password to open it. " + DeleteCopy, hint, StringComparison.Ordinal);
        string written = Written(Step(Assert.Single(Commands(hint))));
        Assert.EndsWith(".xlsx", written, StringComparison.Ordinal);
        AssertNeedsThePassword("cells", written);
    }

    /// <summary>
    /// An edit written as csv: the hint runs the same edit again to an xlsx, which keeps the edit
    /// and needs the password, and then says to delete the csv.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void WorkbookEditedToCsv_HintRunsTheEditAgainToAnEncryptedWorkbook()
    {
        Created(Run("cells", "create", "locked.xlsx", "--encrypt-env", Variable));

        CliResult result = Run("cells", "edit", "locked.xlsx", "--set", "Sheet1!A1=5", "--out", "plain.csv", "--password-env", Variable);

        string hint = Hint(result);
        Assert.StartsWith("No command of this build can encrypt csv files. If the output must stay protected, run 'aspose-cli cells edit ", hint, StringComparison.Ordinal);
        Assert.EndsWith(DeleteCopy, hint, StringComparison.Ordinal);
        string written = Written(Step(Assert.Single(Commands(hint))));
        Assert.Equal(_workspace.File("plain.xlsx"), written, ignoreCase: true);
        AssertNeedsThePassword("cells", written);
        JsonNode read = Run("cells", "read", written, "--password-env", Variable, "--range", "Sheet1!A1").Json();
        Assert.Equal("5", read["sheet"]!["cells"]![0]![0]!["v"]!.ToString());
    }

    /// <summary>
    /// A PDF converted to docx: the hint converts the encrypted input itself with Words and
    /// replaces the docx this command wrote, so nothing is left to delete, and never converts that
    /// docx again, a second save that would embed every font (PDF-DOCX-EMBED-FONTS).
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PdfConvertedToDocx_HintConvertsTheInputItselfOverTheDocx(bool existingOutput)
    {
        CreateEncryptedPdf(pages: 1);

        CliResult result = Run(["pdf", "convert", "locked.pdf", "--password-env", Variable, "--to", "docx", .. Output("plain.docx", existingOutput)]);

        string hint = Hint(result);
        Assert.StartsWith("This command cannot encrypt docx files, and converting the one it wrote again could change it. "
            + "If the output must stay protected, run 'aspose-cli words convert ", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("delete", hint, StringComparison.Ordinal);
        string command = Assert.Single(Commands(hint));
        Assert.Contains(Quoted(_workspace.File("locked.pdf")), command, StringComparison.OrdinalIgnoreCase);
        JsonNode converted = Step(command);
        Assert.DoesNotContain(Codes(converted), static code => code == "FONTS_EMBEDDED");
        Assert.Equal(Written(result.Json()), Written(converted), ignoreCase: true);
        AssertNeedsThePassword("words", Written(converted));
    }

    /// <summary>
    /// A selection of pages converted to docx: the hint splits those pages from the encrypted
    /// input and converts the part with Words over the docx, never converting the docx again.
    /// Run as written, the docx holds the selected page alone and needs the password.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PagesOfAPdfConvertedToDocx_HintSplitsThemAndConvertsThePart(bool existingOutput)
    {
        CreateEncryptedPdf(pages: 3);

        CliResult result = Run(["pdf", "convert", "locked.pdf", "--password-env", Variable, "--pages", "2", "--to", "docx", .. Output("page.docx", existingOutput)]);

        string hint = Hint(result);
        string[] commands = Commands(hint);
        Assert.Equal(2, commands.Length);
        Assert.StartsWith("aspose-cli pdf split " + Quoted(_workspace.File("locked.pdf")) + " --password-env " + Variable + " --by pages --groups 2 ", commands[0], StringComparison.OrdinalIgnoreCase);
        // The part goes in a folder of its own beside the output, named after it.
        string folder = Path.GetFileNameWithoutExtension(Written(result.Json())) + "-encrypted-part";
        string part = Path.Combine(_workspace.Path, folder, "locked.1.pdf");
        Assert.StartsWith("aspose-cli words convert " + Quoted(part) + " ", commands[1], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith($"; once the conversion has run, delete {Quoted(Path.Combine(_workspace.Path, folder))}, the folder the split created beside the output for the part.", hint, StringComparison.OrdinalIgnoreCase);
        Step(commands[0]);
        JsonNode converted = Step(commands[1]);
        // The folder the hint says may be deleted holds the part the split wrote and the conversion read.
        Assert.Equal(part, converted["input"]!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.DoesNotContain(Codes(converted), static code => code == "FONTS_EMBEDDED");
        Assert.Equal(Written(result.Json()), Written(converted), ignoreCase: true);
        AssertNeedsThePassword("words", Written(converted));
        JsonNode inspected = Run("words", "inspect", Written(converted), "--password-env", Variable).Json();
        Assert.Equal(1, inspected["document"]!["pageCount"]!.GetValue<int>());
    }

    /// <summary>
    /// A PDF converted to a format another product's conversion encrypts but cannot read PDF for:
    /// the hint converts the file this command wrote to an encrypted copy, then says to delete it.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("xlsx", "cells")]
    [InlineData("pptx", "slides")]
    public void PdfConvertedToAnOfficeFormat_HintWritesAnEncryptedCopy(string format, string product)
    {
        CreateEncryptedPdf(pages: 1);

        CliResult result = Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--out", $"plain.{format}");

        string hint = Hint(result);
        Assert.StartsWith($"This command cannot encrypt {format} files. If the output must stay protected, run 'aspose-cli {product} convert ", hint, StringComparison.Ordinal);
        Assert.EndsWith(DeleteCopy, hint, StringComparison.Ordinal);
        string copy = Written(Step(Assert.Single(Commands(hint))));
        Assert.Equal(_workspace.File($"plain.encrypted.{format}"), copy, ignoreCase: true);
        AssertNeedsThePassword(product, copy);
    }

    /// <summary>
    /// A Words or Slides document converted to PDF: no conversion encrypts PDF, so the hint
    /// encrypts the PDF in place with the PDF edit and the input's password variable.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("words", "locked.docx")]
    [InlineData("slides", "locked.pptx")]
    public void DocumentConvertedToPdf_HintEncryptsThePdfInPlace(string product, string locked)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Created(Run(product, "create", locked, "--from-markdown", "source.md", "--encrypt-env", Variable));

        CliResult result = Run(product, "convert", locked, "--password-env", Variable, "--out", "plain.pdf");

        string hint = Hint(result);
        Assert.StartsWith("This command cannot encrypt pdf files. If the output must stay protected, run `aspose-cli pdf edit ", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("delete", hint, StringComparison.Ordinal);
        string encrypted = Written(Step(Assert.Single(Commands(hint))));
        Assert.Equal(_workspace.File("plain.pdf"), encrypted, ignoreCase: true);
        AssertNeedsThePassword("pdf", encrypted);
    }

    /// <summary>
    /// PDF/A forbids encryption: the hint says the output cannot be encrypted and stay PDF/A, and
    /// encrypts a copy of it with the PDF edit, never the PDF/A file itself, which stays as written.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void PdfConvertedToPdfA_HintKeepsTheArchiveAndEncryptsACopy()
    {
        CreateEncryptedPdf(pages: 1);

        CliResult result = Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--to", "pdfa-2b", "--out", "archive.pdf");

        string hint = Hint(result);
        Assert.StartsWith("A pdfa-2b file cannot be encrypted and stay pdfa-2b. If the output must stay protected, run `aspose-cli pdf edit ", hint, StringComparison.Ordinal);
        Assert.EndsWith(DeleteCopy, hint, StringComparison.Ordinal);
        // The copy is named by the format it was written in, not by the extension pdf shares.
        Assert.StartsWith("The 'pdfa-2b' output is not encrypted", Warning(result)["message"]!.GetValue<string>(), StringComparison.Ordinal);
        string archive = FileHashes.Sha256(_workspace.File("archive.pdf"));
        string copy = Written(Step(Assert.Single(Commands(hint))));
        Assert.Equal(_workspace.File("archive.encrypted.pdf"), copy, ignoreCase: true);
        AssertNeedsThePassword("pdf", copy);
        Assert.Equal(archive, FileHashes.Sha256(_workspace.File("archive.pdf")));
    }

    /// <summary>
    /// No command of this build encrypts Markdown: the hint says so and names no command.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void PdfConvertedToMarkdown_HintSaysNoCommandEncryptsIt()
    {
        CreateEncryptedPdf(pages: 1);

        CliResult result = Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--out", "plain.md");

        Assert.Equal("No command of this build can encrypt md files.", Hint(result));
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

        string hint = Hint(result);
        Assert.Contains("expected result", hint, StringComparison.Ordinal);
        Assert.EndsWith("To protect it, encrypt it with 'aspose-cli pdf edit' instead of decrypting it.", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("encrypt_document", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--encrypt-env", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("must stay protected", hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pages imported from a PDF with an open password: the hint runs the edit again with
    /// <c>--encrypt-env</c> and <c>--overwrite</c>, so it replaces the file it wrote instead of
    /// failing with <c>OUTPUT_EXISTS</c>; the variable is the caller's to name.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportFromProtectedPdf_HintRunsTheEditAgainOverItsOutput(bool existingOutput)
    {
        CreateProtected();

        CliResult result = Run([
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"import_pages","path":"protected.pdf","pages":"1","at":1,"sourcePasswordEnv":"{{{UserVariable}}}"}]""",
            .. Output("imported.pdf", existingOutput)]);

        string hint = Hint(result);
        Assert.StartsWith("Only the password of the document a command opens carries over to its output, not one of a document it takes content from. "
            + "If the output must stay protected, run `aspose-cli pdf edit ", hint, StringComparison.Ordinal);
        Assert.EndsWith(", which writes the output again encrypted with a password to open it that grants every permission, "
            + "after replacing <password-env> with the name of the environment variable that holds that password.", hint, StringComparison.Ordinal);
        JsonNode written = Step(Assert.Single(Commands(hint)), ("<password-env>", Variable));
        Assert.Equal(Written(result.Json()), Written(written), ignoreCase: true);
        AssertNeedsThePassword("pdf", Written(written));
    }

    /// <summary>
    /// A PDF with only an owner password opens without one and only restricts what may be done:
    /// the hint of a merge, a split and a PDF/A conversion of it encrypts what they wrote with the
    /// edit that sets only an owner password, so it is restricted again and still opens without a
    /// password; the variable is the caller's to name. That edit grants readers nothing, which the
    /// hint says, and it names the inspect that reads what the original granted.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("merge", false)]
    [InlineData("merge", true)]
    [InlineData("split", false)]
    [InlineData("pdfa", false)]
    public void RestrictedPdf_HintEncryptsWithAnOwnerPasswordOnly(string command, bool existingOutput)
    {
        CreateRestricted();

        CliResult result = Run(command switch
        {
            "merge" => ["pdf", "merge", "restricted.pdf", "plain.pdf", .. Output("merged.pdf", existingOutput, required: true)],
            "split" => ["pdf", "split", "restricted.pdf", "--by", "pages", "--out-dir", "parts"],
            _ => ["pdf", "convert", "restricted.pdf", "--to", "pdfa-2b"],
        });

        string hint = Hint(result);
        Assert.Contains("an owner password only: it still opens without a password, but the command grants readers no permission without the owner password, "
            + "after replacing <owner-password-env> with the name of the environment variable that holds the owner password.", hint, StringComparison.Ordinal);
        string[] commands = Commands(hint);
        Assert.Equal(2, commands.Length);
        (string step, string reader) = (commands[0], commands[1]);
        Assert.Contains("\"ownerPasswordEnv\":\"<owner-password-env>\"", step, StringComparison.Ordinal);
        Assert.DoesNotContain("userPasswordEnv", step, StringComparison.Ordinal);
        // After the step and the deletion it ends with, the hint reads what the original grants.
        Assert.Contains("To grant readers what the original allowed, set the permissions field of the operation as 'aspose-cli pdf inspect ", hint, StringComparison.Ordinal);
        Assert.True(hint.IndexOf("To grant readers", StringComparison.Ordinal) > hint.IndexOf(step, StringComparison.Ordinal), hint);
        if (command == "pdfa")
        {
            Assert.True(hint.IndexOf("To grant readers", StringComparison.Ordinal) > hint.IndexOf("delete the unencrypted file", StringComparison.Ordinal), hint);
        }

        JsonNode original = Step(reader, ("\"<original>\"", Quoted(_workspace.File("restricted.pdf"))));
        Assert.Equal(_workspace.File("restricted.pdf"), original["input"]!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.True(original["permissions"]!["print"]!.GetValue<bool>());
        string[] files = command == "split"
            ? [.. result.Json()["outputs"]!.AsArray().Select(static output => output!["path"]!.GetValue<string>())]
            : [Written(result.Json())];
        foreach (string file in files)
        {
            string written = Written(Step(step, ("<owner-password-env>", OwnerVariable), ("\"<file>\"", Quoted(file))));
            JsonNode inspected = Run("pdf", "inspect", written, "--detail", "permissions").Json();
            Assert.True(inspected["input"]!["encrypted"]!.GetValue<bool>(), written);
            Assert.False(inspected["permissions"]!["print"]!.GetValue<bool>(), written);
        }
    }

    /// <summary>
    /// A DOCX copy of a PDF with only an owner password cannot be restricted that way: only a
    /// conversion encrypts DOCX, and with a password to open, which the hint says as it runs the
    /// command again.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void RestrictedPdfToDocx_HintRunsTheConversionAgainWithAPasswordToOpen()
    {
        CreateRestricted();

        CliResult result = Run("words", "convert", "restricted.pdf", "--to", "docx", "--out", "restricted.docx");

        string hint = Hint(result);
        Assert.StartsWith("The input opens without a password, but only a password to open it protects a docx file. "
            + "If the output must stay protected, run 'aspose-cli words convert ", hint, StringComparison.Ordinal);
        Assert.Contains("although the input opens without one", hint, StringComparison.Ordinal);
        JsonNode written = Step(Assert.Single(Commands(hint)), ("<password-env>", Variable));
        AssertNeedsThePassword("words", Written(written));
    }

    /// <summary>
    /// A password read from stdin is never repeated, by value or by stdin, which the command may
    /// need for a document: the hint's command passes it on by a variable placeholder, with no
    /// sentence of its own, and run as written with the placeholder filled, it needs no stdin.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void WorkbookOpenedWithAPasswordOnStdin_HintPassesItOnByAVariablePlaceholder()
    {
        Created(Run("cells", "create", "locked.xlsx", "--encrypt-env", Variable));

        CliResult result = _workspace.RunCommandLine("aspose-cli cells convert locked.xlsx --password-stdin --to csv --output-format json", Variables, "hint-secret\n");

        string hint = Hint(result);
        Assert.DoesNotContain("--password-stdin", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("hint-secret", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("giving it again", hint, StringComparison.Ordinal);
        string command = Assert.Single(Commands(hint));
        Assert.Contains(" --password-env \"<password-env>\" ", command, StringComparison.Ordinal);
        AssertNeedsThePassword("cells", Written(Step(command, ("<password-env>", Variable))));
    }

    /// <summary>
    /// A password given literally is never repeated: the hint's command passes it on by a variable
    /// placeholder, as one read from stdin, and runs as written with the placeholder filled.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void WorkbookOpenedWithALiteralPassword_HintPassesItOnByAVariablePlaceholder()
    {
        Created(Run("cells", "create", "locked.xlsx", "--encrypt-env", Variable));

        CliResult result = Run("cells", "convert", "locked.xlsx", "--password", "hint-secret", "--to", "csv");

        string hint = Hint(result);
        Assert.DoesNotContain("hint-secret", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--password ", hint, StringComparison.Ordinal);
        string command = Assert.Single(Commands(hint));
        Assert.Contains(" --password-env \"<password-env>\" ", command, StringComparison.Ordinal);
        AssertNeedsThePassword("cells", Written(Step(command, ("<password-env>", Variable))));
    }

    /// <summary>
    /// An edit that read its operations from stdin: the hint's command keeps --ops - and says to
    /// pipe the same operations again, never writing them into the hint, where a secret they hold
    /// would show. Run as written with those operations on stdin, it encrypts what it writes.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ImportWithOperationsOnStdin_HintPipesTheSameOperationsAgain()
    {
        CreateProtected();
        string ops = $$$"""[{"op":"import_pages","path":"protected.pdf","pages":"1","at":1,"sourcePasswordEnv":"{{{UserVariable}}}"}]""";

        CliResult result = _workspace.RunCommandLine("aspose-cli pdf edit plain.pdf --ops - --out imported.pdf --output-format json", Variables, ops);

        string hint = Hint(result);
        string command = Assert.Single(Commands(hint));
        Assert.Contains(" --ops - ", command, StringComparison.Ordinal);
        Assert.DoesNotContain("import_pages", hint, StringComparison.Ordinal);
        Assert.Contains("', piping the same --ops document to it on stdin as this command read it, ", hint, StringComparison.Ordinal);
        CliResult ran = _workspace.RunCommandLine(command.Replace("<password-env>", Variable, StringComparison.Ordinal), Variables, ops);
        Assert.True(ran.ExitCode == 0, $"{command}\n{ran.StdErr}");
        Assert.Equal(Written(result.Json()), Written(ran.Json()), ignoreCase: true);
        AssertNeedsThePassword("pdf", Written(ran.Json()));
    }

    /// <summary>
    /// The two hints of a page selection converted to docx each split the pages into a folder of
    /// their own, which each says to delete once converted. Done as they say, one after the other
    /// without deleting the first's folder, both run as written and end with an encrypted docx of
    /// that page.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void PagesOfAPdfConvertedToDocx_BothHintsRunOneAfterTheOther()
    {
        CreateEncryptedPdf(pages: 3);

        CliResult result = Run("pdf", "convert", "locked.pdf", "--password-env", Variable, "--pages", "2", "--to", "docx", "--out", "page.docx");

        Assert.True(result.ExitCode == 0, result.StdErr);
        string editable = result.Json()["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "LOSSY_CONVERSION")!["hint"]!.GetValue<string>();
        Assert.Contains($"; once the conversion has run, delete {Quoted(_workspace.File("page-editable-part"))}, the folder the split created beside the output for the part", editable, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"; once the conversion has run, delete {Quoted(_workspace.File("page-encrypted-part"))}, the folder the split created beside the output for the part", Hint(result), StringComparison.OrdinalIgnoreCase);
        string[] commands = [.. Commands(editable), .. Commands(Hint(result))];
        Assert.Equal(4, commands.Length);
        foreach (string command in commands)
        {
            Step(command);
        }

        AssertNeedsThePassword("words", _workspace.File("page.docx"));
        JsonNode inspected = Run("words", "inspect", "page.docx", "--password-env", Variable).Json();
        Assert.Equal(1, inspected["document"]!["pageCount"]!.GetValue<int>());
    }

    // locked.pdf: a PDF of the given number of pages whose open password the variable holds.
    private void CreateEncryptedPdf(int pages)
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        if (pages == 1)
        {
            Created(Run("pdf", "create", "locked.pdf", "--from-markdown", "source.md", "--encrypt-env", Variable));
            return;
        }

        Created(Run("pdf", "create", "page.pdf", "--from-markdown", "source.md"));
        Created(Run(["pdf", "merge", .. Enumerable.Repeat("page.pdf", pages), "--out", "locked.pdf", "--encrypt-env", Variable]));
    }

    // protected.pdf: an open password, a different owner password, and only printing allowed.
    private void CreateProtected()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n\n# Two\n\nSecond part.\n");
        Created(Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md"));
        Created(Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","userPasswordEnv":"{{{UserVariable}}}","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true}}]""",
            "--out", "protected.pdf"));
    }

    // restricted.pdf: only an owner password, which lets readers print and copy; plain.pdf beside it.
    private void CreateRestricted()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n");
        Created(Run("pdf", "create", "plain.pdf", "--from-markdown", "source.md"));
        Created(Run(
            "pdf", "edit", "plain.pdf", "--ops",
            $$$"""[{"op":"encrypt_document","ownerPasswordEnv":"{{{OwnerVariable}}}","permissions":{"print":true,"copy":true}}]""",
            "--out", "restricted.pdf"));
    }

    // The --out of a command: a new file, or one that already exists with --overwrite; none for
    // the default output path, unless the command requires one.
    private string[] Output(string name, bool existing, bool required = false)
    {
        if (existing)
        {
            File.WriteAllText(_workspace.File(name), "an earlier file");
            return ["--out", name, "--overwrite"];
        }

        return required ? ["--out", name] : [];
    }

    // Runs one command a hint gives, as written but for its placeholders; returns its result.
    private JsonNode Step(string command, params (string Placeholder, string Value)[] filled)
    {
        string line = filled.Aggregate(command, static (text, fill) => text.Replace(fill.Placeholder, fill.Value, StringComparison.Ordinal));
        CliResult ran = _workspace.RunCommandLine(line, Variables);
        Assert.True(ran.ExitCode == 0, $"{line}\n{ran.StdErr}");
        return JsonNode.Parse(ran.StdOut)!;
    }

    // A file a protected format holds: it needs the password the variable holds to open.
    private void AssertNeedsThePassword(string product, string file)
    {
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run(product, "inspect", file).StdErr)!["error"]!["code"]!.GetValue<string>());
        CliResult opened = Run(product, "inspect", file, "--password-env", Variable);
        Assert.True(opened.ExitCode == 0, opened.StdErr);
    }

    // The commands a hint quotes to run, in order: in backticks, or in single quotes when they hold none.
    private static string[] Commands(string hint) =>
        [.. QuotedCommand().Matches(hint).Select(static match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)];

    // The one file a result names as written.
    private static string Written(JsonNode result) =>
        (result["output"] ?? result["outputs"]![0])!["path"]!.GetValue<string>();

    private static string[] Codes(JsonNode result) =>
        [.. (result["warnings"]?.AsArray() ?? []).Select(static warning => warning!["code"]!.GetValue<string>())];

    // A path as a command line of this CLI quotes it.
    private static string Quoted(string path) => $"\"{path}\"";

    private static void Created(CliResult created) => Assert.True(created.ExitCode == 0, created.StdErr);

    private static JsonNode Warning(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!["warnings"]!.AsArray()
            .Single(static warning => warning!["code"]!.GetValue<string>() == "ENCRYPTION_REMOVED")!;
    }

    private static string Hint(CliResult result) => Warning(result)["hint"]!.GetValue<string>();

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);

    [GeneratedRegex(@"`(aspose-cli [^`]+)`|(?<![\p{L}\p{N}])'(aspose-cli [^'`]+)'(?![\p{L}\p{N}])")]
    private static partial Regex QuotedCommand();
}
