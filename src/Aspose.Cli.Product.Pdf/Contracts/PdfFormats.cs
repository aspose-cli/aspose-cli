using Aspose.Cli.Sdk.Extensibility;

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
        FormatDescriptor.Routed("html", FormatUse.Convert, null, 3, null, ".html"),
        FormatDescriptor.Routed("epub", FormatUse.Convert, null, 4, null, ".epub"),
        FormatDescriptor.Routed("txt", FormatUse.Convert, null, 5, null, ".txt"),
        FormatDescriptor.Routed("md", FormatUse.Convert, null, 6, null, ".md"),
        FormatDescriptor.Routed("svg", FormatUse.Convert | FormatUse.Render, null, 7, 2, ".svg"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, null, 8, null, ".xps"),
        FormatDescriptor.Routed("pdfa-1b", FormatUse.Convert, null, 9, null, ".pdf"),
        FormatDescriptor.Routed("pdfa-2b", FormatUse.Convert, null, 10, null, ".pdf"),
        FormatDescriptor.Routed("pdfa-3b", FormatUse.Convert, null, 11, null, ".pdf"),
        FormatDescriptor.Routed("png", FormatUse.Convert | FormatUse.Render, null, 12, 0, ".png"),
        FormatDescriptor.Routed("jpeg", FormatUse.Convert | FormatUse.Render, null, 13, 1, ".jpg", ".jpeg"),
        FormatDescriptor.Routed("tiff", FormatUse.Convert, null, 14, null, ".tiff"),
    ], PdfFormatRecognition.Rules);

    public static IReadOnlyList<string> RenderIds { get; } =
        Definitions.IdsFor(FormatUse.Render);

    public static IReadOnlyList<string> ConvertIds { get; } =
        Definitions.IdsFor(FormatUse.Convert);

    public static IReadOnlyList<string> ImageConvertIds { get; } = ["png", "jpeg", "tiff"];
    public static IReadOnlyList<string> PdfaConvertIds { get; } = ["pdfa-1b", "pdfa-2b", "pdfa-3b"];

    public static bool IsConvert(string id) => ConvertIds.Contains(id, StringComparer.Ordinal);
    public static bool IsRender(string id) => RenderIds.Contains(id, StringComparer.Ordinal);
}
