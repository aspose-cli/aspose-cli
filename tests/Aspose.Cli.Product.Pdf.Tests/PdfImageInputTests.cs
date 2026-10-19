using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Aspose.PDF accepts any bytes as a stamp or page image and fails, or embeds nothing, only when
/// the document is saved; the CLI checks every image before the engine sees it.
/// </summary>
public sealed class PdfImageInputTests
{
    private static readonly byte[] CutPng = ResourceHttpServer.Image[..(ResourceHttpServer.Image.Length / 2)];

    [Fact]
    public void Edit_RefusesAWatermarkImageThatIsNotAnImageAsAnInvalidOperation()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("source.pdf");
        string image = fixture.File("notes.png");
        File.WriteAllText(image, "not an image");
        string output = fixture.File("source.out.pdf");

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch { Ops = [new AddWatermarkTextOp { Text = "DRAFT" }, new AddWatermarkImageOp { Path = image }] },
            Output = TestOutput.At(output),
        }));

        OperationErrors.AssertInvalid(error, 1, "add_watermark_image", image);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Edit_RefusesATruncatedStampImageAsAnInvalidOperation()
    {
        using var fixture = new PdfEngineFixture();
        string input = fixture.CreateDocument("source.pdf");
        string image = fixture.File("cut.png");
        File.WriteAllBytes(image, CutPng);
        string output = fixture.File("source.out.pdf");

        CliException error = Assert.Throws<CliException>(() => PdfEdit.Run(fixture.Session, new PdfEditRequest
        {
            Input = input,
            Batch = new PdfOpsBatch
            {
                Ops = [new AddStampImageOp { Page = 1, Path = image, Rect = new PdfRectInput { X = 20, Y = 20, Width = 20, Height = 20 } }],
            },
            Output = TestOutput.At(output),
        }));

        OperationErrors.AssertInvalid(error, 0, "add_stamp_image", image);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void EditCli_ExitsWithAValidationErrorForATruncatedWatermarkImage()
    {
        using var workspace = new TempWorkspace();
        Assert.Equal(0, workspace.Run("pdf", "create", "source.pdf", "--from-text", Write(workspace, "source.txt", "Body"u8.ToArray())).ExitCode);
        Write(workspace, "cut.png", CutPng);

        CliResult edited = workspace.Run("pdf", "edit", "source.pdf", "--ops",
            """{"ops":[{"op":"add_watermark_image","path":"cut.png"}]}""", "--out", "out.pdf", "--output-format", "json");

        Assert.Equal(4, edited.ExitCode);
        JsonNode error = JsonNode.Parse(edited.StdErr)!["error"]!;
        Assert.Equal("OPS_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("op-0001", error["details"]!["id"]!.GetValue<string>());
        Assert.False(File.Exists(workspace.File("out.pdf")));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("cut")]
    public void CreateFromImages_RefusesAnImageThatIsNotAnIntactImageAsACorruptFile(string kind)
    {
        using var workspace = new TempWorkspace();
        Write(workspace, "good.png", ResourceHttpServer.Image);
        string bad = Write(workspace, "scan.png", kind == "text" ? Encoding.UTF8.GetBytes("not an image") : CutPng);

        CliResult created = workspace.Run("pdf", "create", "scan.pdf", "--from-images", "good.png", "scan.png", "--output-format", "json");

        AssertCorruptImage(created, bad);
        Assert.False(File.Exists(workspace.File("scan.pdf")));
    }

    [Theory]
    [InlineData("![Chart](chart.png)")]
    [InlineData("<img src=\"chart.png\">")]
    [InlineData("<picture><source srcset=\"chart.png 1x\"><img src=\"logo.png\"></picture>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"chart.png\" width=\"9\" height=\"9\"/></svg>")]
    public void CreateFromMarkdown_RefusesAReferencedImageThatIsNotAnIntactImageAsACorruptFile(string reference)
    {
        using var workspace = new TempWorkspace();
        string bad = Write(workspace, "chart.png", CutPng);
        Write(workspace, "logo.png", ResourceHttpServer.Image);
        Write(workspace, "report.md", Encoding.UTF8.GetBytes("# Report\n\n" + reference + "\n"));

        CliResult created = workspace.Run("pdf", "create", "report.pdf", "--from-markdown", "report.md", "--output-format", "json");

        AssertCorruptImage(created, bad);
        Assert.False(File.Exists(workspace.File("report.pdf")));
    }

    /// <summary>
    /// An image a stylesheet's url() names is checked like any image the importer embeds: a file
    /// with an image's signature or name that is not an intact image is refused, and a font a
    /// stylesheet names is not taken for one.
    /// </summary>
    [Theory]
    [InlineData("<div style=\"background-image:url(chart.png); height:40px\">Band</div>", "cut")]
    [InlineData("<style>body { background: url('chart.png') }</style>", "cut")]
    [InlineData("<div style=\"background-image:url(chart.png); height:40px\">Band</div>", "text")]
    public void CreateFromMarkdown_RefusesAStylesheetImageThatIsNotAnIntactImage(string reference, string kind)
    {
        using var workspace = new TempWorkspace();
        string bad = Write(workspace, "chart.png", kind == "text" ? Encoding.UTF8.GetBytes("not an image") : CutPng);
        Write(workspace, "report.md", Encoding.UTF8.GetBytes("# Report\n\n" + reference + "\n"));

        CliResult created = workspace.Run("pdf", "create", "report.pdf", "--from-markdown", "report.md", "--output-format", "json");

        AssertCorruptImage(created, bad);
        Assert.False(File.Exists(workspace.File("report.pdf")));
    }

    [Fact]
    public void CreateFromMarkdown_TakesAFontAStylesheetNamesForNoImage()
    {
        using var workspace = new TempWorkspace();
        Write(workspace, "brand.woff", [.. "wOFF"u8, 0, 1, 0, 0, 0, 0, 0, 0]);
        Write(workspace, "report.md", Encoding.UTF8.GetBytes(
            "<style>@font-face { font-family: Brand; src: url(brand.woff) } h1 { font-family: Brand }</style>\n\n# Report\n"));

        CliResult created = workspace.Run("pdf", "create", "report.pdf", "--from-markdown", "report.md", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
    }

    // The engine embeds a streamed image by the type the check recognized, not by the file's name.
    [Fact]
    public void FileTypeOf_NamesTheTypesOfSvgAndDicomFromTheirContent()
    {
        Assert.Equal(Aspose.Pdf.ImageFileType.Dicom, PdfCreate.FileTypeOf(Dicom()));
        Assert.Equal(Aspose.Pdf.ImageFileType.Svg, PdfCreate.FileTypeOf(
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"9\" height=\"9\"/>")));
        Assert.Equal(Aspose.Pdf.ImageFileType.Unknown, PdfCreate.FileTypeOf(ResourceHttpServer.Image));
    }

    [Fact]
    public void CreateFromImages_EmbedsADicomImage()
    {
        using var workspace = new TempWorkspace();
        Write(workspace, "scan.bin", Dicom());

        CliResult created = workspace.Run("pdf", "create", "scan.pdf", "--from-images", "scan.bin", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        using var document = new Aspose.Pdf.Document(workspace.File("scan.pdf"));
        Assert.Single(Assert.Single(document.Pages).Resources.Images);
    }

    // TIFF and EMF are recognized by their signatures and embedded from the checked bytes, as
    // they were from the file.
    [Theory]
    [InlineData("scan.tif")]
    [InlineData("chart.emf")]
    public void CreateFromImages_EmbedsAFormatOnlyTheEngineReads(string name)
    {
        using var workspace = new TempWorkspace();
        Write(workspace, name, name.EndsWith(".tif", StringComparison.Ordinal) ? Tiff() : Emf());

        CliResult created = workspace.Run("pdf", "create", "out.pdf", "--from-images", name, "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        using var document = new Aspose.Pdf.Document(workspace.File("out.pdf"));
        Aspose.Pdf.Resources resources = Assert.Single(document.Pages).Resources;
        Assert.True(resources.Images.Count + resources.Forms.Count > 0, "The page holds no image.");
    }

    /// <summary>
    /// Each image input is reported by the id of the format its content is, from the image
    /// table: a <c>.jpg</c> as jpeg, a <c>.tif</c> as tiff, and PNG content under any name as png.
    /// </summary>
    [Fact]
    public void CreateFromImages_ReportsEachInputByItsDeclaredFormat()
    {
        using var workspace = new TempWorkspace();
        using (SkiaSharp.SKBitmap bitmap = SkiaSharp.SKBitmap.Decode(ResourceHttpServer.Image))
        using (SkiaSharp.SKData jpeg = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90))
        {
            Write(workspace, "photo.jpg", jpeg.ToArray());
        }

        Write(workspace, "scan.tif", Tiff());
        Write(workspace, "image.dat", ResourceHttpServer.Image);

        CliResult created = workspace.Run(
            "pdf", "create", "out.pdf", "--from-images", "photo.jpg", "scan.tif", "image.dat", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        Assert.Equal(
            ["jpeg", "tiff", "png"],
            JsonNode.Parse(created.StdOut)!["inputs"]!.AsArray().Select(static input => input!["format"]!.GetValue<string>()));
    }

    // A 2 x 2, 8-bit grayscale, uncompressed little-endian TIFF.
    private static byte[] Tiff()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write("II"u8);
        writer.Write((ushort)42);
        writer.Write(8u);
        (ushort Tag, ushort Type, uint Value)[] entries =
        [
            (256, 3, 2), (257, 3, 2), (258, 3, 8), (259, 3, 1), (262, 3, 1), (273, 4, 8 + 2 + (8 * 12) + 4), (278, 3, 2), (279, 4, 4),
        ];
        writer.Write((ushort)entries.Length);
        foreach ((ushort tag, ushort type, uint value) in entries)
        {
            writer.Write(tag);
            writer.Write(type);
            writer.Write(1u);
            writer.Write(value);
        }

        writer.Write(0u);
        writer.Write(new byte[] { 0x00, 0x55, 0xAA, 0xFF });
        writer.Flush();
        return stream.ToArray();
    }

    // An EMF that draws one rectangle: a header, EMR_RECTANGLE and EMR_EOF.
    private static byte[] Emf()
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        const int size = 88 + 24 + 20;
        writer.Write(1u);
        writer.Write(88u);
        foreach (int value in new[] { 0, 0, 99, 99, 0, 0, 2645, 2645 })
        {
            writer.Write(value);
        }

        writer.Write(0x464D4520u);
        writer.Write(0x10000u);
        writer.Write((uint)size);
        writer.Write(3u);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(1024);
        writer.Write(768);
        writer.Write(270);
        writer.Write(203);
        writer.Write(43u);
        writer.Write(24u);
        foreach (int value in new[] { 10, 10, 90, 90 })
        {
            writer.Write(value);
        }

        writer.Write(14u);
        writer.Write(20u);
        writer.Write(0u);
        writer.Write(16u);
        writer.Write(20u);
        writer.Flush();
        return stream.ToArray();
    }

    // A 2 x 2, 8-bit monochrome DICOM file in explicit VR little endian.
    private static byte[] Dicom()
    {
        var meta = new MemoryStream();
        Element(meta, 0x0002, 0x0001, "OB", [0x00, 0x01]);
        Element(meta, 0x0002, 0x0002, "UI", Uid("1.2.840.10008.5.1.4.1.1.7"));
        Element(meta, 0x0002, 0x0003, "UI", Uid("1.2.3.4"));
        Element(meta, 0x0002, 0x0010, "UI", Uid("1.2.840.10008.1.2.1"));
        var file = new MemoryStream();
        file.Write(new byte[128]);
        file.Write("DICM"u8);
        Element(file, 0x0002, 0x0000, "UL", BitConverter.GetBytes((uint)meta.Length));
        meta.WriteTo(file);
        Element(file, 0x0008, 0x0016, "UI", Uid("1.2.840.10008.5.1.4.1.1.7"));
        Element(file, 0x0008, 0x0018, "UI", Uid("1.2.3.4"));
        Element(file, 0x0028, 0x0002, "US", BitConverter.GetBytes((ushort)1));
        Element(file, 0x0028, 0x0004, "CS", Encoding.ASCII.GetBytes("MONOCHROME2 "));
        Element(file, 0x0028, 0x0010, "US", BitConverter.GetBytes((ushort)2));
        Element(file, 0x0028, 0x0011, "US", BitConverter.GetBytes((ushort)2));
        Element(file, 0x0028, 0x0100, "US", BitConverter.GetBytes((ushort)8));
        Element(file, 0x0028, 0x0101, "US", BitConverter.GetBytes((ushort)8));
        Element(file, 0x0028, 0x0102, "US", BitConverter.GetBytes((ushort)7));
        Element(file, 0x0028, 0x0103, "US", BitConverter.GetBytes((ushort)0));
        Element(file, 0x7FE0, 0x0010, "OB", [0x00, 0x55, 0xAA, 0xFF]);
        return file.ToArray();
    }

    private static byte[] Uid(string value) =>
        Encoding.ASCII.GetBytes(value.Length % 2 == 0 ? value : value + "\0");

    private static void Element(MemoryStream stream, ushort group, ushort element, string vr, byte[] value)
    {
        stream.Write(BitConverter.GetBytes(group));
        stream.Write(BitConverter.GetBytes(element));
        stream.Write(Encoding.ASCII.GetBytes(vr));
        if (vr is "OB" or "OW" or "SQ" or "UN" or "UT")
        {
            stream.Write([0, 0]);
            stream.Write(BitConverter.GetBytes((uint)value.Length));
        }
        else
        {
            stream.Write(BitConverter.GetBytes((ushort)value.Length));
        }

        stream.Write(value);
    }

    private static void AssertCorruptImage(CliResult result, string image)
    {
        Assert.True(result.ExitCode == 3, result.StdErr);
        JsonNode error = JsonNode.Parse(result.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Equal(image, error["details"]!["path"]!.GetValue<string>(), ignoreCase: true);
    }

    private static string Write(TempWorkspace workspace, string name, byte[] content)
    {
        string path = workspace.File(name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
