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
            if ((format.Uses & (FormatUse.Input | FormatUse.ConvertInput)) != 0 && engine?.Read != true)
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
