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
        new("ppt", [LoadFormat.Ppt, LoadFormat.Ppt95], SaveFormat.Ppt),
        new("pptx", [LoadFormat.Pptx], SaveFormat.Pptx),
        new("pptm", [LoadFormat.Pptm], SaveFormat.Pptm),
        new("pps", [LoadFormat.Pps], SaveFormat.Pps),
        new("ppsx", [LoadFormat.Ppsx], SaveFormat.Ppsx),
        new("ppsm", [LoadFormat.Ppsm], SaveFormat.Ppsm),
        new("pot", [LoadFormat.Pot], SaveFormat.Pot),
        new("potx", [LoadFormat.Potx], SaveFormat.Potx),
        new("potm", [LoadFormat.Potm], SaveFormat.Potm),
        new("odp", [LoadFormat.Odp], SaveFormat.Odp),
        new("otp", [LoadFormat.Otp], SaveFormat.Otp),
        new("fodp", [LoadFormat.Fodp], SaveFormat.Fodp),
        new("pdf", [], SaveFormat.Pdf),
        new("xps", [], SaveFormat.Xps),
        new("html", [], SaveFormat.Html),
        new("html5", [], SaveFormat.Html5),
        new("gif", [], SaveFormat.Gif),
        new("md", [], SaveFormat.Md),
        new("png", []) { SlideImage = ImageFormat.Png },
        new("jpeg", []) { SlideImage = ImageFormat.Jpeg },
        new("tiff", []) { SlideImage = ImageFormat.Tiff },
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
    /// itself. A render format is a row that declares a slide image, or one that is neither read
    /// nor saved as a presentation.
    /// </summary>
    internal static ImageFormat? SlideImageOf(string id) =>
        Find(id) is { } row && (row.SlideImage is not null || row is { Save: null, Loads.Length: 0 })
            ? row.SlideImage
            : throw new InvalidOperationException($"'{id}' is not a slide image format.");

    private static SlidesEngineFormat? Find(string id) => All.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal));
}

/// <summary>What one public format id is to the engine.</summary>
/// <param name="Id">The public format id.</param>
/// <param name="Loads">The formats Aspose.Slides detects a presentation of this id as; empty when it is never read.</param>
/// <param name="Save">The format the whole presentation is saved in, or null.</param>
internal sealed record SlidesEngineFormat(string Id, LoadFormat[] Loads, SaveFormat? Save = null)
{
    /// <summary>
    /// For a render format, the bitmap each slide is drawn as; null when each slide is written as
    /// SVG, or the format is not rendered.
    /// </summary>
    public ImageFormat? SlideImage { get; init; }
}
