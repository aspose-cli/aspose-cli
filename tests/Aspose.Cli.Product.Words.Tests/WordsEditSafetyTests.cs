using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Loading;
using Aspose.Words.Saving;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsEditSafetyTests
{
    private const string OriginalPassword = "synthetic-input-secret";
    private const string ReplacementPassword = "synthetic-output-secret";

    [Fact]
    public void Loader_ReadsAnInputThatAnInPlaceEditIsReplacing()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();

        // Another command's in-place edit holds the input open with delete access while it
        // replaces the file.
        using (new FileStream(input, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.DeleteOnClose))
        {
            Assert.Equal(0, fixture.Engine.GetInfo(input, new DocumentInfoRequest()).Document.CommentCount);
        }
    }

    [Fact]
    public void UpdateFields_NamesTheInputFileInAFileNameField()
    {
        using var fixture = new WordsFixture();
        string input = fixture.Temp.File("quarterly.docx");
        string output = fixture.Temp.File("updated.docx");
        var document = new Document();
        new DocumentBuilder(document).InsertField("FILENAME", "stale");
        document.Save(input);

        fixture.Engine.ApplyOps(input, new WordsOpsBatch { Ops = [new UpdateFieldsOp()] },
            new WordsEditRequest { Output = TestOutput.At(output) });

        var updated = new Document(output);
        Assert.Equal("quarterly.docx", Assert.Single(updated.Range.Fields.Cast<Aspose.Words.Fields.Field>(),
            static field => field.Type == Aspose.Words.Fields.FieldType.FieldFileName).Result);
    }

    [Theory]
    [InlineData("docx", false, false)]
    [InlineData("docx", true, true)]
    [InlineData("doc", false, false)]
    [InlineData("odt", false, false)]
    public void EncryptedEdits_PreserveEncryptionAndUseTheSamePasswordForReopen(string format, bool inPlace, bool verify)
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateEncryptedDocument(OriginalPassword);
        byte[] original = File.ReadAllBytes(input);
        string output = inPlace ? input : fixture.Temp.File($"edited.{format}");
        string backup = fixture.Temp.File("original.backup.docx");
        WordsEditResult result = fixture.Engine.ApplyOps(input, TextBatch(), new WordsEditRequest
        {
            Output = TestOutput.At(output, overwrite: inPlace, backup: inPlace ? backup : null),
            Password = OriginalPassword,
            Verify = verify,
        });

        Assert.True(FileFormatUtil.DetectFileFormat(output).IsEncrypted);
        Assert.Equal(ErrorCodes.PasswordRequired, Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(output, new DocumentInfoRequest())).Code);
        Assert.Equal(ErrorCodes.PasswordInvalid, Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(output, new DocumentInfoRequest { Password = "wrong" })).Code);
        var reopened = new Document(output, new LoadOptions { Password = OriginalPassword });
        Assert.Contains("Safe", reopened.GetText(), StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(inPlace ? backup : input));
        Assert.DoesNotContain(result.Warnings ?? [], warning => warning.Code == WordsDiagnostics.EncryptionRemoved);
        if (verify) { Assert.NotNull(result.Verification); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitEncryption_RotatesThePassword(bool verify)
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateEncryptedDocument(OriginalPassword);
        string output = fixture.Temp.File("rotated.docx");
        fixture.Engine.ApplyOps(input, TextBatch(), new WordsEditRequest
        {
            Output = TestOutput.At(output),
            Password = OriginalPassword,
            EncryptPassword = ReplacementPassword,
            Verify = verify,
        });
        Assert.Equal(ErrorCodes.PasswordInvalid, Assert.Throws<CliException>(() =>
            fixture.Engine.GetInfo(output, new DocumentInfoRequest { Password = OriginalPassword })).Code);
        Assert.True(fixture.Engine.GetInfo(output, new DocumentInfoRequest { Password = ReplacementPassword }).Document.BlockCount > 0);
    }

    [Fact]
    public void PlaintextInput_WithAnUnusedReadPassword_RemainsPlaintext()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string output = fixture.Temp.File("plain.docx");
        fixture.Engine.ApplyOps(input, TextBatch(), new WordsEditRequest { Output = TestOutput.At(output), Password = OriginalPassword });
        Assert.False(FileFormatUtil.DetectFileFormat(output).IsEncrypted);
    }

    [Fact]
    public void ProtectionOperations_UseTheSecretOfTheVariableTheyName()
    {
        using var fixture = new WordsFixture();
        string protectedPath = fixture.Temp.File("protected.docx");
        string output = fixture.Temp.File("unprotected.docx");
        var secrets = new Dictionary<string, string>
        {
            ["PROTECT_PASSWORD"] = OriginalPassword,
            ["OTHER_PASSWORD"] = ReplacementPassword,
        };
        fixture.Engine.ApplyOps(
            fixture.CreateReport(),
            new WordsOpsBatch { Ops = [new ProtectOp { Mode = "readOnly", PasswordEnv = "PROTECT_PASSWORD" }] },
            new WordsEditRequest { Output = TestOutput.At(protectedPath), OpSecrets = secrets });
        Assert.Equal("readOnly", fixture.Engine.GetInfo(protectedPath, new DocumentInfoRequest()).Document.Protection);

        CliException wrong = Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(
            protectedPath, Unprotect("OTHER_PASSWORD"),
            new WordsEditRequest { Output = TestOutput.At(fixture.Temp.File("wrong.docx")), OpSecrets = secrets }));
        Assert.Equal(WordsDiagnostics.DocumentProtected, wrong.Code);
        fixture.Engine.ApplyOps(
            protectedPath, Unprotect("PROTECT_PASSWORD"),
            new WordsEditRequest { Output = TestOutput.At(output), OpSecrets = secrets });
        Assert.Equal(ProtectionType.NoProtection, new Document(output).ProtectionType);
        Assert.Equal("none", fixture.Engine.GetInfo(output, new DocumentInfoRequest()).Document.Protection);

        static WordsOpsBatch Unprotect(string variable) =>
            new() { Ops = [new UnprotectOp { PasswordEnv = variable }] };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonEncryptableOutput_DisclosesRemovalOnlyWhenPublished(bool dryRun)
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateEncryptedDocument(OriginalPassword);
        byte[] original = File.ReadAllBytes(input);
        string output = fixture.Temp.File("edited.txt");
        WordsEditResult result = fixture.Engine.ApplyOps(input, TextBatch(), new WordsEditRequest
        {
            Output = TestOutput.At(output),
            Password = OriginalPassword,
            Options = new EditCommandOptions { DryRun = dryRun },
        });
        Assert.Equal(!dryRun, File.Exists(output));
        Assert.Equal(!dryRun, result.Warnings?.Any(warning => warning.Code == WordsDiagnostics.EncryptionRemoved) == true);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.Throws<CliException>(() => fixture.Engine.ApplyOps(input, TextBatch(), new WordsEditRequest
        {
            Output = TestOutput.At(fixture.Temp.File("invalid.txt")),
            Password = OriginalPassword,
            EncryptPassword = ReplacementPassword,
            Options = new EditCommandOptions { DryRun = dryRun },
        }));
        Assert.False(File.Exists(fixture.Temp.File("invalid.txt")));
    }

    [Fact]
    public void EncryptedDryRun_CreatesNeitherOutputNorBackup()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateEncryptedDocument(OriginalPassword);
        byte[] original = File.ReadAllBytes(input);
        string backup = fixture.Temp.File("unused.backup.docx");
        WordsEditResult result = fixture.Engine.ApplyOps(input, TextBatch(), new WordsEditRequest
        {
            Output = TestOutput.At(input, overwrite: true, backup: backup),
            Password = OriginalPassword,
            Options = new EditCommandOptions { DryRun = true },
        });
        Assert.True(result.DryRun);
        Assert.Null(result.Output);
        Assert.Equal(original, File.ReadAllBytes(input));
        Assert.False(File.Exists(backup));
    }

    [Fact]
    public void CliEncryptedInPlaceEdit_PreservesThePasswordAndDoesNotExposeSecrets()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("source.txt"), "Seed");
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"op":"set_text","at":{"block":1},"text":"Safe"}]}""");
        var environment = new Dictionary<string, string?> { ["WORDS_EDIT_SECRET"] = OriginalPassword };
        CliResult created = workspace.RunWithEnv(environment, "words", "create", "secret.docx", "--text", "source.txt", "--encrypt-env", "WORDS_EDIT_SECRET", "--output", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        byte[] original = File.ReadAllBytes(workspace.File("secret.docx"));
        CliResult edited = workspace.RunWithEnv(environment, "words", "edit", "secret.docx", "--ops", "ops.json", "--password-env", "WORDS_EDIT_SECRET", "--in-place", "--backup", "--verify", "--output", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.True(FileFormatUtil.DetectFileFormat(workspace.File("secret.docx")).IsEncrypted);
        Assert.Equal(original, File.ReadAllBytes(workspace.File("secret.backup.docx")));
        Assert.NotNull(JsonNode.Parse(edited.StdOut)!["verification"]);
        Assert.DoesNotContain(OriginalPassword, created.StdOut + created.StdErr + edited.StdOut + edited.StdErr, StringComparison.Ordinal);
        CliResult missing = workspace.Run("words", "inspect", "secret.docx", "--output", "json");
        Assert.NotEqual(0, missing.ExitCode);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(missing.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    private static WordsOpsBatch TextBatch() => new()
    {
        Ops = [new SetTextOp { At = new WordsTarget { Block = 1 }, Text = "Safe" }],
    };
}
