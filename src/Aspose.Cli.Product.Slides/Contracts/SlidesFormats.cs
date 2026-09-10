namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Deny-by-default presentation format registry verified against Aspose.Slides 26.7.</summary>
public static class SlidesFormats
{
    public static IReadOnlyList<string> LoadIds { get; } =
        ["ppt", "pptx", "pptm", "pps", "ppsx", "ppsm", "pot", "potx", "potm", "odp", "otp", "fodp"];

    public static IReadOnlyList<string> ConvertIds { get; } =
        ["pptx", "ppt", "pptm", "odp", "pdf", "xps", "html", "html5", "png", "jpeg", "tiff", "gif", "svg", "md"];

    public static IReadOnlyList<string> RenderIds { get; } = ["png", "jpeg", "svg"];

    public static IReadOnlyList<string> WriteIds { get; } = ["pptx", "pptm"];

    public static string Extension(string id) => id switch
    {
        "html5" => ".html",
        "jpeg" => ".jpg",
        _ => "." + id,
    };
}
