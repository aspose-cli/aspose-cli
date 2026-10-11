using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What becomes of an encrypted presentation's encryption when it is converted, through the
/// built CLI: a format that can carry a password keeps it, and any other is written without
/// one, opens without a password and carries <c>ENCRYPTION_REMOVED</c>.
/// </summary>
public sealed class SlidesOutputEncryptionTests : IDisposable
{
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal) { ["SLIDES_PASSWORD"] = "slides-secret" };
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
    public void ConvertToAFormatWithPasswords_KeepsTheEncryption()
    {
        JsonNode result = Succeeds(Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "pptx", "--out", "copy.pptx"));

        Assert.True(result["outputs"]![0]!["encrypted"]!.GetValue<bool>());
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
        Assert.Equal("PASSWORD_REQUIRED", JsonNode.Parse(Run("slides", "inspect", "copy.pptx").StdErr)!["error"]!["code"]!.GetValue<string>());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToImages_DisclosesNothing()
    {
        JsonNode result = Succeeds(Run("slides", "convert", "locked.pptx", "--password-env", "SLIDES_PASSWORD", "--to", "png", "--out", "slide.png"));

        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
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
