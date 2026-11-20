
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Deny-by-default presentation format registry.</summary>
public static class SlidesFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Declare("ppt", FormatUse.Input | FormatUse.Convert, ".ppt"),
        // pptx leads the convert formats.
        FormatDescriptor.Declare("pptx", FormatUse.Input | FormatUse.Convert, ".pptx") with { Protectable = true, ConvertOrder = -1 },
        FormatDescriptor.Declare("pptm", FormatUse.Input | FormatUse.Convert, ".pptm") with { Protectable = true },
        FormatDescriptor.Declare("pps", FormatUse.Input, ".pps"),
        FormatDescriptor.Declare("ppsx", FormatUse.Input, ".ppsx"),
        FormatDescriptor.Declare("ppsm", FormatUse.Input, ".ppsm"),
        FormatDescriptor.Declare("pot", FormatUse.Input, ".pot"),
        FormatDescriptor.Declare("potx", FormatUse.Input, ".potx"),
        FormatDescriptor.Declare("potm", FormatUse.Input, ".potm"),
        FormatDescriptor.Declare("odp", FormatUse.Input | FormatUse.Convert, ".odp"),
        FormatDescriptor.Declare("otp", FormatUse.Input, ".otp"),
        FormatDescriptor.Declare("fodp", FormatUse.Input, ".fodp"),
        FormatDescriptor.Declare("pdf", FormatUse.Convert, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, ".xps"),
        FormatDescriptor.Declare("html", FormatUse.Convert, ".html", ".htm"),
        FormatDescriptor.Declare("html5", FormatUse.Convert, ".html", ".htm") with { CompanionFiles = true },
        FormatDescriptor.Declare("png", FormatUse.Render, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("tiff", FormatUse.Render, ".tiff", ".tif") with { Raster = true },
        FormatDescriptor.Declare("gif", FormatUse.Convert, ".gif") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, ".svg"),
        FormatDescriptor.Declare("md", FormatUse.Convert, ".md", ".markdown"),
    ];

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    /// <summary>The presentation formats create and edit write.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Id is "pptx" or "pptm")];

    /// <summary>The presentation formats edit writes back in the same format: every one the engine both opens and saves.</summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input))];
}
