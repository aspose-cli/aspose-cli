using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Deny-by-default PDF format registry.</summary>
public static class PdfFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Routed("pdf", FormatUse.Input, 0, null, null, ".pdf"),
        FormatDescriptor.Routed("docx", FormatUse.Convert, null, 0, null, ".docx"),
        FormatDescriptor.Routed("xlsx", FormatUse.Convert, null, 1, null, ".xlsx"),
        FormatDescriptor.Routed("pptx", FormatUse.Convert, null, 2, null, ".pptx"),
        FormatDescriptor.Routed("html", FormatUse.Convert, null, 3, null, ".html", ".htm"),
        FormatDescriptor.Routed("epub", FormatUse.Convert, null, 4, null, ".epub"),
        FormatDescriptor.Routed("txt", FormatUse.Convert, null, 5, null, ".txt"),
        FormatDescriptor.Routed("md", FormatUse.Convert, null, 6, null, ".md", ".markdown"),
        FormatDescriptor.Routed("svg", FormatUse.Convert | FormatUse.Render, null, 7, 2, ".svg"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, null, 8, null, ".xps"),
        FormatDescriptor.Routed("pdfa-1b", FormatUse.Convert, null, 9, null, ".pdf"),
        FormatDescriptor.Routed("pdfa-2b", FormatUse.Convert, null, 10, null, ".pdf"),
        FormatDescriptor.Routed("pdfa-3b", FormatUse.Convert, null, 11, null, ".pdf"),
        FormatDescriptor.Routed("png", FormatUse.Convert | FormatUse.Render, null, 12, 0, ".png"),
        FormatDescriptor.Routed("jpeg", FormatUse.Convert | FormatUse.Render, null, 13, 1, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Routed("tiff", FormatUse.Convert, null, 14, null, ".tiff", ".tif"),
    ], PdfFormatRecognition.Rules);

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
}
