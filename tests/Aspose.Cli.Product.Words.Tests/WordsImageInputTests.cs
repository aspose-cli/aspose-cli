using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// Aspose.Words embeds an image it cannot fully decode, so the CLI checks every image before
/// the engine sees it.
/// </summary>
public sealed class WordsImageInputTests
{
    private static readonly byte[] CutPng = ResourceHttpServer.Image[..(ResourceHttpServer.Image.Length / 2)];

    [Fact]
    public void InsertImage_RefusesATruncatedImageAsAnInvalidOperation()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string image = Write(fixture, "cut.png", CutPng);
        string output = fixture.Temp.File("report.out.docx");

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertMarkdownOp { At = new WordsTarget { Block = 1 }, Position = "after", Markdown = "Lead." },
                    new InsertImageOp { At = new WordsTarget { Block = 1 }, Position = "after", Path = image },
                ],
            },
            Output = TestOutput.At(output),
        }));

        OperationErrors.AssertInvalid(error, 1, "insert_image", image);
        Assert.False(File.Exists(output));
    }

    // Skia decodes the rows before the cut of a noisy image and reports them incomplete, which
    // SKBitmap.Decode accepts; only a full decode that must succeed refuses the image.
    [Fact]
    public void Watermark_RefusesAnImageCutInItsPixelDataAsAnInvalidOperation()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        byte[] png = NoisyPng(64, 48);
        string image = Write(fixture, "cut.png", png[..(png.Length * 3 / 5)]);
        string output = fixture.Temp.File("report.out.docx");

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new SetWatermarkOp { Path = image }] },
            Output = TestOutput.At(output),
        }));

        OperationErrors.AssertInvalid(error, 0, "set_watermark", image);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Watermark_TakesAnIntactImage()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string image = Write(fixture, "logo.png", ResourceHttpServer.Image);
        string output = fixture.Temp.File("report.out.docx");

        WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new SetWatermarkOp { Path = image }] },
            Output = TestOutput.At(output),
        });

        Assert.Equal(Aspose.Words.WatermarkType.Image, new Aspose.Words.Document(output).Watermark.Type);
    }

    [Fact]
    public void CreateFromMarkdown_RefusesATruncatedImageAsACorruptFileNamingIt()
    {
        using var fixture = new WordsFixture();
        string image = Write(fixture, "chart.png", CutPng);
        string markdown = Write(fixture, "report.md", "# Report\n\n![Chart](chart.png)\n"u8.ToArray());
        string output = fixture.Temp.File("report.docx");

        CliException error = Assert.Throws<CliException>(() => WordsCreate.Run(fixture.Session,
            new WordsCreateRequest { Output = TestOutput.At(output), Markdown = new SourceFile("--from-markdown", markdown) }));

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.Equal(image, error.Details!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void InsertMarkdown_RefusesATruncatedImageAsACorruptFileNamingIt()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string image = Write(fixture, "chart.png", CutPng);
        string output = fixture.Temp.File("report.out.docx");

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops = [new InsertMarkdownOp { At = new WordsTarget { Block = 1 }, Position = "after", Markdown = "![Chart](chart.png)" }],
            },
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Equal(image, error.Details["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void InsertMarkdown_WithATruncatedImageFailsAloneInABestEffortBatch()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        Write(fixture, "chart.png", CutPng);
        string output = fixture.Temp.File("report.out.docx");

        WordsEditResult result = WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch
            {
                Ops =
                [
                    new InsertMarkdownOp { At = new WordsTarget { Block = 1 }, Position = "after", Markdown = "![Chart](chart.png)" },
                    new InsertMarkdownOp { At = new WordsTarget { Block = 1 }, Position = "after", Markdown = "Kept paragraph." },
                ],
            },
            Output = TestOutput.At(output),
            Options = new EditCommandOptions { BestEffort = true },
        });

        Assert.Equal([OpStatuses.Failed, OpStatuses.Ok], result.Applied.Select(static item => item.Status));
        Assert.Equal(ErrorCodes.FileCorrupt.Name, result.Applied[0].Error!.Code);
        Assert.Contains("Kept paragraph.", new Aspose.Words.Document(output).GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public void InsertImage_RefusesACompressedSvgAsPdfAndCellsDo()
    {
        using var fixture = new WordsFixture();
        string input = fixture.CreateReport();
        string image = Write(fixture, "diagram.svgz", Gzip("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"9\" height=\"9\"/>"));
        string output = fixture.Temp.File("report.out.docx");

        CliException error = Assert.Throws<CliException>(() => WordsEdit.Run(fixture.Session, new WordsEditRequest
        {
            Input = input,
            Batch = new WordsOpsBatch { Ops = [new InsertImageOp { At = new WordsTarget { Block = 1 }, Position = "after", Path = image }] },
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Contains("SVGZ", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private static byte[] Gzip(string text)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(text));
        }

        return compressed.ToArray();
    }

    private static byte[] NoisyPng(int width, int height)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height);
        var random = new Random(7);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SkiaSharp.SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
            }
        }

        using SkiaSharp.SKData data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string Write(WordsFixture fixture, string name, byte[] content)
    {
        string path = fixture.Temp.File(name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
