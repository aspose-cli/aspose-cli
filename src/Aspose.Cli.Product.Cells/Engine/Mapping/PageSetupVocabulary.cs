using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The page-size presets as the engine's paper sizes, the page size a read reports, and the
/// margins in points the engine stores in centimetres.
/// </summary>
internal static class PageSetupVocabulary
{
    // Every Aspose.Cells PageSetup margin is measured in centimetres, while the op
    // contract states points. The unit is translated here, at the engine boundary.
    internal static double Centimetres(double points) => points / 72 * 2.54;

    /// <summary>A margin the engine stores in centimetres, in points to two decimals.</summary>
    internal static double Points(double centimetres) => Math.Round(centimetres / 2.54 * 72, 2);

    // The presets by the engine's paper sizes, whose dimensions the presets state exactly.
    internal static readonly (string Preset, PaperSizeType Paper)[] Presets =
    [
        (PageSizePresets.Letter, PaperSizeType.PaperLetter),
        (PageSizePresets.Legal, PaperSizeType.PaperLegal),
        (PageSizePresets.A3, PaperSizeType.PaperA3),
        (PageSizePresets.A4, PaperSizeType.PaperA4),
        (PageSizePresets.A5, PaperSizeType.PaperA5),
        (PageSizePresets.Tabloid, PaperSizeType.PaperTabloid),
    ];

    /// <summary>The page size in points as the page is laid out: in landscape the width is the longer side.</summary>
    internal static PageSizeInfo PageSize(PageSetup pageSetup)
    {
        (double shorter, double longer) = Presets.FirstOrDefault(item => item.Paper == pageSetup.PaperSize).Preset is { } preset
            ? PageSizePresets.Dimensions(preset)
            : Sides(pageSetup);
        return pageSetup.Orientation == PageOrientationType.Landscape
            ? PageSizeInfo.Of(longer, shorter)
            : PageSizeInfo.Of(shorter, longer);
    }

    /// <summary>The shorter and the longer side of the paper in points, as the engine reports them.</summary>
    internal static (double Shorter, double Longer) Sides(PageSetup pageSetup) =>
        (Math.Round(Math.Min(pageSetup.PaperWidth, pageSetup.PaperHeight) * 72, 2),
            Math.Round(Math.Max(pageSetup.PaperWidth, pageSetup.PaperHeight) * 72, 2));
}
