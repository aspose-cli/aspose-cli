using Xunit;

namespace Aspose.Cli.Product.Pdf.Tests;

/// <summary>
/// Every format the product declares is one the engine can serve: an input format is one the
/// engine opens, a convert format has a way to be written and a render format is written one
/// image per page.
/// </summary>
public sealed class PdfEngineFormatTests
{
    [Fact]
    public void EveryDeclaredFormat_HasAnEngineMapping()
    {
        var missing = new List<string>();
        foreach (FormatDescriptor format in PdfFormats.Definitions)
        {
            PdfEngineFormat? engine = PdfEngineFormats.All.SingleOrDefault(entry => entry.Id == format.Id);
            if (format.Uses.HasFlag(FormatUse.Input) && engine?.Read != true)
            {
                missing.Add($"{format.Id}: not read by the engine");
            }

            if (format.Uses.HasFlag(FormatUse.Convert) && !Writable(engine))
            {
                missing.Add($"{format.Id}: no engine write mapping");
            }

            if (format.Uses.HasFlag(FormatUse.Render) && (engine?.Write != PdfEngineWrite.PageImage || !Writable(engine)))
            {
                missing.Add($"{format.Id}: not an engine page image");
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EveryEngineMapping_IsADeclaredFormat()
    {
        HashSet<string> declared = PdfFormats.Definitions.Concat(PdfFormats.FormData)
            .Select(static format => format.Id).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(PdfEngineFormats.All, entry => !declared.Contains(entry.Id));
        Assert.Equal(
            PdfEngineFormats.All.Count,
            PdfEngineFormats.All.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A convert format said to read every page whatever --pages selects is one the engine writes
    /// by removing the pages left out; every other is written from the selected pages alone.
    /// </summary>
    [Fact]
    public void ConvertReadsEveryPage_IsTheEnginesWriteFromTheWholeDocument()
    {
        foreach (FormatDescriptor format in PdfFormats.Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)))
        {
            Assert.True(
                PdfFormats.ConvertReadsEveryPage(format) == (PdfEngineFormats.Of(format.Id).Write is PdfEngineWrite.Document or PdfEngineWrite.Archive),
                format.Id);
        }
    }

    /// <summary>
    /// A raster render format is one the engine writes with a page device, and a raster convert
    /// format is the multi-page TIFF; no vector format has a device, and every device has the
    /// encoding a grid drawn on its raster is written in.
    /// </summary>
    [Fact]
    public void RasterFormats_AreThePageDevicesAndTheTiff()
    {
        foreach (FormatDescriptor format in PdfFormats.Definitions)
        {
            PdfEngineFormat engine = PdfEngineFormats.Of(format.Id);
            if (format.Uses.HasFlag(FormatUse.Render))
            {
                Assert.True((engine.Device is not null) == format.Raster, $"{format.Id}: device and Raster disagree");
            }
            else
            {
                Assert.Null(engine.Device);
            }

            if (format.Uses.HasFlag(FormatUse.Convert))
            {
                Assert.True((engine.Write == PdfEngineWrite.Tiff) == format.Raster, $"{format.Id}: TIFF write and Raster disagree");
            }

            Assert.True((engine.Encoded is not null) == (engine.Device is not null), $"{format.Id}: device and encoding disagree");
        }
    }

    /// <summary>
    /// A page with a grid is encoded as the engine device encodes the page without one: the same
    /// image format and, for JPEG, the same quantization tables, which the quality sets.
    /// </summary>
    [Fact]
    public void GridEncoding_UsesTheDeviceQuality()
    {
        using var document = new Aspose.Pdf.Document();
        document.Pages.Add().Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Grid"));
        var resolution = new Aspose.Pdf.Devices.Resolution(72);
        foreach (PdfEngineFormat engine in PdfEngineFormats.All.Where(static entry => entry.Device is not null))
        {
            using var device = new MemoryStream();
            engine.Device!(resolution).Process(document.Pages[1], device);
            using var png = new MemoryStream();
            new Aspose.Pdf.Devices.PngDevice(resolution).Process(document.Pages[1], png);
            png.Position = 0;
            using SkiaSharp.SKBitmap bitmap = SkiaSharp.SKBitmap.Decode(png);
            using SkiaSharp.SKData encoded = bitmap.Encode(engine.Encoded!.Format, engine.Encoded.Quality);

            Assert.Equal(Signature(device.ToArray()), Signature(encoded.ToArray()));
            Assert.Equal(QuantizationTables(device.ToArray()), QuantizationTables(encoded.ToArray()));
        }
    }

    [Fact]
    public void EveryFormDataFormat_HasAnEngineExport()
    {
        foreach (FormatDescriptor format in PdfFormats.FormData)
        {
            Assert.True(PdfEngineFormats.Find(format.Id)?.Export is not null, format.Id);
        }

        Assert.All(
            PdfEngineFormats.All.Where(static entry => entry.Export is not null),
            static entry => Assert.Contains(PdfFormats.FormData, format => format.Id == entry.Id));
    }

    [Theory]
    [InlineData("pdfa-1b")]
    [InlineData("pdfa-2b")]
    [InlineData("pdfa-3b")]
    public void EveryValidateProfile_HasAnEngineProfile(string profile) =>
        Assert.NotNull(PdfEngineFormats.Archive(profile));

    private static string Signature(byte[] image) => Convert.ToHexString(image, 0, 3);

    /// <summary>The payloads of the DQT segments of a JPEG, in order; empty for any other image.</summary>
    private static string QuantizationTables(byte[] image)
    {
        var tables = new List<string>();
        if (image.Length < 2 || image[0] != 0xFF || image[1] != 0xD8)
        {
            return string.Empty;
        }

        int at = 2;
        while (at + 4 <= image.Length && image[at] == 0xFF && image[at + 1] != 0xDA)
        {
            int length = (image[at + 2] << 8) | image[at + 3];
            if (image[at + 1] == 0xDB)
            {
                tables.Add(Convert.ToHexString(image, at + 4, length - 2));
            }

            at += 2 + length;
        }

        Assert.NotEmpty(tables);
        return string.Join("|", tables);
    }

    /// <summary>Whether the entry names everything its write kind needs.</summary>
    private static bool Writable(PdfEngineFormat? engine) => engine?.Write switch
    {
        PdfEngineWrite.Document => engine.Save is not null,
        PdfEngineWrite.Archive => engine.Archive is not null,
        PdfEngineWrite.PageImage => engine.Device is not null || engine.Save is not null,
        PdfEngineWrite.Tiff or PdfEngineWrite.Text => true,
        _ => false,
    };
}
