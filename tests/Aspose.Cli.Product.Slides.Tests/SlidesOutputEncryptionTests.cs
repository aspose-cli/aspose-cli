using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What becomes of an encrypted presentation's encryption when it is converted or edited, through
/// the built CLI: <c>--encrypt-env</c> encrypts with its password; without it a PPTX or PPTM
/// output keeps the input's password, and any other format is written without one, opens
/// without a password and carries <c>ENCRYPTION_REMOVED</c>. An encrypted presentation an
/// operation reads opens with the password its variable holds.
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
        JsonNode result = Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "odp", "--out", "locked.odp").Json();

        Assert.Contains("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
        JsonNode reopened = Run("slides", "read", "locked.odp", "--kind", "presentation").Json();
        Assert.False(reopened["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertSelectedSlidesToAFormatWithoutPasswords_WritesAnOpenFileAndDisclosesIt()
    {
        JsonNode result = Run(
            "slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "odp", "--slides", "1", "--out", "selected.odp").Json();

        Assert.Contains("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
        Assert.False(Run("slides", "read", "selected.odp", "--kind", "presentation").Json()["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithPasswords_WithoutEncrypt_KeepsTheSourcePassword()
    {
        JsonNode result = Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "pptx", "--out", "copy.pptx").Json();

        Assert.True(result["outputs"]![0]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "read", "copy.pptx", "--kind", "presentation").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(Run("slides", "read", "copy.pptx", "--kind", "presentation", "--password-env", "SLIDES_PASSWORD").Json()["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithPasswords_WithEncrypt_EncryptsWithTheNewPassword()
    {
        JsonNode result = Run(
            "slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "pptx", "--out", "copy.pptx",
            "--encrypt-env", "SLIDES_NEW_PASSWORD").Json();

        Assert.True(result["outputs"]![0]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
        Assert.Equal("PASSWORD_INVALID", JsonNode.Parse(Run("slides", "read", "copy.pptx", "--kind", "presentation", "--password-env", "SLIDES_PASSWORD").StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.True(Run("slides", "read", "copy.pptx", "--kind", "presentation", "--password-env", "SLIDES_NEW_PASSWORD").Json()["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EditWithoutEncrypt_KeepsTheInputPassword()
    {
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"set":"presentation","props":{"title":"Locked"}}]}""");

        JsonNode result = Run("slides", "edit", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--ops", "ops.json", "--out", "edited.pptx").Json();

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "read", "edited.pptx", "--kind", "presentation").StdErr)!["error"]!["code"]!.GetValue<string>());
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
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"set":"presentation","props":{"title":"Locked"}}]}""");

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
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"set":"presentation","props":{"title":"Again"}}]}""");

        JsonNode result = Run(
            "slides", "edit", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--ops", "ops.json", "--out", "edited.pptx",
            "--encrypt-env", "SLIDES_NEW_PASSWORD").Json();

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.True(Run("slides", "read", "edited.pptx", "--kind", "presentation", "--password-env", "SLIDES_NEW_PASSWORD").Json()["input"]!["encrypted"]!.GetValue<bool>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ImportSlide_OpensAnEncryptedSourceWithTheVariableItNames()
    {
        Run("slides", "create", "base.pptx").Json();
        uint source;
        using (var locked = new Aspose.Slides.Presentation(_workspace.File("locked.pptx"), new Aspose.Slides.LoadOptions { Password = Variables["SLIDES_PASSWORD"] }))
        {
            source = locked.Slides[0].SlideId;
        }

        File.WriteAllText(_workspace.File("with.json"), $$$"""{"ops":[{"do":"import_slide","args":{"file":"locked.pptx","sourceSlide":{{{source}}},"passwordEnv":"SLIDES_PASSWORD"}}]}""");
        File.WriteAllText(_workspace.File("without.json"), $$$"""{"ops":[{"do":"import_slide","args":{"file":"locked.pptx","sourceSlide":{{{source}}}}}]}""");

        JsonNode imported = Run("slides", "edit", "base.pptx", "--ops", "with.json", "--out", "with.pptx").Json();
        CliResult refused = Run("slides", "edit", "base.pptx", "--ops", "without.json", "--out", "without.pptx");

        Assert.Equal("import_slide", imported["ops"]![0]!["do"]!.GetValue<string>());
        Assert.True(imported["ops"]![0]!["exists"]!.GetValue<bool>());
        Assert.Equal(2, Run("slides", "read", "with.pptx", "--kind", "slide").Json()["objects"]!.AsArray().Count);
        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(refused.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("without.pptx")));
    }

    /// <summary>
    /// An encrypted source an <c>import_slide</c> names without its variable is refused with
    /// <c>PASSWORD_REQUIRED</c>.
    /// </summary>
    [Fact]
    public void ImportSlide_WithoutItsVariable_IsPasswordRequired()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("base.pptx", slides: 1);
        string source = fixture.CreatePresentation("locked-source.pptx", slides: 1, password: "deck-secret");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = TestBatch.Parse([new JsonObject
            {
                ["do"] = "import_slide",
                ["args"] = new JsonObject { ["file"] = source, ["sourceSlide"] = 256 },
            }]),
            Output = new EditOutput(fixture.File("imported.pptx")),
        }));

        Assert.Equal(ErrorCodes.PasswordRequired, error.Code);
        Assert.False(File.Exists(fixture.File("imported.pptx")));
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
        JsonNode result = Run("slides", "render", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "png", "--out", "slide.png").Json();

        Assert.DoesNotContain("ENCRYPTION_REMOVED", ResultWarnings.Codes(result));
        // Each image names its slide, and a PNG reports no encryption.
        JsonArray outputs = result["outputs"]!.AsArray();
        Assert.Equal(Enumerable.Range(1, outputs.Count), outputs.Select(static output => output!["slide"]!.GetValue<int>()));
        Assert.All(outputs, output => Assert.Null(output!["encrypted"]));
    }

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);
}
