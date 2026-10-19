using Aspose.Slides;
using Aspose.Slides.Export;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The one table between the format ids <see cref="SlidesFormats"/> declares and the formats
/// Aspose.Slides loads and saves, and what each format keeps. A format the product declares but
/// this table cannot load, save or draw fails its product test, not a user's command; the render
/// formats have no save format and are drawn slide by slide instead (<see cref="SlidesExport"/>).
/// </summary>
internal static class SlidesEngineFormats
{
    /// <summary>Every format id the engine maps, with what it maps to.</summary>
    internal static IReadOnlyList<SlidesEngineFormat> All { get; } =
    [
        new("ppt", [LoadFormat.Ppt, LoadFormat.Ppt95], SaveFormat.Ppt) { BinaryPpt = true },
        new("pptx", [LoadFormat.Pptx], SaveFormat.Pptx),
        new("pptm", [LoadFormat.Pptm], SaveFormat.Pptm),
        new("pps", [LoadFormat.Pps]) { BinaryPpt = true },
        new("ppsx", [LoadFormat.Ppsx]),
        new("ppsm", [LoadFormat.Ppsm]),
        new("pot", [LoadFormat.Pot]) { BinaryPpt = true },
        new("potx", [LoadFormat.Potx]),
        new("potm", [LoadFormat.Potm]),
        new("odp", [LoadFormat.Odp], SaveFormat.Odp) { LosesNativeContent = true },
        new("otp", [LoadFormat.Otp]),
        new("fodp", [LoadFormat.Fodp]),
        new("pdf", [], SaveFormat.Pdf),
        new("xps", [], SaveFormat.Xps),
        new("html", [], SaveFormat.Html) { Lossy = true },
        new("html5", [], SaveFormat.Html5) { Lossy = true },
        new("tiff", [], SaveFormat.Tiff),
        new("gif", [], SaveFormat.Gif),
        new("md", [], SaveFormat.Md) { Lossy = true, HoldsReadText = true },
        new("png", []) { SlideImage = ImageFormat.Png },
        new("jpeg", []) { SlideImage = ImageFormat.Jpeg },
        new("svg", []),
    ];

    /// <summary>The format id of a format Aspose.Slides detected, or <c>unknown</c>.</summary>
    internal static string IdOf(LoadFormat format) =>
        All.FirstOrDefault(row => row.Loads.Contains(format))?.Id ?? "unknown";

    /// <summary>The format Aspose.Slides saves the whole presentation in.</summary>
    internal static SaveFormat SaveFormatOf(string id) =>
        Find(id)?.Save ?? throw new InvalidOperationException($"'{id}' has no presentation save format.");

    /// <summary>
    /// The bitmap a render format draws each slide as, or null for SVG, which each slide writes
    /// itself. A render format is a row that is neither read nor saved as a presentation.
    /// </summary>
    internal static ImageFormat? SlideImageOf(string id) =>
        Find(id) is { Save: null, Loads.Length: 0 } row
            ? row.SlideImage
            : throw new InvalidOperationException($"'{id}' is not a slide image format.");

    /// <summary>Whether a format id is a PowerPoint 97-2003 binary file: a presentation, show or template.</summary>
    internal static bool IsBinaryPpt(string id) => Find(id)?.BinaryPpt == true;

    /// <summary>Whether an output of a format id may not keep every presentation feature.</summary>
    internal static bool IsLossy(string id) => Find(id)?.Lossy == true;

    /// <summary>Whether an output of a format id holds the slides' text as the CLI read it.</summary>
    internal static bool HoldsReadText(string id) => Find(id)?.HoldsReadText == true;

    /// <summary>Whether a save in a format id loses the deck's native charts, transitions and sections.</summary>
    internal static bool LosesNativeContent(string id) => Find(id)?.LosesNativeContent == true;

    private static SlidesEngineFormat? Find(string id) => All.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal));
}

/// <summary>What one public format id is to the engine.</summary>
/// <param name="Id">The public format id.</param>
/// <param name="Loads">The formats Aspose.Slides detects a presentation of this id as; empty when it is never read.</param>
/// <param name="Save">The format the whole presentation is saved in, or null.</param>
internal sealed record SlidesEngineFormat(string Id, LoadFormat[] Loads, SaveFormat? Save = null)
{
    /// <summary>Whether the format is a PowerPoint 97-2003 binary file, which the engine reads with its PPT reader.</summary>
    public bool BinaryPpt { get; init; }

    /// <summary>
    /// For a render format, which is neither read nor saved as a presentation, the bitmap each
    /// slide is drawn as; null when each slide is written as SVG.
    /// </summary>
    public ImageFormat? SlideImage { get; init; }

    /// <summary>
    /// Whether the export may not keep every presentation feature (layout, animation,
    /// interactive media), so a convert to it warns <c>LOSSY_CONVERSION</c>.
    /// </summary>
    public bool Lossy { get; init; }

    /// <summary>
    /// Whether the output holds the slides' text as the CLI read it, as Markdown does: the text
    /// evaluation mode cut short shows there, and its watermark does not.
    /// </summary>
    public bool HoldsReadText { get; init; }

    /// <summary>
    /// Whether the save turns the native charts into pictures and drops the transitions and
    /// sections (SLIDES-ODP-NATIVE-CONTENT), which a convert names in <c>LOSSY_CONVERSION</c>.
    /// </summary>
    public bool LosesNativeContent { get; init; }
}
