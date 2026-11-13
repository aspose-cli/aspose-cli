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
        new("ppt", [LoadFormat.Ppt, LoadFormat.Ppt95], SaveFormat.Ppt)
        {
            BinaryPpt = true,
            // SLIDES-PPT-NATIVE-CONTENT: the charts and the table styles; a PPT file has no sections.
            Loses = SlidesContentLoss.Charts | SlidesContentLoss.Sections | SlidesContentLoss.TableStyles,
        },
        new("pptx", [LoadFormat.Pptx], SaveFormat.Pptx),
        new("pptm", [LoadFormat.Pptm], SaveFormat.Pptm),
        new("pps", [LoadFormat.Pps]) { BinaryPpt = true },
        new("ppsx", [LoadFormat.Ppsx]),
        new("ppsm", [LoadFormat.Ppsm]),
        new("pot", [LoadFormat.Pot]) { BinaryPpt = true },
        new("potx", [LoadFormat.Potx]),
        new("potm", [LoadFormat.Potm]),
        // SLIDES-ODP-NATIVE-CONTENT: the charts, the transitions and the sections.
        new("odp", [LoadFormat.Odp], SaveFormat.Odp) { Loses = SlidesContentLoss.Charts | SlidesContentLoss.Transitions | SlidesContentLoss.Sections },
        new("otp", [LoadFormat.Otp]),
        new("fodp", [LoadFormat.Fodp]),
        new("pdf", [], SaveFormat.Pdf),
        new("xps", [], SaveFormat.Xps),
        new("html", [], SaveFormat.Html) { Lossy = true },
        // The HTML5 export draws each native chart as a picture, whose text no reader finds.
        new("html5", [], SaveFormat.Html5) { Lossy = true, Loses = SlidesContentLoss.Charts },
        new("gif", [], SaveFormat.Gif),
        // The Markdown export writes the slides' text alone (MarkdownExportType.TextOnly).
        new("md", [], SaveFormat.Md) { Lossy = true, HoldsReadText = true, Loses = SlidesContentLoss.Pictures },
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

    /// <summary>Whether a format id is a PowerPoint 97-2003 binary file: a presentation, show or template.</summary>
    internal static bool IsBinaryPpt(string id) => Find(id)?.BinaryPpt == true;

    /// <summary>Whether an output of a format id may not keep every presentation feature.</summary>
    internal static bool IsLossy(string id) => Find(id)?.Lossy == true;

    /// <summary>Whether an output of a format id holds the slides' text as the CLI read it.</summary>
    internal static bool HoldsReadText(string id) => Find(id)?.HoldsReadText == true;

    /// <summary>What a save in a format id does not keep of a deck; <see cref="SlidesContentLoss.None"/> for an id the engine does not map.</summary>
    internal static SlidesContentLoss Losses(string id) => Find(id)?.Loses ?? SlidesContentLoss.None;

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
    /// For a render format, the bitmap each slide is drawn as; null when each slide is written as
    /// SVG, or the format is not rendered.
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
    /// What the save does not keep of a deck, though the output shows its slides, which a convert
    /// names in <c>LOSSY_CONVERSION</c> when the slides it converts have it.
    /// </summary>
    public SlidesContentLoss Loses { get; init; }
}

/// <summary>The kinds of deck content a save in a format may not keep.</summary>
[Flags]
internal enum SlidesContentLoss
{
    /// <summary>The save keeps every kind.</summary>
    None = 0,

    /// <summary>The native charts, which become pictures of themselves.</summary>
    Charts = 1,

    /// <summary>The slide transitions.</summary>
    Transitions = 2,

    /// <summary>The sections.</summary>
    Sections = 4,

    /// <summary>
    /// The table styles: a styled table keeps its cells and text but not the fills, borders and
    /// text formatting its style gives it.
    /// </summary>
    TableStyles = 8,

    /// <summary>
    /// The pictures, of which an output of the slides' text holds none: a convert names them with
    /// the speaker notes, which it leaves out as well.
    /// </summary>
    Pictures = 16,
}
