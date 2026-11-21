using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesHardeningTests
{
    [Fact]
    public void LoadedInput_CanBeReplacedInPlaceByAnotherWriter()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("loaded.pptx", slides: 1);
        string replacement = fixture.CreatePresentation("replacement.pptx", slides: 2);
        var loader = SlidesEngineFixture.Loader(ProductTestBudgets.Create<SlidesModule>());

        // A concurrent in-place edit publishes by replacing the file the engine still reads.
        using (Engine.Mapping.LoadedPresentation loaded = loader.OpenTable(input, password: null, new Aspose.Cli.Sdk.Tables.EngineWarningLog()))
        {
            File.Replace(replacement, input, destinationBackupFileName: null);
            Assert.Single(loaded.Presentation.Slides);
        }

        using var replaced = new Aspose.Slides.Presentation(input);
        Assert.Equal(2, replaced.Slides.Count);
    }

    [Fact]
    public void LargeDeck_SupportsBoundedRendering()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("windowed.pptx", slides: 12);

        SlidesRenderResult rendered = SlidesExport.Render(
            fixture.Session,
            new SlidesRenderRequest
            {
                Input = input,
                Output = TestOutput.At(fixture.File("bounded.png"), format: "png"),
                Slides = PageRange.Parse("1,12"),
                Width = 320,
            });

        Assert.Equal([1, 12], rendered.Outputs.Select(static item => item.Slide));
        Assert.All(rendered.Outputs, static item => Assert.True(item.SizeBytes > 100));
        Assert.Equal(320, PngWidth(rendered.Outputs[0].Path));
    }

    [Fact]
    public void MediaExtraction_CopiesTheStoredBytesAndIsNotDisclosedAsEvaluationOutput()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("media.pptx");
        using (var presentation = new Aspose.Slides.Presentation())
        {
            Aspose.Slides.IPPImage image = presentation.Images.AddImage(SlidesEngineFixture.Png(16));
            presentation.Slides[0].Shapes.AddPictureFrame(Aspose.Slides.ShapeType.Rectangle, 10, 10, 50, 50, image);
            presentation.Save(input, Aspose.Slides.Export.SaveFormat.Pptx);
        }

        byte[] stored;
        using (System.IO.Compression.ZipArchive package = System.IO.Compression.ZipFile.OpenRead(input))
        {
            System.IO.Compression.ZipArchiveEntry entry = Assert.Single(package.Entries,
                static entry => entry.FullName.StartsWith("ppt/media/", StringComparison.Ordinal));
            using Stream stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            stored = copy.ToArray();
        }

        SlidesExtractResult result = fixture.Disclosed(session => SlidesExtract.Run(session, new SlidesExtractRequest
        {
            Input = input,
            Output = new ResolvedDirectory(fixture.File("media")),
            What = SlidesExtractKinds.Media,
        }));

        SlidesExtractedOutput item = Assert.Single(result.Outputs);
        Assert.Equal(stored, File.ReadAllBytes(item.Path));
    }

    [Fact]
    public void UnboundedHighDpiBatch_IsRejectedBeforeWriting()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("budget.pptx", slides: 1);
        using (var presentation = new Aspose.Slides.Presentation(input))
        {
            presentation.SlideSize.SetSize(
                720,
                2_000,
                Aspose.Slides.SlideSizeScaleType.DoNotScale);
            presentation.Save(input, Aspose.Slides.Export.SaveFormat.Pptx);
        }

        string output = fixture.File("too-large.png");

        CliException error = Assert.Throws<CliException>(() =>
            SlidesExport.Render(
                fixture.Session,
                new SlidesRenderRequest
                {
                    Input = input,
                    Output = TestOutput.At(output, format: "png"),
                    Dpi = 1200,
                }));

        Assert.Equal(ErrorCodes.RenderTooLarge, error.Code);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(fixture.Temp.Path, "too-large.*.png"));
    }

    [Fact]
    public void Render_RespectsExactWidthAndDpiGeometry()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("geometry.pptx", slides: 1);
        string widthOutput = fixture.File("width.png");
        string dpiOutput = fixture.File("dpi.png");

        SlidesExport.Render(fixture.Session, new SlidesRenderRequest
        {
            Input = input,
            Output = TestOutput.At(widthOutput, format: "png"),
            Width = 640,
        });
        SlidesExport.Render(fixture.Session, new SlidesRenderRequest
        {
            Input = input,
            Output = TestOutput.At(dpiOutput, format: "png"),
            Dpi = 150,
        });

        Assert.Equal(640, PngWidth(widthOutput));
        Assert.Equal(1500, PngWidth(dpiOutput));
    }

    [Theory]
    [MemberData(nameof(MalformedPresentations))]
    public void MalformedPresentation_ReturnsFileCorrupt(
        string extension,
        byte[] content)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("malformed" + extension);
        File.WriteAllBytes(input, content);
        CliException readError = Assert.Throws<CliException>(() =>
            SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input }));

        Assert.Equal(ErrorCodes.FileCorrupt, readError.Code);
    }

    [Theory]
    [MemberData(nameof(UnrecognizedContents))]
    public void UnrecognizedContentNamedAsAPresentation_SaysItIsInNoSupportedFormat(byte[] content)
    {
        // SLD-5: the engine names such content Unknown, which is no format to tell a user.
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("renamed.pptx");
        File.WriteAllBytes(input, content);

        CliException error = Assert.Throws<CliException>(() =>
            SlidesRead.Run(fixture.Session, new SlidesReadRequest { Input = input }));

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.DoesNotContain("Unknown", error.Message, StringComparison.Ordinal);
        Assert.Contains("(its content is not in any supported presentation format).", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<byte[]> UnrecognizedContents => new()
    {
        "# Quarterly review\n\n## Results\n\n- Revenue up 12%\n- Costs down 3%\n"u8.ToArray(),
        Enumerable.Range(0, 4096).Select(static index => (byte)((index * 7919 + 13) % 251)).ToArray(),
    };

    [Fact]
    public void MalformedConversion_WritesNoPartialOutput()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("malformed.pptx");
        File.WriteAllBytes(input, [0x50, 0x4B, 0x03, 0x04, 0x00]);
        string output = fixture.File("malformed.pdf");

        CliException error = Assert.Throws<CliException>(() =>
            SlidesExport.Convert(
                fixture.Session,
                new SlidesConvertRequest
                {
                    Input = input,
                    Output = TestOutput.At(output, format: "pdf"),
                }));

        Assert.Equal(ErrorCodes.FileCorrupt, error.Code);
        Assert.False(File.Exists(output));
    }

    public static TheoryData<string, byte[]> MalformedPresentations => new()
    {
        { ".pptx", [0x50, 0x4B, 0x03, 0x04, 0x00] },
        { ".ppt", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1] },
        { ".pptx", "plain text renamed as a deck"u8.ToArray() },
        { ".odp", [0x00, 0x01, 0x02, 0x03, 0xFF] },
    };

    private static int PngWidth(string path)
    {
        byte[] header = File.ReadAllBytes(path);
        Assert.True(header.Length >= 24);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], header[..4]);
        return (header[16] << 24)
            | (header[17] << 16)
            | (header[18] << 8)
            | header[19];
    }
}
