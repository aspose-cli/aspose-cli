using Aspose.Words;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

public sealed class WordsOutputFormatTests
{
    [Theory]
    [InlineData("out.docx", null, "docx")]
    [InlineData("out.xml", null, "flatopc")]
    [InlineData("out.xml", "wordml", "wordml")]
    [InlineData("out.XML", "flatopc", "flatopc")]
    [InlineData("out.htm", null, "html")]
    [InlineData("out.pdf", "docx", "pdf")]
    public void ForOutput_KeepsTheSourceFormatThatOwnsTheExtension(string path, string? source, string expected) =>
        Assert.Equal(expected, WordsFormats.ForOutput(path, source));

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

        fixture.Engine.Convert(input, new WordsConvertRequest { TargetFormatId = "rtf", OutputPath = output });

        Assert.True(new FileInfo(output).Length < 100_000, $"{new FileInfo(output).Length} bytes");
        Assert.Single(new Document(output).FirstSection.Body.GetChildNodes(NodeType.Shape, true).Cast<Aspose.Words.Drawing.Shape>(), static shape => shape.HasImage);
    }

    [Fact]
    public void InPlaceEditOfWordML_StaysWordML()
    {
        using var fixture = new WordsFixture();
        string docx = fixture.CreateReport();
        string input = fixture.Temp.File("report.xml");
        new Document(docx).Save(input, SaveFormat.WordML);

        WordsEditResult result = fixture.Engine.ApplyOps(input, new WordsOpsBatch
        {
            Ops = [new ReplaceTextOp { Find = "twelve", Replace = "ten" }],
        }, new WordsEditRequest { OutputPath = input, Overwrite = true });

        Assert.Equal("wordml", result.Output!.Format);
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

        WordsCompareResult result = fixture.Engine.Compare(left, right, new WordsCompareRequest { OutputPath = output });

        Assert.Equal("pdf", result.Output!.Format);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(output), 0, 4));
    }
}
