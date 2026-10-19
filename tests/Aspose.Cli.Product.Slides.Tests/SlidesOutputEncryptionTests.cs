using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What becomes of an encrypted presentation's encryption when it is converted or edited, through
/// the built CLI: <c>--encrypt-env</c> encrypts with its password; without it a PPTX or PPTM
/// output keeps the input's password, and any other format is written without one, opens
/// without a password and carries <c>ENCRYPTION_REMOVED</c>.
/// </summary>
public sealed class SlidesOutputEncryptionTests : IDisposable
{
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal)
    {
        ["SLIDES_PASSWORD"] = "slides-secret",
        ["SLIDES_NEW_PASSWORD"] = "slides-renewed",
    };
    private readonly TempWorkspace _workspace = new();

    public SlidesOutputEncryptionTests()
    {
        CliResult locked = Run("slides", "create", "locked.pptx", "--encrypt-env", "SLIDES_PASSWORD");
        Assert.True(locked.ExitCode == 0, locked.StdErr);
    }

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithoutPasswords_WritesAnOpenFileAndDisclosesIt()
    {
        JsonNode result = Succeeds(Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "odp", "--out", "locked.odp"));

        Assert.Contains("ENCRYPTION_REMOVED", Codes(result));
        JsonNode reopened = Succeeds(Run("slides", "inspect", "locked.odp"));
        Assert.False(reopened["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertSelectedSlidesToAFormatWithoutPasswords_WritesAnOpenFileAndDisclosesIt()
    {
        JsonNode result = Succeeds(Run(
            "slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "odp", "--slides", "1", "--out", "selected.odp"));

        Assert.Contains("ENCRYPTION_REMOVED", Codes(result));
        Assert.False(Succeeds(Run("slides", "inspect", "selected.odp"))["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithPasswords_WithoutEncrypt_KeepsTheSourcePassword()
    {
        JsonNode result = Succeeds(Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "pptx", "--out", "copy.pptx"));

        Assert.True(result["outputs"]![0]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "inspect", "copy.pptx").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(Succeeds(Run("slides", "inspect", "copy.pptx", "--password-env", "SLIDES_PASSWORD"))["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithPasswords_WithEncrypt_EncryptsWithTheNewPassword()
    {
        JsonNode result = Succeeds(Run(
            "slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "pptx", "--out", "copy.pptx",
            "--encrypt-env", "SLIDES_NEW_PASSWORD"));

        Assert.True(result["outputs"]![0]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        Assert.Equal("PASSWORD_INVALID", JsonNode.Parse(Run("slides", "inspect", "copy.pptx", "--password-env", "SLIDES_PASSWORD").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(Succeeds(Run("slides", "inspect", "copy.pptx", "--password-env", "SLIDES_NEW_PASSWORD"))["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EditWithoutEncrypt_KeepsTheInputPassword()
    {
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_title","slide":1,"text":"Locked"}]}""");

        JsonNode result = Succeeds(Run("slides", "edit", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--ops", "ops.json", "--out", "edited.pptx"));

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "inspect", "edited.pptx").StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// An encrypted PPT opens to be written through a PPTX round trip; the PPTX an edit writes
    /// from it still opens only with the input's password, as the result reports.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void EditOfAnEncryptedPpt_KeepsTheInputPassword()
    {
        using (var source = new Aspose.Slides.Presentation())
        {
            source.ProtectionManager.Encrypt(Variables["SLIDES_PASSWORD"]!);
            source.Save(_workspace.File("locked.ppt"), Aspose.Slides.Export.SaveFormat.Ppt);
        }

        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_title","slide":1,"text":"Locked"}]}""");
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "inspect", "locked.ppt").StdErr)!["error"]!["code"]!.GetValue<string>());

        JsonNode result = Succeeds(Run("slides", "edit", "locked.ppt", "--password-env", "SLIDES_PASSWORD", "--ops", "ops.json", "--out", "edited.pptx"));

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "inspect", "edited.pptx").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(Succeeds(Run("slides", "inspect", "edited.pptx", "--password-env", "SLIDES_PASSWORD"))["input"]!["encrypted"]!.GetValue<bool>());
    }

    /// <summary>
    /// An edit keeps the input's encryption in the presentation it saves, which is right only
    /// for a format that carries a password: every format edit writes is one, and an edit to
    /// any other presentation or export format is refused before anything is written.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void EditWritesOnlyFormatsThatCarryAPassword()
    {
        Assert.All(SlidesFormats.Writable, static format => Assert.True(format.Protectable, format.Id));
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_title","slide":1,"text":"Locked"}]}""");

        foreach (FormatDescriptor format in SlidesFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert) && !format.Protectable))
        {
            string output = "edited" + format.Extensions[0];
            CliResult result = Run("slides", "edit", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--ops", "ops.json", "--out", output);

            Assert.True(result.ExitCode != 0, $"{format.Id}: {result.StdOut}");
            Assert.False(File.Exists(_workspace.File(output)), format.Id);
        }
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EditWithEncrypt_EncryptsWithTheNewPassword()
    {
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_title","slide":1,"text":"Relocked"}]}""");

        JsonNode result = Succeeds(Run(
            "slides", "edit", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--ops", "ops.json", "--out", "edited.pptx",
            "--encrypt-env", "SLIDES_NEW_PASSWORD"));

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.True(Succeeds(Run("slides", "inspect", "edited.pptx", "--password-env", "SLIDES_NEW_PASSWORD"))["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ImportSlides_OpensAnEncryptedSourceWithTheVariableItNames()
    {
        Succeeds(Run("slides", "create", "base.pptx"));
        File.WriteAllText(_workspace.File("with.json"), """{"ops":[{"op":"import_slides","path":"locked.pptx","sourcePasswordEnv":"SLIDES_PASSWORD"}]}""");
        File.WriteAllText(_workspace.File("without.json"), """{"ops":[{"op":"import_slides","path":"locked.pptx"}]}""");

        JsonNode imported = Succeeds(Run("slides", "edit", "base.pptx", "--ops", "with.json", "--out", "with.pptx"));
        CliResult refused = Run("slides", "edit", "base.pptx", "--ops", "without.json", "--out", "without.pptx");

        Assert.Equal(1, imported["applied"]![0]!["itemsAffected"]!.GetValue<long>());
        Assert.Equal(2, Succeeds(Run("slides", "inspect", "with.pptx"))["presentation"]!["slideCount"]!.GetValue<int>());
        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// An encrypted source an <c>import_slides</c> names without its variable is refused with a
    /// hint that names the operation's own password field, not a command option.
    /// </summary>
    [Fact]
    public void ImportSlides_WithoutItsVariable_HintNamesSourcePasswordEnv()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("base.pptx", slides: 1);
        string source = fixture.CreatePresentation("locked-source.pptx", slides: 1, password: "deck-secret");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new ImportSlidesOp { Path = source }] },
            Output = TestOutput.At(fixture.File("imported.pptx")),
        }));

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.Contains("\"sourcePasswordEnv\"", error.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("--password-env", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// The in-process session the tests share is wired as activation wires it: its loader tells
    /// the write pipeline that the input is encrypted, so the result discloses the removal.
    /// </summary>
    [Fact]
    public void InProcessConvertOfAnEncryptedInput_DisclosesTheRemovedEncryption()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("locked.pptx", slides: 1, password: "deck-secret");

        SlidesConvertResult result = fixture.Disclosed(session => SlidesExport.Convert(session, new SlidesConvertRequest
        {
            Input = input,
            Password = new Secret("deck-secret"),
            Output = TestOutput.At(fixture.File("locked.odp"), format: "odp"),
        }));

        Assert.Contains(result.Warnings ?? [], static warning => warning.Code == WarningCodes.EncryptionRemoved);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void RenderToImages_DisclosesNothing()
    {
        JsonNode result = Succeeds(Run("slides", "render", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "png", "--out", "slide.png"));

        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        // Each image names its slide, and a PNG reports no encryption.
        JsonArray outputs = result["outputs"]!.AsArray();
        Assert.Equal(Enumerable.Range(1, outputs.Count), outputs.Select(static output => output!["slide"]!.GetValue<int>()));
        Assert.All(outputs, output => Assert.Null(output!["encrypted"]));
    }

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);

    private static JsonNode Succeeds(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        return JsonNode.Parse(result.StdOut)!;
    }

    private static string[] Codes(JsonNode result) =>
        [.. result["warnings"]?.AsArray().Select(static warning => warning!["code"]!.GetValue<string>()) ?? []];
}
