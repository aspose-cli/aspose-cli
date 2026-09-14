namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Deny-by-default presentation format registry.</summary>
public static class SlidesFormats
{
    public static IReadOnlyList<string> LoadIds { get; } =
        ["ppt", "pptx", "pptm", "pps", "ppsx", "ppsm", "pot", "potx", "potm", "odp", "otp", "fodp"];

    public static IReadOnlyList<string> ConvertIds { get; } =
        ["pptx", "ppt", "pptm", "odp", "pdf", "xps", "html", "html5", "png", "jpeg", "tiff", "gif", "svg", "md"];

    public static IReadOnlyList<string> WriteIds { get; } = ["pptx", "pptm"];
}
