
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Deny-by-default PDF format registry.</summary>
public static class PdfFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Routed("pdf", FormatUse.Input, ".pdf") with
        {
            Protectable = true,
            Recognition = FileFormatRecognition.Match(FileProbePattern.PdfHeader(), "PDF header"),
        },
        FormatDescriptor.Routed("docx", FormatUse.Convert, ".docx") with { MistakenFor = ["word"] },
        FormatDescriptor.Routed("xlsx", FormatUse.Convert, ".xlsx") with { MistakenFor = ["excel"] },
        FormatDescriptor.Routed("pptx", FormatUse.Convert, ".pptx") with { MistakenFor = ["powerpoint"] },
        FormatDescriptor.Routed("html", FormatUse.Convert, ".html", ".htm"),
        FormatDescriptor.Routed("epub", FormatUse.Convert, ".epub"),
        FormatDescriptor.Routed("txt", FormatUse.Convert, ".txt"),
        FormatDescriptor.Routed("md", FormatUse.Convert, ".md", ".markdown"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, ".xps"),
        FormatDescriptor.Routed("pdfa-1b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Routed("pdfa-2b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Routed("pdfa-3b", FormatUse.Convert, ".pdf") with { ChosenByExtension = false },
        FormatDescriptor.Routed("tiff", FormatUse.Convert, ".tiff", ".tif") with { Raster = true },
        FormatDescriptor.Routed("png", FormatUse.Render, ".png") with { Raster = true },
        FormatDescriptor.Routed("jpeg", FormatUse.Render, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Routed("svg", FormatUse.Render, ".svg"),
    ];

    /// <summary>
    /// Whether converting to <paramref name="format"/> reads every page of the document, whatever
    /// --pages selects: a document export or a PDF/A archive keeps the selected pages by removing
    /// the others, which reads them, while page images and page text read only the pages they show.
    /// </summary>
    internal static bool ConvertReadsEveryPage(FormatDescriptor format) => !format.Image && format.Id != "txt";

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
