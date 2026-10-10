using Aspose.Slides;
using Aspose.Slides.Export;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The one table between the format ids <see cref="SlidesFormats"/> declares and the formats
/// Aspose.Slides loads and saves. A format the product declares but this table cannot load or
/// write fails its product test, not a user's command.
/// </summary>
internal static class SlidesEngineFormats
{
    /// <summary>Every format id the engine maps, with what it maps to.</summary>
    internal static IReadOnlyList<SlidesEngineFormat> All { get; } =
    [
        new("ppt", [LoadFormat.Ppt, LoadFormat.Ppt95], SaveFormat.Ppt),
        new("pptx", [LoadFormat.Pptx], SaveFormat.Pptx),
        new("pptm", [LoadFormat.Pptm], SaveFormat.Pptm),
        new("pps", [LoadFormat.Pps]),
        new("ppsx", [LoadFormat.Ppsx]),
        new("ppsm", [LoadFormat.Ppsm]),
        new("pot", [LoadFormat.Pot]),
        new("potx", [LoadFormat.Potx]),
        new("potm", [LoadFormat.Potm]),
        new("odp", [LoadFormat.Odp], SaveFormat.Odp),
        new("otp", [LoadFormat.Otp]),
        new("fodp", [LoadFormat.Fodp]),
        new("pdf", [], SaveFormat.Pdf),
        new("xps", [], SaveFormat.Xps),
        new("html", [], SaveFormat.Html),
        new("html5", [], SaveFormat.Html5),
        new("tiff", [], SaveFormat.Tiff),
        new("gif", [], SaveFormat.Gif),
        new("md", [], SaveFormat.Md),
        new("png", [], SlideImage: true),
        new("jpeg", [], SlideImage: true),
        new("svg", [], SlideImage: true),
    ];

    /// <summary>The format id of a format Aspose.Slides detected, or <c>unknown</c>.</summary>
    internal static string IdOf(LoadFormat format) =>
        All.FirstOrDefault(row => row.Loads.Contains(format))?.Id ?? "unknown";

    /// <summary>Whether the id is written as one image per slide rather than by saving the presentation.</summary>
    internal static bool IsSlideImage(string id) => Find(id)?.SlideImage == true;

    /// <summary>The format Aspose.Slides saves the whole presentation in.</summary>
    internal static SaveFormat SaveFormatOf(string id) =>
        Find(id)?.Save ?? throw new InvalidOperationException($"'{id}' has no presentation save format.");

    private static SlidesEngineFormat? Find(string id) => All.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal));
}

/// <summary>What one public format id is to the engine.</summary>
/// <param name="Id">The public format id.</param>
/// <param name="Loads">The formats Aspose.Slides detects a presentation of this id as; empty when it is never read.</param>
/// <param name="Save">The format the whole presentation is saved in, or null.</param>
/// <param name="SlideImage">Whether the id is written as one image per slide.</param>
internal sealed record SlidesEngineFormat(string Id, LoadFormat[] Loads, SaveFormat? Save = null, bool SlideImage = false);
