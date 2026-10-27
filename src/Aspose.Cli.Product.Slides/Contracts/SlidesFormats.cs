
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Deny-by-default presentation format registry.</summary>
public static class SlidesFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Routed("ppt", FormatUse.Input | FormatUse.Convert, ".ppt") with { Recognition = SlidesFormatRecognition.CompoundPresentation },
        // pptx leads the convert formats.
        FormatDescriptor.Routed("pptx", FormatUse.Input | FormatUse.Convert, ".pptx") with { Protectable = true, ConvertOrder = -1, Recognition = SlidesFormatRecognition.OpenXmlPresentation },
        FormatDescriptor.Routed("pptm", FormatUse.Input | FormatUse.Convert, ".pptm") with { Protectable = true, Recognition = SlidesFormatRecognition.OpenXmlPresentation },
        FormatDescriptor.Routed("pps", FormatUse.Input, ".pps") with { Recognition = SlidesFormatRecognition.CompoundPresentation },
        FormatDescriptor.Routed("ppsx", FormatUse.Input, ".ppsx") with { Recognition = SlidesFormatRecognition.OpenXmlPresentation },
        FormatDescriptor.Routed("ppsm", FormatUse.Input, ".ppsm") with { Recognition = SlidesFormatRecognition.OpenXmlPresentation },
        FormatDescriptor.Routed("pot", FormatUse.Input, ".pot") with { Recognition = SlidesFormatRecognition.CompoundPresentation },
        FormatDescriptor.Routed("potx", FormatUse.Input, ".potx") with { Recognition = SlidesFormatRecognition.OpenXmlPresentation },
        FormatDescriptor.Routed("potm", FormatUse.Input, ".potm") with { Recognition = SlidesFormatRecognition.OpenXmlPresentation },
        FormatDescriptor.Routed("odp", FormatUse.Input | FormatUse.Convert, ".odp") with { Recognition = SlidesFormatRecognition.OpenDocumentPresentation },
        FormatDescriptor.Routed("otp", FormatUse.Input, ".otp") with { Recognition = SlidesFormatRecognition.OpenDocumentPresentation },
        FormatDescriptor.Routed("fodp", FormatUse.Input, ".fodp") with
        {
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.All(
                    FileProbePattern.Any(
                        FileProbePattern.TextStartsIgnoringBomAndWhitespace("<?xml"),
                        FileProbePattern.TextStartsIgnoringBomAndWhitespace("<")),
                    FileProbePattern.TextContains("office:presentation")),
                "flat OpenDocument presentation root"),
        },
        FormatDescriptor.Routed("pdf", FormatUse.Convert, ".pdf"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, ".xps"),
        FormatDescriptor.Routed("html", FormatUse.Convert, ".html", ".htm"),
        FormatDescriptor.Routed("html5", FormatUse.Convert, ".html", ".htm") with { CompanionFiles = true },
        FormatDescriptor.Routed("png", FormatUse.Render, ".png") with { Raster = true },
        FormatDescriptor.Routed("jpeg", FormatUse.Render, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Routed("tiff", FormatUse.Convert, ".tiff", ".tif") with { Raster = true },
        FormatDescriptor.Routed("gif", FormatUse.Convert, ".gif") with { Raster = true },
        FormatDescriptor.Routed("svg", FormatUse.Render, ".svg"),
        FormatDescriptor.Routed("md", FormatUse.Convert, ".md", ".markdown"),
    ];

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    /// <summary>The presentation formats create and edit write.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Id is "pptx" or "pptm")];
}
