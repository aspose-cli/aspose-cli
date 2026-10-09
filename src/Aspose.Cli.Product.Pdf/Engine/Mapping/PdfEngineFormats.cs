using Aspose.Pdf;
using Aspose.Pdf.Devices;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The engine side of each public format id, in one table: how a document of that format is
/// read or written, with the save format, PDF/A profile or page device the engine uses. A
/// product test keeps it in step with <c>PdfFormats</c>, so a declared format the engine
/// cannot read or write fails the build, not the user.
/// </summary>
internal static class PdfEngineFormats
{
    /// <summary>Every format id the engine maps, with what it maps to.</summary>
    internal static IReadOnlyList<PdfEngineFormat> All { get; } =
    [
        new("pdf") { Read = true },
        new("docx") { Write = PdfEngineWrite.Document, Save = SaveFormat.DocX, Lossy = true },
        new("xlsx") { Write = PdfEngineWrite.Document, Save = SaveFormat.Excel, Lossy = true },
        new("pptx") { Write = PdfEngineWrite.Document, Save = SaveFormat.Pptx, Lossy = true },
        new("html") { Write = PdfEngineWrite.Document, Save = SaveFormat.Html, Lossy = true },
        new("epub") { Write = PdfEngineWrite.Document, Save = SaveFormat.Epub, Lossy = true },
        new("md") { Write = PdfEngineWrite.Document, Save = SaveFormat.Markdown, Lossy = true },
        new("xps") { Write = PdfEngineWrite.Document, Save = SaveFormat.Xps },
        new("txt") { Write = PdfEngineWrite.Text, Lossy = true },
        new("pdfa-1b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_1B },
        new("pdfa-2b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_2B },
        new("pdfa-3b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_3B },
        new("png") { Write = PdfEngineWrite.PageImage, Device = static resolution => new PngDevice(resolution) },
        new("jpeg") { Write = PdfEngineWrite.PageImage, Device = static resolution => new JpegDevice(resolution, 95) },
        new("svg") { Write = PdfEngineWrite.PageImage, Save = SaveFormat.Svg },
        new("tiff") { Write = PdfEngineWrite.Tiff },
    ];

    /// <summary>The entry of a format id.</summary>
    public static PdfEngineFormat Of(string formatId) =>
        Find(formatId) ?? throw Missing(formatId);

    /// <summary>The engine save format of a document export format id.</summary>
    public static SaveFormat Save(string formatId) =>
        Of(formatId).Save ?? throw Missing(formatId);

    /// <summary>The PDF/A profile of an archive format id; null when the id is not one.</summary>
    public static PdfFormat? Archive(string formatId) => Find(formatId)?.Archive;

    /// <summary><c>true</c> for page images that are bitmaps, where DPI applies.</summary>
    public static bool IsRaster(string formatId) => Find(formatId)?.Device is not null;

    private static PdfEngineFormat? Find(string formatId) =>
        All.FirstOrDefault(format => string.Equals(format.Id, formatId, StringComparison.Ordinal));

    private static ArgumentOutOfRangeException Missing(string formatId) =>
        new(nameof(formatId), formatId, "Format id is missing from the engine format table.");
}

/// <summary>How the engine writes a format.</summary>
internal enum PdfEngineWrite
{
    /// <summary>Not written.</summary>
    None,

    /// <summary>The selected pages are saved as one document in <see cref="PdfEngineFormat.Save"/>.</summary>
    Document,

    /// <summary>The selected pages are converted to the <see cref="PdfEngineFormat.Archive"/> profile.</summary>
    Archive,

    /// <summary>Each page is written as its own image.</summary>
    PageImage,

    /// <summary>The selected pages are written as one multi-page TIFF.</summary>
    Tiff,

    /// <summary>The text of the selected pages is written as UTF-8.</summary>
    Text,
}

/// <summary>What one public format id is to the engine.</summary>
/// <param name="Id">The public format id.</param>
internal sealed record PdfEngineFormat(string Id)
{
    /// <summary>Whether the engine opens documents of this format.</summary>
    public bool Read { get; init; }

    /// <summary>How the engine writes this format.</summary>
    public PdfEngineWrite Write { get; init; }

    /// <summary>The engine save format of a document export or a vector page image.</summary>
    public SaveFormat? Save { get; init; }

    /// <summary>The PDF/A profile an archive conforms to.</summary>
    public PdfFormat? Archive { get; init; }

    /// <summary>The device that rasterises one page at a resolution; null when not a bitmap.</summary>
    public Func<Resolution, ImageDevice>? Device { get; init; }

    /// <summary>Whether the conversion may not preserve every layout or interactive feature.</summary>
    public bool Lossy { get; init; }
}
