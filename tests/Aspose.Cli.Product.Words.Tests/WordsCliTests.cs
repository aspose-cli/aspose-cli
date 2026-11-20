using System.Text;
using System.Text.Json.Nodes;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// The Words commands through the built CLI, with or without a license: the options each command
/// states to the engine, what it writes and what its result says. Each command is a process of
/// its own; without a license the engine adds its paragraph to every document it opens, so the
/// cases find what they change by search, not by a fixed position.
/// </summary>
public sealed class WordsCliTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    [Fact]
    public void ConvertText_ReadsItWithTheOptionsStated()
    {
        File.WriteAllText(_workspace.File("notes.txt"), "First line\n  Indented line\n", Encoding.UTF8);

        CliResult converted = _workspace.Run("words", "convert", "notes.txt", "--to", "docx", "--encoding", "utf-8",
            "--auto-numbering-detection", "false", "--detect-numbering-with-whitespaces", "false", "--detect-hyperlinks", "false", "--leading-spaces", "Preserve");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Contains("  Indented line", new Document(_workspace.File("notes.docx")).GetText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--encoding")]
    [InlineData("--auto-numbering-detection")]
    [InlineData("--detect-numbering-with-whitespaces")]
    [InlineData("--detect-hyperlinks")]
    [InlineData("--leading-spaces")]
    public void ConvertText_WithoutATextOption_IsRefusedNamingIt(string left)
    {
        File.WriteAllText(_workspace.File("notes.txt"), "Text\n");
        string[] options = ["--encoding", "utf-8", "--auto-numbering-detection", "false", "--detect-numbering-with-whitespaces", "false", "--detect-hyperlinks", "false", "--leading-spaces", "Trim"];
        int at = Array.IndexOf(options, left);

        CliResult refused = _workspace.Run(["words", "convert", "notes.txt", "--to", "docx", .. options[..at], .. options[(at + 2)..]]);

        Assert.Equal(2, refused.ExitCode);
        Assert.Contains(left, Error(refused)["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("notes.docx")));
    }

    [Fact]
    public void ConvertMarkdown_TakesTheEncodingOnly()
    {
        File.WriteAllText(_workspace.File("notes.md"), "# Title\n\nBody text.\n", Encoding.UTF8);

        CliResult converted = _workspace.Run("words", "convert", "notes.md", "--to", "docx", "--encoding", "utf-8");
        CliResult refused = _workspace.Run("words", "convert", "notes.md", "--to", "docx", "--encoding", "utf-8", "--detect-hyperlinks", "false", "--out", "other.docx");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Contains("Body text.", new Document(_workspace.File("notes.docx")).GetText(), StringComparison.Ordinal);
        Assert.Equal(2, refused.ExitCode);
        Assert.Contains("--detect-hyperlinks", Error(refused)["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("search")]
    [InlineData("edit")]
    [InlineData("render")]
    public void TextOnlyConvertReads_IsFormatUnsupported(string verb)
    {
        File.WriteAllText(_workspace.File("notes.md"), "# Title\n");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"do":"update_fields"}]}""");
        string[] tail = verb switch
        {
            "search" => ["--pattern", "Title"],
            "edit" => ["--ops", "ops.json", "--out", "out.md"],
            _ => [],
        };

        CliResult refused = _workspace.Run(["words", verb, "notes.md", .. tail]);

        Assert.Equal("FORMAT_UNSUPPORTED", Error(refused)["code"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("9")]
    [InlineData("1-9")]
    [InlineData("2-")]
    public void Render_OfAPagePastTheLast_IsPageNotFound_AndWritesNothing(string pages)
    {
        WriteDocument("doc.docx", "One page.");

        CliResult refused = _workspace.Run("words", "render", "doc.docx", "--pages", pages, "--out", "page.png");

        Assert.Equal("PAGE_NOT_FOUND", Error(refused)["code"]!.GetValue<string>());
        Assert.Empty(Directory.GetFiles(_workspace.Path, "page*.png"));
    }

    [Fact]
    public void Render_DrawsEveryPageByDefault()
    {
        WriteDocument("doc.docx", "One page.");

        CliResult rendered = _workspace.Run("words", "render", "doc.docx", "--out", "page.png");

        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        Assert.NotEmpty(JsonNode.Parse(rendered.StdOut)!["outputs"]!.AsArray());
    }

    [Theory]
    [InlineData("--author")]
    [InlineData("--date-time")]
    public void Compare_TakesTheAuthorAndTheDateTime(string left)
    {
        WriteDocument("a.docx", "Original text.");
        WriteDocument("b.docx", "Changed text.");
        string[] options = ["--author", "Reviewer", "--date-time", "2026-03-01T09:00:00"];
        int at = Array.IndexOf(options, left);

        CliResult refused = _workspace.Run(["words", "compare", "a.docx", "b.docx", "--out", "redline.docx", .. options[..at], .. options[(at + 2)..]]);

        Assert.Equal(2, refused.ExitCode);
        Assert.False(File.Exists(_workspace.File("redline.docx")));
    }

    [Fact]
    public void Compare_WritesTheRedline_WithTheEnginesRevisionCount()
    {
        WriteDocument("a.docx", "Original text.");
        WriteDocument("b.docx", "Changed text.");

        CliResult compared = _workspace.Run("words", "compare", "a.docx", "b.docx", "--author", "Reviewer", "--date-time", "2026-03-01T09:00:00", "--out", "redline.docx");

        Assert.True(compared.ExitCode == 0, compared.StdErr);
        var redline = new Document(_workspace.File("redline.docx"));
        Assert.Equal(redline.Revisions.Count, JsonNode.Parse(compared.StdOut)!["revisionCount"]!.GetValue<int>());
        Assert.Contains(redline.Revisions.Cast<Revision>(), static revision => revision.Author == "Reviewer");
    }

    [Fact]
    public void Create_WritesTheEnginesNewDocument_OrATemplateCopy()
    {
        WriteDocument("template.docx", "Template text.");

        CliResult blank = _workspace.Run("words", "create", "blank.docx");
        CliResult copy = _workspace.Run("words", "create", "copy.docx", "--template", "template.docx");

        Assert.True(blank.ExitCode == 0, blank.StdErr);
        Assert.True(copy.ExitCode == 0, copy.StdErr);
        Assert.Contains("Template text.", new Document(_workspace.File("copy.docx")).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WritesTheBytesOfEachStoredImage()
    {
        var document = new Document();
        new DocumentBuilder(document).InsertImage(WordsTableRoundTripTests.Png());
        document.Save(_workspace.File("pictures.docx"));

        CliResult extracted = _workspace.Run("words", "extract", "pictures.docx", "--what", "images", "--out-dir", "images");

        Assert.True(extracted.ExitCode == 0, extracted.StdErr);
        Assert.Contains(Directory.GetFiles(_workspace.File("images")), file => File.ReadAllBytes(file).AsSpan().SequenceEqual(WordsTableRoundTripTests.Png()));
    }

    [Fact]
    public void EditInPlace_LeavesOnlyTheDocument_AndReadsTheChangeBack()
    {
        WriteDocument("doc.docx", "Contoso plans.");
        File.WriteAllText(_workspace.File("ops.json"), """{"ops":[{"do":"replace","args":{"pattern":"Contoso","replacement":"Fabrikam"}}]}""");

        CliResult edited = _workspace.Run("words", "edit", "doc.docx", "--in-place", "--ops", "ops.json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal(1, JsonNode.Parse(edited.StdOut)!["ops"]![0]!["returned"]!.GetValue<int>());
        Assert.Equal(["doc.docx", "ops.json"], Directory.EnumerateFileSystemEntries(_workspace.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Contains("Fabrikam plans.", new Document(_workspace.File("doc.docx")).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void SearchHit_NamesTheParagraph_ThatEditTakes()
    {
        WriteDocument("doc.docx", "Contoso plans.");

        CliResult found = _workspace.Run("words", "search", "doc.docx", "--pattern", "Contoso");
        string at = JsonNode.Parse(found.StdOut)!["hits"]![0]!["at"]!.GetValue<string>();
        File.WriteAllText(_workspace.File("ops.json"), $$$"""{"ops":[{"set":"{{{at}}}","props":{"paragraphFormat.alignment":"Center"}}]}""");
        CliResult edited = _workspace.Run("words", "edit", "doc.docx", "--ops", "ops.json", "--out", "out.docx");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal("Center", JsonNode.Parse(edited.StdOut)!["ops"]![0]!["now"]!["paragraphFormat.alignment"]!.GetValue<string>());
    }

    [Fact]
    public void EncryptedDocument_IsReadWithItsPassword()
    {
        var document = new Document();
        new DocumentBuilder(document).Write("Secret text.");
        document.Save(_workspace.File("secret.docx"), new Aspose.Words.Saving.OoxmlSaveOptions { Password = "pw" });

        CliResult refused = _workspace.Run("words", "read", "secret.docx");
        CliResult read = _workspace.RunWithEnv(new Dictionary<string, string?> { ["DOC_PASSWORD"] = "pw" }, "words", "read", "secret.docx", "--password-env", "DOC_PASSWORD", "--at", "section/0/body/range");

        Assert.NotEqual(0, refused.ExitCode);
        Assert.True(read.ExitCode == 0, read.StdErr);
        Assert.True(JsonNode.Parse(read.StdOut)!["input"]!["encrypted"]!.GetValue<bool>());
        Assert.Contains("Secret text.", read.StdOut, StringComparison.Ordinal);
    }

    private void WriteDocument(string name, string text)
    {
        var document = new Document();
        new DocumentBuilder(document).Write(text);
        document.Save(_workspace.File(name));
    }

    private static JsonNode Error(CliResult result) => JsonNode.Parse(result.StdErr)!["error"]!;

    public void Dispose() => _workspace.Dispose();
}
