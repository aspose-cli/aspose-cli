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
    [InlineData("docx", "out.htm", "html")]
    [InlineData("docx", "out.pdf", "pdf")]
    public void AnEditedDocument_KeepsTheSourceFormatThatOwnsTheOutputExtension(string source, string output, string expected)
    {
        using var workspace = new TempWorkspace();
        var document = new Document();
        new DocumentBuilder(document).Write("Clause one.");
        string input = workspace.File(source == "docx" ? "source.docx" : "source.xml");
        document.Save(input, source == "wordml" ? SaveFormat.WordML : source == "flatopc" ? SaveFormat.FlatOpc : SaveFormat.Docx);

        CliResult edited = workspace.Run(
            "words", "edit", Path.GetFileName(input), "--ops", "{\"ops\":[{\"op\":\"update_fields\"}]}",
            "--out", output, "--output", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal(expected, System.Text.Json.Nodes.JsonNode.Parse(edited.StdOut)!["output"]!["format"]!.GetValue<string>());
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

        fixture.Engine.Convert(input, new WordsConvertRequest { Output = TestOutput.At(output, format: "rtf") });

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
            "--in-place", "--output", "json");

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

        WordsCompareResult result = fixture.Engine.Compare(left, right, new WordsCompareRequest { Output = TestOutput.At(output) });

        Assert.Equal("pdf", result.Output!.Format);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(output), 0, 4));
    }
}
