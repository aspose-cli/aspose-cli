using Aspose.Pdf;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Facades;

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
        new("pdf") { Read = true, Write = PdfEngineWrite.Document, Save = SaveFormat.Pdf },
        new("docx") { Write = PdfEngineWrite.Document, Save = SaveFormat.DocX },
        new("xlsx") { Write = PdfEngineWrite.Document, Save = SaveFormat.Excel },
        new("pptx") { Write = PdfEngineWrite.Document, Save = SaveFormat.Pptx },
        new("html")
        {
            // Imported with HtmlLoadOptions; only convert reads it.
            Read = true,
            Write = PdfEngineWrite.Document,
            Save = SaveFormat.Html,
            // One file: the publication publishes one, so the images, fonts and styles go inside it.
            Options = static pages => new HtmlSaveOptions
            {
                PartsEmbeddingMode = HtmlSaveOptions.PartsEmbeddingModes.EmbedAllIntoHtml,
                ExplicitListOfSavedPages = pages is null ? null : [.. pages],
            },
        },
        new("epub") { Write = PdfEngineWrite.Document, Save = SaveFormat.Epub },
        new("md") { Write = PdfEngineWrite.Document, Save = SaveFormat.Markdown },
        new("xps") { Write = PdfEngineWrite.Document, Save = SaveFormat.Xps },
        new("txt") { Write = PdfEngineWrite.Text },
        new("pdfa-1b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_1B },
        new("pdfa-2b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_2B },
        new("pdfa-3b") { Write = PdfEngineWrite.Archive, Archive = PdfFormat.PDF_A_3B },
        new("png") { Write = PdfEngineWrite.PageImage, Device = static resolution => new PngDevice(resolution) },
        new("jpeg") { Write = PdfEngineWrite.PageImage, Device = static resolution => new JpegDevice(resolution) },
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

    private static ArgumentOutOfRangeException Missing(string formatId) =>
        new(nameof(formatId), formatId, "Format id is missing from the engine format table.");
}

/// <summary>How the engine writes a format.</summary>
internal enum PdfEngineWrite
{
    /// <summary>Not written.</summary>
    None,

    /// <summary>The document is saved in <see cref="PdfEngineFormat.Save"/>.</summary>
    Document,

    /// <summary>The document is converted to the <see cref="PdfEngineFormat.Archive"/> profile and saved.</summary>
    Archive,

    /// <summary>Each page is written as its own image.</summary>
    PageImage,

    /// <summary>The selected pages are written as one multi-page TIFF.</summary>
    Tiff,

    /// <summary>The engine's text of every page is written as UTF-8.</summary>
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

    /// <summary>The save options of a document export, with the pages --pages selects or null; null when the save format alone does.</summary>
    public Func<IReadOnlyList<int>?, SaveOptions>? Options { get; init; }

    /// <summary>How the AcroForm data is exported in this form data format; null when it is not one.</summary>
    public Action<Form, Stream>? Export { get; init; }
}
