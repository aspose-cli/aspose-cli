using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Facades;
using SkiaSharp;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The engine side of each public format id, in one table: how a document of that format is
/// read or written, with the save format, PDF/A profile or page device the engine uses, and
/// how the form data formats are exported. A product test keeps it in step with
/// <c>PdfFormats</c>, so a declared format the engine cannot read or write fails the build, not
/// the user.
/// </summary>
internal static class PdfEngineFormats
{
    /// <summary>Every format id the engine maps, with what it maps to.</summary>
    internal static IReadOnlyList<PdfEngineFormat> All { get; } =
    [
        new("pdf") { Read = true, Reopens = true },
        new("docx") { Write = PdfEngineWrite.Document, Save = SaveFormat.DocX, Lossy = true },
        new("xlsx") { Write = PdfEngineWrite.Document, Save = SaveFormat.Excel, Lossy = true },
        new("pptx") { Write = PdfEngineWrite.Document, Save = SaveFormat.Pptx, Lossy = true },
        new("html")
        {
            Write = PdfEngineWrite.Document,
            Save = SaveFormat.Html,
            // The HTML writer leaves <title> empty unless it is given the PDF title.
            Options = static document => new HtmlSaveOptions
            {
                PartsEmbeddingMode = HtmlSaveOptions.PartsEmbeddingModes.EmbedAllIntoHtml,
                Title = document.Info.Title ?? string.Empty,
            },
            Lossy = true,
            LossyHint = "Open the HTML in a browser and compare it with the PDF before relying on exact pagination, forms or annotations; review does not lay out HTML made from a PDF, so review the PDF itself.",
        },
        new("epub") { Write = PdfEngineWrite.Document, Save = SaveFormat.Epub, Lossy = true },
        new("md") { Write = PdfEngineWrite.Document, Save = SaveFormat.Markdown, Lossy = true },
        new("xps") { Write = PdfEngineWrite.Document, Save = SaveFormat.Xps },
        new("txt") { Write = PdfEngineWrite.Text, Lossy = true },
        new("pdfa-1b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_1B, AttachmentRule = "PDF/A-1 does not allow attachments" },
        new("pdfa-2b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_2B, AttachmentRule = "PDF/A-2 allows only attachments that are PDF/A documents" },
        new("pdfa-3b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_3B },
        PageRaster("png", SKEncodedImageFormat.Png, 100, static (resolution, _) => new PngDevice(resolution)),
        PageRaster("jpeg", SKEncodedImageFormat.Jpeg, 95, static (resolution, quality) => new JpegDevice(resolution, quality)),
        new("svg") { Write = PdfEngineWrite.PageImage, Save = SaveFormat.Svg },
        new("tiff") { Write = PdfEngineWrite.Tiff },
        new("json") { Export = static (form, stream) => form.ExportJson(stream, indented: true) },
        new("fdf") { Export = static (form, stream) => form.ExportFdf(stream) },
        new("xfdf") { Export = static (form, stream) => form.ExportXfdf(stream) },
    ];

    /// <summary>The entry of a format id.</summary>
    public static PdfEngineFormat Of(string formatId) =>
        Find(formatId) ?? throw Missing(formatId);

    /// <summary>The engine save format of a document export format id.</summary>
    public static SaveFormat Save(string formatId) =>
        Of(formatId).Save ?? throw Missing(formatId);

    /// <summary>The PDF/A profile of an archive format id; null when the id is not one.</summary>
    public static PdfFormat? Archive(string formatId) => Find(formatId)?.Archive;

    /// <summary>The entry of a format id; null when the engine does not map it.</summary>
    public static PdfEngineFormat? Find(string formatId) =>
        All.FirstOrDefault(format => string.Equals(format.Id, formatId, StringComparison.Ordinal));

    /// <summary>
    /// A bitmap page image: the engine device that rasterises a page and the encoding a page
    /// raster drawn on afterwards, such as a grid, is written in, at the same quality.
    /// </summary>
    private static PdfEngineFormat PageRaster(
        string id,
        SKEncodedImageFormat format,
        int quality,
        Func<Resolution, int, ImageDevice> device) => new(id)
        {
            Write = PdfEngineWrite.PageImage,
            Device = resolution => device(resolution, quality),
            Encoded = new PdfRasterEncoding(format, quality),
        };

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

    /// <summary>The encoding of a page raster the engine did not write itself, such as one with a grid drawn on it.</summary>
    public PdfRasterEncoding? Encoded { get; init; }

    /// <summary>The save options a document export needs beyond <see cref="Save"/>; null when the save format alone does.</summary>
    public Func<Document, SaveOptions>? Options { get; init; }

    /// <summary>Whether the conversion may not preserve every layout or interactive feature.</summary>
    public bool Lossy { get; init; }

    /// <summary>What to check before relying on a <see cref="Lossy"/> conversion; null for the general advice.</summary>
    public string? LossyHint { get; init; }

    /// <summary>
    /// Why an archive profile does not keep every attachment; null for a profile that keeps any
    /// attachment, so one it removed is one the engine could not make conform.
    /// </summary>
    public string? AttachmentRule { get; init; }

    /// <summary>
    /// Whether a saved file of this format is reopened to read its evaluation marks, because the
    /// document it was written from, such as one a facade signed, does not show them.
    /// </summary>
    public bool Reopens { get; init; }

    /// <summary>How the AcroForm data is exported in this form data format; null when it is not one.</summary>
    public Action<Form, Stream>? Export { get; init; }
}

/// <summary>How a page raster is encoded.</summary>
/// <param name="Format">The image encoding.</param>
/// <param name="Quality">The encoder quality, 0 to 100.</param>
internal sealed record PdfRasterEncoding(SKEncodedImageFormat Format, int Quality);
