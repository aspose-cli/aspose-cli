using Aspose.Slides;
using Aspose.Slides.Export;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The one table between the format ids <see cref="SlidesFormats"/> declares and the formats
/// Aspose.Slides loads and saves. A format the product declares but this table cannot load or
/// save fails its product test, not a user's command; the render formats are drawn slide by
/// slide instead (<see cref="SlidesExport"/>).
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
    ];

    /// <summary>The format id of a format Aspose.Slides detected, or <c>unknown</c>.</summary>
    internal static string IdOf(LoadFormat format) =>
        All.FirstOrDefault(row => row.Loads.Contains(format))?.Id ?? "unknown";

    /// <summary>The format Aspose.Slides saves the whole presentation in.</summary>
    internal static SaveFormat SaveFormatOf(string id) =>
        Find(id)?.Save ?? throw new InvalidOperationException($"'{id}' has no presentation save format.");

    /// <summary>Whether a format id is a PowerPoint 97-2003 binary file: a presentation, show or template.</summary>
    internal static bool IsBinaryPpt(string id) => Find(id)?.BinaryPpt == true;

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
}
