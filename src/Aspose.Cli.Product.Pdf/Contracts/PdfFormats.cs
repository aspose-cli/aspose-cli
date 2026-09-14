namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Deny-by-default PDF format registry.</summary>
public static class PdfFormats
{
    public static IReadOnlyList<string> RenderIds { get; } = ["png", "jpeg", "svg"];

    public static IReadOnlyList<string> ConvertIds { get; } =
        ["docx", "xlsx", "pptx", "html", "epub", "txt", "md", "svg", "xps",
         "pdfa-1b", "pdfa-2b", "pdfa-3b", "png", "jpeg", "tiff"];

    public static IReadOnlyList<string> ImageConvertIds { get; } = ["png", "jpeg", "tiff"];
    public static IReadOnlyList<string> PdfaConvertIds { get; } = ["pdfa-1b", "pdfa-2b", "pdfa-3b"];

    public static bool IsConvert(string id) => ConvertIds.Contains(id, StringComparer.Ordinal);
    public static bool IsRender(string id) => RenderIds.Contains(id, StringComparer.Ordinal);
}
