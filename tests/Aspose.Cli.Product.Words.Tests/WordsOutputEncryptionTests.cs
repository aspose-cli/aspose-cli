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

        Assert.True(result["outputs"]![0]!["encrypted"]!.GetValue<bool>());
        Assert.True(FileFormatUtil.DetectFileFormat(_workspace.File("locked.odt")).IsEncrypted);
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
    }

    /// <summary>X-03: split encrypts every part with --encrypt-env, as pdf split does.</summary>
    [Fact]
    public void SplitWithEncryptEnv_EncryptsEveryPart()
    {
        JsonNode result = Succeeds(Run("words", "split", "plain.docx", "--by", "heading1", "--out-dir", "sealed", "--encrypt-env", "WORDS_PASSWORD"));

        string[] parts = Directory.GetFiles(_workspace.File("sealed"));
        Assert.NotEmpty(parts);
        Assert.All(parts, part => Assert.True(FileFormatUtil.DetectFileFormat(part).IsEncrypted));
        Assert.All(parts, part => Assert.NotNull(new Document(part, new Aspose.Words.Loading.LoadOptions { Password = Password })));
        Assert.All(PartsEncrypted(result), Assert.True);
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitOfAnEncryptedDocument_KeepsThePasswordInEveryPart()
    {
        JsonNode result = Succeeds(Run("words", "split", "locked.docx", "--password-env", "WORDS_PASSWORD", "--by", "heading1", "--out-dir", "parts"));

        string[] parts = Directory.GetFiles(_workspace.File("parts"));
        Assert.NotEmpty(parts);
        Assert.All(parts, part => Assert.True(FileFormatUtil.DetectFileFormat(part).IsEncrypted));
        Assert.Equal([true, true], PartsEncrypted(result));
        Assert.DoesNotContain("ENCRYPTION_REMOVED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void SplitOfAPlainDocument_ReportsEveryPartUnencrypted()
    {
        JsonNode result = Succeeds(Run("words", "split", "plain.docx", "--by", "heading1", "--out-dir", "parts"));

        Assert.All(Directory.GetFiles(_workspace.File("parts")), part => Assert.False(FileFormatUtil.DetectFileFormat(part).IsEncrypted));
        Assert.Equal([false, false], PartsEncrypted(result));
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

    /// <summary>
    /// PDF-DOCX-EMBED-FONTS: a docx that asks every save to embed its fonts but embeds none, as
    /// Aspose.PDF writes it, grows by megabytes when Words saves it, which the result discloses.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertOfADocxAskingToEmbedFonts_DisclosesTheEmbeddedFonts()
    {
        AskToEmbedFonts("plain.docx", "embedding.docx");

        JsonNode result = Succeeds(Run("words", "convert", "embedding.docx", "--to", "docx", "--out", "sealed.docx", "--encrypt-env", "WORDS_PASSWORD"));

        JsonNode warning = Assert.Single(result["warnings"]?.AsArray() ?? [], static warning => warning!["code"]!.GetValue<string>() == "FONTS_EMBEDDED")!;
        Assert.Contains("larger", warning["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(warning["hint"]?.GetValue<string>()));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void EditOfADocxAskingToEmbedFonts_DisclosesTheEmbeddedFonts()
    {
        AskToEmbedFonts("plain.docx", "embedding.docx");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_text","target":{"block":1},"text":"Edited"}]}""");

        JsonNode result = Succeeds(Run("words", "edit", "embedding.docx", "--ops", "ops.json", "--out", "edited.docx"));

        Assert.Contains("FONTS_EMBEDDED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void CreateFromATemplateAskingToEmbedFonts_DisclosesTheEmbeddedFonts()
    {
        AskToEmbedFonts("plain.docx", "embedding.docx");

        JsonNode result = Succeeds(Run("words", "create", "created.docx", "--template", "embedding.docx", "--from-markdown", "source.md"));

        Assert.Contains("FONTS_EMBEDDED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void CompareRedlineOfAnOriginalAskingToEmbedFonts_DisclosesTheEmbeddedFonts()
    {
        AskToEmbedFonts("plain.docx", "embedding.docx");
        File.WriteAllText(_workspace.File("changed.md"), "# One\n\nFirst part changed.\n\n# Two\n\nSecond part.\n");
        Assert.Equal(0, _workspace.Run("words", "create", "changed.docx", "--from-markdown", "changed.md").ExitCode);

        JsonNode result = Succeeds(Run("words", "compare", "embedding.docx", "changed.docx", "--out", "redline.docx"));

        Assert.Contains("FONTS_EMBEDDED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertOfAPlainDocx_DisclosesNoEmbeddedFonts()
    {
        JsonNode result = Succeeds(Run("words", "convert", "plain.docx", "--to", "docx", "--out", "sealed.docx", "--encrypt-env", "WORDS_PASSWORD"));

        Assert.DoesNotContain("FONTS_EMBEDDED", Codes(result));
    }

    /// <summary>
    /// WRD-15: a docx from 'pdf convert' can embed the font of some text, such as a CJK subset,
    /// and not the others; a Words save still embeds the rest, which every writing command discloses.
    /// </summary>
    [Category(TestCategory.Slow)]
    [Theory]
    [InlineData("convert", "--to", "docx", "--out", "converted.docx")]
    [InlineData("split", "--by", "heading1", "--out-dir", "parts")]
    [InlineData("edit", "--ops", "ops.json", "--out", "edited.docx")]
    public void SaveOfADocxEmbeddingSomeOfItsFonts_DisclosesTheEmbeddedFonts(string command, params string[] options)
    {
        CreateWithEmbeddedFonts("embedded.docx");
        DropTheEmbeddedFont("embedded.docx", "partly.docx", "Times New Roman");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"op":"set_text","target":{"block":2},"text":"Edited"}]}""");

        JsonNode result = Succeeds(Run(["words", command, "partly.docx", .. options]));

        Assert.Contains("FONTS_EMBEDDED", Codes(result));
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void ConvertOfADocxEmbeddingEveryFontItUses_DisclosesNoEmbeddedFonts()
    {
        CreateWithEmbeddedFonts("embedded.docx");

        JsonNode result = Succeeds(Run("words", "convert", "embedded.docx", "--to", "docx", "--out", "converted.docx"));

        Assert.DoesNotContain("FONTS_EMBEDDED", Codes(result));
    }

    /// <summary>Copies a docx and adds the two font-embedding flags Aspose.PDF writes, without embedding a font.</summary>
    private void AskToEmbedFonts(string source, string target)
    {
        File.Copy(_workspace.File(source), _workspace.File(target));
        RewritePart(target, "word/settings.xml", static settings =>
        {
            int open = settings.IndexOf("<w:settings", StringComparison.Ordinal);
            return settings.Insert(settings.IndexOf('>', open) + 1, "<w:embedTrueTypeFonts/><w:embedSystemFonts/>");
        });

        var document = new Document(_workspace.File(target));
        Assert.True(document.FontInfos.EmbedTrueTypeFonts && document.FontInfos.EmbedSystemFonts);
    }

    /// <summary>A two-chapter docx in Arial headings and Times New Roman text that asks to embed its fonts and embeds both.</summary>
    private void CreateWithEmbeddedFonts(string target)
    {
        var document = new Document();
        var builder = new DocumentBuilder(document);
        foreach (string chapter in new[] { "One", "Two" })
        {
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
            builder.Font.Name = "Arial";
            builder.Writeln(chapter);
            builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
            builder.Font.Name = "Times New Roman";
            builder.Writeln($"Text of chapter {chapter}.");
        }

        document.FontInfos.EmbedTrueTypeFonts = true;
        document.FontInfos.EmbedSystemFonts = true;
        document.FontInfos.SaveSubsetFonts = true;
        document.Save(_workspace.File(target));

        var saved = new Document(_workspace.File(target));
        Assert.All(new[] { "Arial", "Times New Roman" }, name => Assert.True(Embeds(saved, name), $"{name} is not embedded"));
    }

    /// <summary>Copies a docx without the embedded font of one font-table entry.</summary>
    private void DropTheEmbeddedFont(string source, string target, string name)
    {
        File.Copy(_workspace.File(source), _workspace.File(target));
        RewritePart(target, "word/fontTable.xml", table => System.Text.RegularExpressions.Regex.Replace(
            table,
            $"(<w:font w:name=\"{name}\">.*?</w:font>)",
            static font => System.Text.RegularExpressions.Regex.Replace(font.Value, "<w:embed(Regular|Bold|Italic|BoldItalic) [^>]*/>", string.Empty),
            System.Text.RegularExpressions.RegexOptions.Singleline));

        var document = new Document(_workspace.File(target));
        Assert.False(Embeds(document, name));
        Assert.True(Embeds(document, "Arial"));
    }

    private static bool Embeds(Document document, string name) =>
        document.FontInfos[name] is { } font
        && new[] { Aspose.Words.Fonts.EmbeddedFontStyle.Regular, Aspose.Words.Fonts.EmbeddedFontStyle.Bold }.Any(style =>
            font.GetEmbeddedFont(Aspose.Words.Fonts.EmbeddedFontFormat.OpenType, style) is not null
            || font.GetEmbeddedFont(Aspose.Words.Fonts.EmbeddedFontFormat.EmbeddedOpenType, style) is not null);

    private void RewritePart(string file, string part, Func<string, string> rewrite)
    {
        using var package = System.IO.Compression.ZipFile.Open(_workspace.File(file), System.IO.Compression.ZipArchiveMode.Update);
        System.IO.Compression.ZipArchiveEntry entry = package.GetEntry(part)!;
        string text;
        using (var reader = new StreamReader(entry.Open()))
        {
            text = reader.ReadToEnd();
        }

        entry.Delete();
        using var writer = new StreamWriter(package.CreateEntry(part).Open());
        writer.Write(rewrite(text));
    }

    private CliResult Run(params string[] arguments) =>
        _workspace.RunWithEnv(Variables, [.. arguments, "--output-format", "json"]);

    private static JsonNode Succeeds(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr + result.StdOut);
        return JsonNode.Parse(result.StdOut)!;
    }

    /// <summary>The <c>encrypted</c> of each <c>outputs[]</c> item, which a DOCX part always reports.</summary>
    private static bool[] PartsEncrypted(JsonNode result) =>
        [.. result["outputs"]!.AsArray().Select(static output => output!["encrypted"]!.GetValue<bool>())];

    private static string[] Codes(JsonNode result) =>
        [.. result["warnings"]?.AsArray().Select(static warning => warning!["code"]!.GetValue<string>()) ?? []];
}
