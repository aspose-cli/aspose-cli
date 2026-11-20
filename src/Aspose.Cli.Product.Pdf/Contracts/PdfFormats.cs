
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Deny-by-default PDF format registry.</summary>
public static class PdfFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Routed("pdf", FormatUse.Input | FormatUse.Convert, ".pdf") with
        {
            Protectable = true,
            Recognition = FileFormatRecognition.Match(FileProbePattern.PdfHeader(), "PDF header"),
        },
        // Words rebuilds a PDF as flowing paragraphs; this product's export keeps each line in a
        // positioned frame that is hard to edit.
        FormatDescriptor.Routed("docx", FormatUse.Convert, ".docx") with { },
        FormatDescriptor.Routed("xlsx", FormatUse.Convert, ".xlsx") with { },
        FormatDescriptor.Routed("pptx", FormatUse.Convert, ".pptx") with { },
        // Words owns the generic route of an HTML file; pdf convert reads it, and nothing else does.
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, false, ".html", ".htm"),
        FormatDescriptor.Routed("epub", FormatUse.Convert, ".epub"),
        FormatDescriptor.Routed("txt", FormatUse.Convert, ".txt"),
        FormatDescriptor.Routed("md", FormatUse.Convert, ".md", ".markdown") with { CompanionFiles = true },
        FormatDescriptor.Routed("xps", FormatUse.Convert, ".xps"),
        FormatDescriptor.Routed("pdfa-1b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Routed("pdfa-2b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Routed("pdfa-3b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Routed("tiff", FormatUse.Convert, ".tiff", ".tif") with { Raster = true },
        FormatDescriptor.Routed("png", FormatUse.Render, ".png") with { Raster = true },
        FormatDescriptor.Routed("jpeg", FormatUse.Render, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Routed("svg", FormatUse.Render, ".svg"),
    ];

    /// <summary>Whether a format id is a PDF/A profile, which convert writes with Document.Convert.</summary>
    internal static bool IsArchive(string formatId) => formatId.StartsWith("pdfa-", StringComparison.Ordinal);

    /// <summary>Whether an input is Markdown by its name, which no PDF command reads.</summary>
    internal static bool IsMarkdownInput(string path) =>
        Definitions.NamedBy(path).Any(static format => format.Id == "md");

    /// <summary>Whether an input is HTML by its name, which only convert reads.</summary>
    internal static bool IsHtmlInput(string path) =>
        Definitions.NamedBy(path).Any(static format => format.Id == "html");

    /// <summary>The PDF document format, which create, merge, sign and edit write.</summary>
    internal static IReadOnlyList<FormatDescriptor> Document { get; } =
        [.. Definitions.Where(static format => format.Id == "pdf")];

    /// <summary>The formats <c>extract --what forms</c> exports form data in.</summary>
    internal static IReadOnlyList<FormatDescriptor> FormData { get; } =
    [
        new("json", FormatUse.Convert, ".json"),
        new("fdf", FormatUse.Convert, ".fdf"),
        new("xfdf", FormatUse.Convert, ".xfdf"),
    ];

    /// <summary>The formats <c>sign</c> reads a signing certificate in: PKCS #12.</summary>
    internal static IReadOnlyList<FormatDescriptor> Certificates { get; } =
    [
        new("pfx", FormatUse.Input, ".pfx"),
        new("p12", FormatUse.Input, ".p12"),
    ];
}
