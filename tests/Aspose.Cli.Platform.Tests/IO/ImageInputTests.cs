using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Platform.Tests.IO;

public sealed class ImageInputTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("image-input-").FullName;
    private readonly OperationDeadline _deadline = OperationDeadline.Start(null);

    public void Dispose()
    {
        _deadline.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("gif")]
    public void Read_ReturnsTheBytesOfAnIntactImage(string format)
    {
        byte[] image = Encode(format, 64, 48);
        string path = Write("image." + format, image);

        Assert.Equal(image, ImageInput.Read(Budgets().Inputs, path));
    }

    public static TheoryData<string, byte[]> Damaged()
    {
        byte[] png = Encode("png", 64, 48);
        byte[] jpeg = Encode("jpeg", 64, 48);
        byte[] webp = Encode("webp", 64, 48);
        return new()
        {
            { "text", Encoding.UTF8.GetBytes("not an image") },
            { "empty", [] },
            { "half a PNG", png[..(png.Length / 2)] },
            { "a PNG header alone", png[..33] },
            { "half a JPEG", jpeg[..(jpeg.Length / 2)] },
            { "half a WebP", webp[..(webp.Length / 2)] },
            { "markup that is not SVG", Encoding.UTF8.GetBytes("<html><body>not an image</body></html>") },
        };
    }

    [Theory]
    [MemberData(nameof(Damaged))]
    public void Read_RefusesAnImageInputThatIsNotAnIntactImageAsACorruptFileNamingIt(string kind, byte[] content)
    {
        string path = Write("image.png", content);

        CliException refused = Assert.Throws<CliException>(() => ImageInput.Read(Budgets().Inputs, path));

        Assert.True(refused.Code == ErrorCodes.FileCorrupt, kind);
        Assert.Contains(path, refused.Message, StringComparison.Ordinal);
        Assert.Equal(path, refused.Details!["path"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(Damaged))]
    public void Read_RefusesAnOperationImageThatIsNotAnIntactImageAsAnInvalidOperation(string kind, byte[] content)
    {
        string path = Write("image.png", content);

        OperationInvalidException refused = Assert.Throws<OperationInvalidException>(
            () => OperationImages.Read(Budgets().Inputs, path));

        Assert.True(refused.Message.Contains(path, StringComparison.Ordinal), kind);
        Assert.NotNull(refused.Hint);
    }

    // A compressed image would be an SVGZ, whose network addresses no one can check: refused as
    // NetworkReferenceGuard refuses it, with the same code for an input and an operation.
    [Fact]
    public void Read_RefusesACompressedImageAsTheNetworkGuardDoes()
    {
        string path = Write("image.svgz", [0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00]);

        CliException input = Assert.Throws<CliException>(() => ImageInput.Read(Budgets().Inputs, path));
        CliException operation = Assert.Throws<CliException>(() => OperationImages.Read(Budgets().Inputs, path));
        CliException guard = Assert.Throws<CliException>(
            () => NetworkReferenceGuard.EnsureNoneInImage(new MemoryStream(File.ReadAllBytes(path)), path));

        Assert.Equal(ErrorCodes.FeatureUnsupported, guard.Code);
        Assert.Equal(guard.Code, input.Code);
        Assert.Equal(guard.Code, operation.Code);
        Assert.Equal(guard.Message, input.Message);
    }

    public static TheoryData<string, byte[]> EngineFormats() => new()
    {
        { "svg", Encoding.UTF8.GetBytes("""<?xml version="1.0"?><svg xmlns="http://www.w3.org/2000/svg" width="9" height="9"/>""") },
        { "svg", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<svg:svg xmlns:svg=\"http://www.w3.org/2000/svg\"/>")).ToArray() },
        { "emf", Emf() },
        { "wmf", [0xD7, 0xCD, 0xC6, 0x9A, 0x00, 0x00, 0x00, 0x00] },
        { "wmf", [0x01, 0x00, 0x09, 0x00, 0x00, 0x03, 0x00, 0x00] },
        { "tif", [(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00] },
        { "tif", [(byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08] },
    };

    [Theory]
    [MemberData(nameof(EngineFormats))]
    public void Read_AcceptsAFormatOnlyTheEnginesReadBySignature(string extension, byte[] content)
    {
        string path = Write("image." + extension, content);

        Assert.Equal(content, OperationImages.Read(Budgets().Inputs, path));
    }

    [Fact]
    public void EngineFormatOf_NamesTheFormatEachSignatureMarks()
    {
        byte[] dicom = new byte[132];
        "DICM"u8.CopyTo(dicom.AsSpan(128));

        Assert.Equal(EngineImageFormat.Svg, ImageInput.EngineFormatOf(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")));
        Assert.Equal(EngineImageFormat.Emf, ImageInput.EngineFormatOf(Emf()));
        Assert.Equal(EngineImageFormat.Wmf, ImageInput.EngineFormatOf([0xD7, 0xCD, 0xC6, 0x9A, 0x00, 0x00]));
        Assert.Equal(EngineImageFormat.Tiff, ImageInput.EngineFormatOf([(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00]));
        Assert.Equal(EngineImageFormat.Dicom, ImageInput.EngineFormatOf(dicom));
        Assert.Equal(EngineImageFormat.None, ImageInput.EngineFormatOf(Encode("png", 4, 4)));
        Assert.Equal(EngineImageFormat.None, ImageInput.EngineFormatOf("<html/>"u8));
    }

    /// <summary>
    /// Every image the check accepts is named by its content, with the id of a format in the
    /// image table: the long name, whatever the file's extension.
    /// </summary>
    [Fact]
    public void FormatOf_NamesEveryAcceptedImage()
    {
        byte[] dicom = new byte[132];
        "DICM"u8.CopyTo(dicom.AsSpan(128));
        byte[] pict = new byte[530];
        pict[522] = 0x00;
        pict[523] = 0x11;
        pict[524] = 0x02;
        pict[525] = 0xFF;
        (byte[] Content, string Format)[] images =
        [
            (Encode("png", 4, 4), "png"),
            (Encode("jpeg", 4, 4), "jpeg"),
            (Encode("gif", 1, 1), "gif"),
            (Encode("bmp", 4, 4), "bmp"),
            (Encode("webp", 4, 4), "webp"),
            (Ico(Encode("png", 4, 4)), "ico"),
            ([(byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00], "tiff"),
            (Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"), "svg"),
            (Emf(), "emf"),
            ([0xD7, 0xCD, 0xC6, 0x9A, 0x00, 0x00], "wmf"),
            ([0xFF, 0x4F, 0xFF, 0x51, 0x00, 0x00], "jpeg2000"),
            (pict, "pict"),
            (dicom, "dicom"),
        ];

        Assert.All(images, image => Assert.Equal(image.Format, ImageInput.FormatOf(image.Content)?.Id));
        Assert.Equal(images.Select(static image => image.Format).Order(), ImageInput.Formats.Select(static format => format.Id).Order());
        Assert.Null(ImageInput.FormatOf("<html/>"u8));
        Assert.Null(ImageInput.FormatOf([]));
    }

    [Fact]
    public void Read_DecodesALargeScanlineImageWithinASmallMemoryBudget()
    {
        byte[] image = Encode("jpeg", 2000, 1500);
        string path = Write("large.jpeg", image);
        ResourceBudgetLedger budgets = Budgets(memory: image.Length + (2L << 20), raster: 1000);

        Assert.Equal(image, ImageInput.Read(budgets.Inputs, path));
    }

    [Fact]
    public void Read_DecodesAWebPAtAScaleWithinASmallMemoryBudget()
    {
        byte[] image = Encode("webp", 1000, 1000);
        string path = Write("large.webp", image);
        ResourceBudgetLedger budgets = Budgets(memory: image.Length + (2L << 20), raster: 1000);

        Assert.Equal(image, ImageInput.Read(budgets.Inputs, path));
    }

    // A 20-megapixel image takes 80 MB as 32-bit pixels, more than the default memory budget;
    // the check decodes all its data without holding them.
    [Fact]
    public void Read_DecodesA20MegapixelPngWithinTheDefaultBudget()
    {
        byte[] image = Gradient(5000, 4000);
        string path = Write("large.png", image);

        Assert.Equal(image, ImageInput.Read(Budgets(raster: 1000).Inputs, path));
    }

    [Fact]
    public void Read_RefusesA20MegapixelPngCutInItsPixelData()
    {
        byte[] image = Gradient(5000, 4000);
        string path = Write("cut.png", image[..(image.Length / 2)]);

        CliException refused = Assert.Throws<CliException>(() => ImageInput.Read(Budgets().Inputs, path));

        Assert.Equal(ErrorCodes.FileCorrupt, refused.Code);
    }

    // Skia de-interlaces an interlaced PNG, and libjpeg keeps a progressive JPEG's coefficients,
    // in a buffer of the whole image the decoder allocates itself.
    [Fact]
    public void Read_HoldsAnInterlacedPngToTheRasterBudget()
    {
        string interlaced = Write("interlaced.png", GrayPng(20, 20, interlaced: true));
        string sequential = Write("sequential.png", GrayPng(20, 20, interlaced: false));

        CliException refused = Assert.Throws<CliException>(() => ImageInput.Read(Budgets(raster: 100).Inputs, interlaced));
        ImageInput.Read(Budgets(raster: 100).Inputs, sequential);
        ImageInput.Read(Budgets().Inputs, interlaced);

        Assert.Equal(ErrorCodes.InputBudgetExceeded, refused.Code);
        Assert.Contains(ResourceBudgetKinds.RasterPixels, refused.Message, StringComparison.Ordinal);
    }

    // Skia writes no progressive JPEG; marking a baseline frame progressive (SOF0 to SOF2) is
    // enough, because the frame is held to the raster budget before any data is decoded.
    [Fact]
    public void Read_HoldsAProgressiveJpegToTheRasterBudget()
    {
        // Large enough that a decode at a reduced scale would otherwise be tried.
        byte[] baseline = Encode("jpeg", 1000, 1000);
        byte[] progressive = (byte[])baseline.Clone();
        int frame = progressive.AsSpan().IndexOf((ReadOnlySpan<byte>)[0xFF, 0xC0]);
        Assert.True(frame > 0);
        progressive[frame + 1] = 0xC2;

        CliException refused = Assert.Throws<CliException>(
            () => ImageInput.Read(Budgets(raster: 1000).Inputs, Write("progressive.jpeg", progressive)));
        ImageInput.Read(Budgets(raster: 1000).Inputs, Write("baseline.jpeg", baseline));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, refused.Code);
        Assert.Contains(ResourceBudgetKinds.RasterPixels, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ChecksAWholeFrameDecodeAgainstTheRasterBudget()
    {
        // A 20 x 20 GIF; GIF decodes only into a buffer of the whole frame.
        byte[] gif = Encode("gif", 1, 1);
        gif[6] = gif[8] = gif[32] = gif[34] = 20;
        string path = Write("frame.gif", gif);
        var budgets = new ResourceBudgetLedger(_deadline, new Dictionary<string, long> { [ResourceBudgetKinds.RasterPixels] = 100 });

        CliException refused = Assert.Throws<CliException>(() => ImageInput.Read(budgets.Inputs, path));

        Assert.Equal(ErrorCodes.InputBudgetExceeded, refused.Code);
        Assert.Contains(ResourceBudgetKinds.RasterPixels, refused.Message, StringComparison.Ordinal);
    }

    // A raster limit below an image's pixels makes a decode that holds the whole frame fail, so
    // a test that sets one proves its image decodes in parts.
    private ResourceBudgetLedger Budgets(long? memory = null, long? raster = null)
    {
        var limits = new Dictionary<string, long>();
        if (memory is { } memoryLimit)
        {
            limits[ResourceBudgetKinds.MemoryBufferBytes] = memoryLimit;
        }

        if (raster is { } rasterLimit)
        {
            limits[ResourceBudgetKinds.RasterPixels] = rasterLimit;
        }

        return new ResourceBudgetLedger(_deadline, limits);
    }

    private string Write(string name, byte[] content)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] Emf()
    {
        byte[] header = new byte[88];
        header[0] = 0x01;
        header[4] = 88;
        " EMF"u8.CopyTo(header.AsSpan(40));
        return header;
    }

    // An icon holding one PNG.
    private static byte[] Ico(byte[] png)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)4);
        writer.Write((byte)4);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(png.Length);
        writer.Write(22);
        writer.Write(png);
        return stream.ToArray();
    }

    // An 8-bit grayscale PNG, Adam7-interlaced or not; Skia writes no interlaced PNG.
    private static byte[] GrayPng(int width, int height, bool interlaced)
    {
        (int X, int Y, int DX, int DY)[] passes = interlaced
            ? [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)]
            : [(0, 0, 1, 1)];
        var raw = new MemoryStream();
        foreach ((int x, int y, int dx, int dy) in passes)
        {
            int columns = width > x ? (width - x + dx - 1) / dx : 0;
            int rows = height > y ? (height - y + dy - 1) / dy : 0;
            for (int row = 0; row < rows && columns > 0; row++)
            {
                raw.WriteByte(0);
                for (int column = 0; column < columns; column++)
                {
                    raw.WriteByte((byte)((row * 31) + (column * 17)));
                }
            }
        }

        var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.WriteTo(zlib);
        }

        var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        byte[] header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[12] = interlaced ? (byte)1 : (byte)0;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(MemoryStream png, string type, byte[] data)
    {
        byte[] length = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        png.Write(length);
        byte[] typed = [.. Encoding.ASCII.GetBytes(type), .. data];
        png.Write(typed);
        byte[] crc = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typed));
        png.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private static byte[] Gradient(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(width, height), [SKColors.Navy, SKColors.Orange, SKColors.Teal], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // Noise, so that every part of the encoded data carries pixels and a cut loses some.
    private static byte[] Encode(string format, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var random = new Random(7);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
            }
        }

        if (format == "gif")
        {
            // Skia encodes no GIF: one white pixel.
            return [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
                0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B];
        }

        if (format == "bmp")
        {
            return Bmp(bitmap);
        }

        SKEncodedImageFormat encoded = format switch
        {
            "png" => SKEncodedImageFormat.Png,
            "jpeg" => SKEncodedImageFormat.Jpeg,
            "webp" => SKEncodedImageFormat.Webp,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        using SKData data = bitmap.Encode(encoded, 90)
            ?? throw new InvalidOperationException($"Skia cannot encode {format}.");
        return data.ToArray();
    }

    // Skia encodes no BMP: a 32-bit bottom-up BITMAPINFOHEADER file.
    private static byte[] Bmp(SKBitmap bitmap)
    {
        int pixels = bitmap.Width * bitmap.Height * 4;
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + pixels);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(bitmap.Width);
        writer.Write(bitmap.Height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(pixels);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        for (int y = bitmap.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                writer.Write(color.Blue);
                writer.Write(color.Green);
                writer.Write(color.Red);
                writer.Write(color.Alpha);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }
}
