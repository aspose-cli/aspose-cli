
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Deny-by-default PDF format registry.</summary>
public static class PdfFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, ".pdf") with { Protectable = true },
        // Words rebuilds a PDF as flowing paragraphs; this product's export keeps each line in a
        // positioned frame that is hard to edit.
        FormatDescriptor.Declare("docx", FormatUse.Convert, ".docx"),
        FormatDescriptor.Declare("xlsx", FormatUse.Convert, ".xlsx"),
        FormatDescriptor.Declare("pptx", FormatUse.Convert, ".pptx"),
        // pdf convert reads an HTML page, and no other PDF command does.
        FormatDescriptor.Declare("html", FormatUse.ConvertInput | FormatUse.Convert, ".html", ".htm"),
        FormatDescriptor.Declare("epub", FormatUse.Convert, ".epub"),
        FormatDescriptor.Declare("txt", FormatUse.Convert, ".txt"),
        FormatDescriptor.Declare("md", FormatUse.Convert, ".md", ".markdown") with { CompanionFiles = true },
        FormatDescriptor.Declare("xps", FormatUse.Convert, ".xps"),
        FormatDescriptor.Declare("pdfa-1b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Declare("pdfa-2b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Declare("pdfa-3b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Declare("tiff", FormatUse.Convert, ".tiff", ".tif") with { Raster = true },
        FormatDescriptor.Declare("png", FormatUse.Render, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, ".svg"),
    ];

    /// <summary>Whether a format id is a PDF/A profile, which convert writes with Document.Convert.</summary>
    internal static bool IsArchive(string formatId) => formatId.StartsWith("pdfa-", StringComparison.Ordinal);

    /// <summary>Whether an input is HTML by its name: a format only convert reads, which it imports.</summary>
    internal static bool IsHtmlInput(string path) =>
        Definitions.NamedBy(path).Any(static format => format.Uses.HasFlag(FormatUse.ConvertInput));

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
