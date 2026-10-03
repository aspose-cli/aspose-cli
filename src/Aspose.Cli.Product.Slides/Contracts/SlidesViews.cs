using System.Globalization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Views rendered by the Slides product.</summary>
public static class SlidesViews
{
    /// <summary>The slides of the presentation, one image per slide.</summary>
    public const string Slides = "slides";

    /// <summary>Id of the view part showing a slide, which review findings about that slide name.</summary>
    internal static string PartId(uint slideId) =>
        string.Create(CultureInfo.InvariantCulture, $"slide-{slideId}");
}
