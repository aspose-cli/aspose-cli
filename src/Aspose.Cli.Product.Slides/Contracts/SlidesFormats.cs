using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Deny-by-default presentation format registry.</summary>
public static class SlidesFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Routed("ppt", FormatUse.Input | FormatUse.Convert, 0, 1, null, ".ppt"),
        FormatDescriptor.Routed("pptx", FormatUse.Input | FormatUse.Convert, 1, 0, null, ".pptx") with { Protectable = true },
        FormatDescriptor.Routed("pptm", FormatUse.Input | FormatUse.Convert, 2, 2, null, ".pptm") with { Protectable = true },
        FormatDescriptor.Routed("pps", FormatUse.Input, 3, null, null, ".pps"),
        FormatDescriptor.Routed("ppsx", FormatUse.Input, 4, null, null, ".ppsx"),
        FormatDescriptor.Routed("ppsm", FormatUse.Input, 5, null, null, ".ppsm"),
        FormatDescriptor.Routed("pot", FormatUse.Input, 6, null, null, ".pot"),
        FormatDescriptor.Routed("potx", FormatUse.Input, 7, null, null, ".potx"),
        FormatDescriptor.Routed("potm", FormatUse.Input, 8, null, null, ".potm"),
        FormatDescriptor.Routed("odp", FormatUse.Input | FormatUse.Convert, 9, 3, null, ".odp"),
        FormatDescriptor.Routed("otp", FormatUse.Input, 10, null, null, ".otp"),
        FormatDescriptor.Routed("fodp", FormatUse.Input, 11, null, null, ".fodp"),
        FormatDescriptor.Routed("pdf", FormatUse.Convert, null, 4, null, ".pdf"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, null, 5, null, ".xps"),
        FormatDescriptor.Routed("html", FormatUse.Convert, null, 6, null, ".html", ".htm"),
        FormatDescriptor.Routed("html5", FormatUse.Convert, null, 7, null, ".html", ".htm") with { CompanionFiles = true },
        FormatDescriptor.Routed("png", FormatUse.Render, null, null, 0, ".png") with { Raster = true },
        FormatDescriptor.Routed("jpeg", FormatUse.Render, null, null, 1, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Routed("tiff", FormatUse.Convert, null, 8, null, ".tiff", ".tif") with { Raster = true },
        FormatDescriptor.Routed("gif", FormatUse.Convert, null, 9, null, ".gif") with { Raster = true },
        FormatDescriptor.Routed("svg", FormatUse.Render, null, null, 2, ".svg"),
        FormatDescriptor.Routed("md", FormatUse.Convert, null, 10, null, ".md", ".markdown"),
    ], SlidesFormatRecognition.Rules);

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    /// <summary>The presentation formats create and edit write.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Id is "pptx" or "pptm")];
}
