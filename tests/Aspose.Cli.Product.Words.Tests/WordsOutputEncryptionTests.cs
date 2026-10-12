using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// What becomes of an encrypted document's encryption in the files Words commands write,
/// through the built CLI: a converted copy, the parts of a split and a comparison's redline keep
/// the password in a format that can carry one, and every output that does not keep it carries
/// <c>ENCRYPTION_REMOVED</c>.
/// </summary>
public sealed class WordsOutputEncryptionTests : IDisposable
{
    private const string Password = "words-secret";
    private static readonly Dictionary<string, string?> Variables = new(StringComparer.Ordinal) { ["WORDS_PASSWORD"] = Password };
    private readonly TempWorkspace _workspace = new();

    public WordsOutputEncryptionTests()
    {
        File.WriteAllText(_workspace.File("source.md"), "# One\n\nFirst part.\n\n# Two\n\nSecond part.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "plain.docx", "--from-markdown", "source.md").ExitCode);
        CliResult locked = Run("words", "create", "locked.docx", "--from-markdown", "source.md", "--encrypt-env", "WORDS_PASSWORD");
        Assert.True(locked.ExitCode == 0, locked.StdErr);
    }

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithoutPasswords_DisclosesTheRemovedEncryption()
    {
        JsonNode result = Succeeds(Run("words", "convert", "locked.docx", "--password-env", "WORDS_PASSWORD", "--to", "pdf", "--out", "locked.pdf"));

        Assert.Contains("ENCRYPTION_REMOVED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertToAFormatWithPasswords_KeepsTheEncryption()
    {
        JsonNode result = Succeeds(Run("words", "convert", "locked.docx", "--password-env", "WORDS_PASSWORD", "--to", "odt", "--out", "locked.odt"));

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.True(FileFormatUtil.DetectFileFormat(_workspace.File("locked.odt")).IsEncrypted);
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitOfAnEncryptedDocument_KeepsThePasswordInEveryPart()
    {
        JsonNode result = Succeeds(Run("words", "split", "locked.docx", "--password-env", "WORDS_PASSWORD", "--by", "heading1", "--out-dir", "parts"));

        string[] parts = Directory.GetFiles(_workspace.File("parts"));
        Assert.NotEmpty(parts);
        Assert.All(parts, part => Assert.True(FileFormatUtil.DetectFileFormat(part).IsEncrypted));
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void CompareRedline_KeepsTheOriginalsPassword()
    {
        JsonNode result = Succeeds(Run("words", "compare", "locked.docx", "plain.docx", "--left-password-env", "WORDS_PASSWORD", "--out", "redline.docx"));

        Assert.True(result["output"]!["encrypted"]!.GetValue<bool>());
        Assert.True(FileFormatUtil.DetectFileFormat(_workspace.File("redline.docx")).IsEncrypted);
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void CompareRedlineOfAnEncryptedChangedDocument_DisclosesTheRemovedEncryption()
    {
        JsonNode result = Succeeds(Run("words", "compare", "plain.docx", "locked.docx", "--right-password-env", "WORDS_PASSWORD", "--out", "redline.docx"));

        Assert.False(FileFormatUtil.DetectFileFormat(_workspace.File("redline.docx")).IsEncrypted);
        Assert.Contains("ENCRYPTION_REMOVED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void CompareWithoutAnOutput_DisclosesNothing()
    {
        JsonNode result = Succeeds(Run("words", "compare", "locked.docx", "plain.docx", "--left-password-env", "WORDS_PASSWORD"));

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
