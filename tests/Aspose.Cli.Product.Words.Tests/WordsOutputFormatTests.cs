using System.Text.Json.Nodes;
using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsOutputFormatTests
{
    [Theory]
    [InlineData("docx", "out.docx", "docx")]
    [InlineData("docx", "out.xml", "flatopc")]
    [InlineData("wordml", "out.xml", "wordml")]
    [InlineData("flatopc", "out.XML", "flatopc")]
    public void AnEditedDocument_KeepsTheSourceFormatThatOwnsTheOutputExtension(string source, string output, string expected)
    {
        using var workspace = new TempWorkspace();
        var document = new Document();
        new DocumentBuilder(document).Write("Clause one.");
        string input = workspace.File(source == "docx" ? "source.docx" : "source.xml");
        document.Save(input, source == "wordml" ? SaveFormat.WordML : source == "flatopc" ? SaveFormat.FlatOpc : SaveFormat.Docx);

        CliResult edited = workspace.Run(
            "words", "edit", Path.GetFileName(input), "--ops", "{\"ops\":[{\"op\":\"refresh_fields\"}]}",
            "--out", output, "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal(expected, System.Text.Json.Nodes.JsonNode.Parse(edited.StdOut)!["output"]!["format"]!.GetValue<string>());
    }

    private const string RefreshFields = "{\"ops\":[{\"op\":\"refresh_fields\"}]}";

    [Theory]
    [InlineData("edit", "out.doc", "doc")]
    [InlineData("edit", "out.dot", "dot")]
    [InlineData("edit", "out.docm", "docm")]
    [InlineData("edit", "out.dotx", "dotx")]
    [InlineData("edit", "out.dotm", "dotm")]
    [InlineData("edit", "out.rtf", "rtf")]
    [InlineData("edit", "out.odt", "odt")]
    [InlineData("edit", "out.ott", "ott")]
    [InlineData("create", "out.docx", "docx")]
    [InlineData("create", "out.doc", "doc")]
    [InlineData("create", "out.xml", "flatopc")]
    [InlineData("create", "out.rtf", "rtf")]
    [InlineData("create", "out.odt", "odt")]
    public void EditAndCreate_WriteTheFormatsThatKeepTheDocument(string verb, string output, string expected)
    {
        using var workspace = new TempWorkspace();
        SaveClause(workspace.File("source.docx"));

        CliResult written = workspace.Run([.. Command(verb, output), "--output-format", "json"]);

        Assert.True(written.ExitCode == 0, written.StdErr);
        Assert.Equal(expected, JsonNode.Parse(written.StdOut)!["output"]!["format"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("edit", "out.pdf", "pdf")]
    [InlineData("edit", "out.md", "md")]
    [InlineData("edit", "out.html", "html")]
    [InlineData("edit", "out.txt", "txt")]
    [InlineData("create", "out.pdf", "pdf")]
    [InlineData("create", "out.md", "md")]
    public void EditAndCreate_RefuseAFormatThatLosesTheDocument_AndPointToTheConvertThatWritesIt(string verb, string output, string format)
    {
        using var workspace = new TempWorkspace();
        SaveClause(workspace.File("source.docx"));

        CliResult refused = workspace.Run([.. Command(verb, output), "--output-format", "json"]);

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(format, error["details"]!["format"]!.GetValue<string>());
        Assert.Equal(
            [".doc", ".dot", ".docx", ".docm", ".dotx", ".dotm", ".xml", ".rtf", ".odt", ".ott"],
            error["details"]!["available"]!.AsArray().Select(static extension => extension!.GetValue<string>()));
        Assert.False(File.Exists(workspace.File(output)));
        string convert = $"aspose-cli words convert <that file> --to {format}";
        Assert.Equal($"Give {(verb == "edit" ? "--out" : "the file")} one of these extensions, then run '{convert}' for {format}.", error["hint"]!.GetValue<string>());

        // The hint works as written: the same command to a .docx, then the convert it names.
        CliResult kept = workspace.Run([.. Command(verb, "kept.docx"), "--output-format", "json"]);
        Assert.True(kept.ExitCode == 0, kept.StdErr);
        string[] converting = convert.Replace("<that file>", "kept.docx", StringComparison.Ordinal).Split(' ')[1..];
        CliResult converted = workspace.Run([.. converting, "--out", output, "--output-format", "json"]);
        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal(format, JsonNode.Parse(converted.StdOut)!["outputs"]![0]!["format"]!.GetValue<string>());
    }

    [Fact]
    public void EditOfAnInputItCannotWrite_IsRefusedInPlace_AndWritesTheEditedCopyItNames()
    {
        using var workspace = new TempWorkspace();
        const string Notes = "# Notes\n\nClause one.\n";
        File.WriteAllText(workspace.File("notes.md"), Notes);

        CliResult refused = workspace.Run("words", "edit", "notes.md", "--in-place", "--ops", RefreshFields, "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("Name the output with --out and one of these extensions, then run 'aspose-cli words convert <that file> --to md' for md.", error["hint"]!.GetValue<string>());
        Assert.Equal(Notes, File.ReadAllText(workspace.File("notes.md")));
        CliResult copied = workspace.Run("words", "edit", "notes.md", "--ops", RefreshFields, "--out", "notes.docx", "--output-format", "json");
        Assert.True(copied.ExitCode == 0, copied.StdErr);
    }

    private static string[] Command(string verb, string output) => verb == "edit"
        ? ["words", "edit", "source.docx", "--ops", RefreshFields, "--out", output]
        : ["words", "create", output];

    private static void SaveClause(string path)
    {
        var document = new Document();
        new DocumentBuilder(document).Write("Clause one.");
        document.Save(path);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("html-fixed")]
    public void ConvertToEveryHtmlFormat_AcceptsTheHtmExtension(string format)
    {
        using var workspace = new TempWorkspace();
        var document = new Document();
        new DocumentBuilder(document).Write("Clause one.");
        document.Save(workspace.File("source.docx"));

        CliResult converted = workspace.Run("words", "convert", "source.docx", "--to", format, "--out", "page.htm");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Contains("Clause", File.ReadAllText(workspace.File("page.htm")), StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToMarkdown_AcceptsTheMarkdownExtension()
    {
        using var workspace = new TempWorkspace();
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Heading1;
        builder.Writeln("Quarterly review");
        builder.ParagraphFormat.StyleIdentifier = StyleIdentifier.Normal;
        builder.Write("Clause one.");
        document.Save(workspace.File("source.docx"));

        CliResult converted = workspace.Run("words", "convert", "source.docx", "--to", "md", "--out", "notes.markdown", "--output-format", "json");

        Assert.True(converted.ExitCode == 0, converted.StdErr);
        Assert.Equal("md", System.Text.Json.Nodes.JsonNode.Parse(converted.StdOut)!["outputs"]![0]!["format"]!.GetValue<string>());
        string[] lines = File.ReadAllLines(workspace.File("notes.markdown"));
        Assert.Contains(lines, static line => line.StartsWith("# ", StringComparison.Ordinal) && line.Contains("Quarterly review", StringComparison.Ordinal));
        Assert.Contains("Clause one.", lines);
    }

    [Fact]
    public void ConvertToRtf_StoresImagesOnceInTheirOwnFormat()
    {
        // The SDK would also store each image as an uncompressed metafile for old RTF readers,
        // which makes a document with a few photos a hundred times larger.
        using var fixture = new WordsFixture();
        using var bitmap = new SkiaSharp.SKBitmap(800, 800);
        bitmap.Erase(SkiaSharp.SKColors.SteelBlue);
        using SkiaSharp.SKData png = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var document = new Document();
        var builder = new DocumentBuilder(document);
        builder.Writeln("Org chart");
        builder.InsertImage(png.ToArray());
        string input = fixture.Temp.File("picture.docx");
        document.Save(input);
        string output = fixture.Temp.File("picture.rtf");

        WordsConvert.Run(fixture.Session, new WordsConvertRequest { Input = input, Output = TestOutput.At(output, format: "rtf") });

        Assert.True(new FileInfo(output).Length < 100_000, $"{new FileInfo(output).Length} bytes");
        Assert.Single(new Document(output).FirstSection.Body.GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>(), static shape => shape.HasImage);
    }

    [Fact]
    public void InPlaceEditOfWordML_StaysWordML()
    {
        using var workspace = new TempWorkspace();
        var document = new Document();
        new DocumentBuilder(document).Write("Revenue increased by twelve percent.");
        string input = workspace.File("report.xml");
        document.Save(input, SaveFormat.WordML);

        CliResult edited = workspace.Run(
            "words", "edit", "report.xml", "--ops", "{\"ops\":[{\"op\":\"replace_text\",\"find\":\"twelve\",\"replace\":\"ten\"}]}",
            "--in-place", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal("wordml", System.Text.Json.Nodes.JsonNode.Parse(edited.StdOut)!["output"]!["format"]!.GetValue<string>());
        Assert.Equal(LoadFormat.WordML, FileFormatUtil.DetectFileFormat(input).LoadFormat);
    }

    [Fact]
    public void CompareToPdf_WritesAPdf()
    {
        using var fixture = new WordsFixture();
        string left = fixture.CreateReport("left.docx");
        string right = fixture.Temp.File("right.docx");
        var changed = new Document(left);
        changed.Range.Replace("twelve", "ten");
        changed.Save(right);
        string output = fixture.Temp.File("redline.pdf");

        WordsCompareResult result = WordsCompare.Run(fixture.Session, new WordsCompareRequest { Left = left, Right = right, Output = TestOutput.At(output) });

        Assert.Equal("pdf", result.Output!.Format);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(output), 0, 4));
    }
}
