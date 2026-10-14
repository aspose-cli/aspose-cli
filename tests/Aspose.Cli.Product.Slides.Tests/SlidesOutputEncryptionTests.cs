using System.Text.Json.Nodes;
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

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToImages_DisclosesNothing()
    {
        JsonNode result = Succeeds(Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "png", "--out", "slide.png"));

        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        // Each image names its slide, as a render's does, and a PNG reports no encryption.
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
