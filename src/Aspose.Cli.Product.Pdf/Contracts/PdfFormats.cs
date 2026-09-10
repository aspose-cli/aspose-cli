namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Deny-by-default PDF format registry verified against Aspose.PDF 26.5.</summary>
public static class PdfFormats
{
    public static IReadOnlyList<string> LoadIds { get; } = ["pdf"];
    public static IReadOnlyList<string> RenderIds { get; } = ["png", "jpeg", "svg"];
    public static IReadOnlyList<string> CreateSourceIds { get; } = ["images", "html", "text", "markdown"];

    public static IReadOnlyList<string> ConvertIds { get; } =
        ["docx", "xlsx", "pptx", "html", "epub", "txt", "md", "svg", "xps",
         "pdfa-1b", "pdfa-2b", "pdfa-3b", "png", "jpeg", "tiff"];

    public static IReadOnlyList<string> ImageConvertIds { get; } = ["png", "jpeg", "tiff"];
    public static IReadOnlyList<string> PdfaConvertIds { get; } = ["pdfa-1b", "pdfa-2b", "pdfa-3b"];

    public static bool IsConvert(string id) => ConvertIds.Contains(id, StringComparer.Ordinal);
    public static bool IsRender(string id) => RenderIds.Contains(id, StringComparer.Ordinal);

    /// <summary>Returns the conventional extension for a public PDF format id.</summary>
    public static string Extension(string id) => id switch
    {
        "jpeg" => ".jpg",
        "pdfa-1b" or "pdfa-2b" or "pdfa-3b" => ".pdf",
        _ => "." + id,
    };
}
