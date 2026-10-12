using System.Globalization;
using Aspose.Cells;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Page layout operations: orientation, paper size, scaling, margins, headers
/// and footers, the print area and repeating print titles. Page setup is not
/// cell data, so these return null (no meaningful cell count).
/// </summary>
internal static class PageSetupOps
{
    public static long? SetPageSetup(Worksheet sheet, SetPageSetupOp op)
    {
        PageSetup pageSetup = sheet.PageSetup;
        // A size no standard paper has is refused before anything changes.
        PaperSizeType? paper = op.PageSize is { } size ? Paper(pageSetup, size) : null;

        if (op.Orientation is { } orientation)
        {
            pageSetup.Orientation = orientation == CellsPageOrientations.Landscape
                ? PageOrientationType.Landscape
                : PageOrientationType.Portrait;
        }

        if (paper is { } chosen)
        {
            pageSetup.PaperSize = chosen;
        }

        if (op.FitToWidth is { } wide)
        {
            pageSetup.FitToPagesWide = wide;
        }

        if (op.FitToHeight is { } tall)
        {
            pageSetup.FitToPagesTall = tall;
        }

        if (op.ScalePercent is { } scale)
        {
            pageSetup.Zoom = scale;
        }

        if (op.Margins is { } margins)
        {
            ApplyMargins(pageSetup, margins);
        }

        if (op.Header is { } header)
        {
            pageSetup.SetHeader(1, header);
        }

        if (op.Footer is { } footer)
        {
            pageSetup.SetFooter(1, footer);
        }

        return null;
    }

    public static long? SetPrintArea(Worksheet sheet, SetPrintAreaOp op)
    {
        PageSetup pageSetup = sheet.PageSetup;
        pageSetup.PrintArea = op.Range;

        // The parser normalized the titles to Excel's absolute band form; titles omitted are removed.
        pageSetup.PrintTitleRows = op.TitleRows ?? string.Empty;
        pageSetup.PrintTitleColumns = op.TitleColumns ?? string.Empty;
        return null;
    }

    /// <summary>Removes the print area and titles; 1 when the sheet had any, null when there was nothing to remove.</summary>
    public static long? ClearPrintArea(Worksheet sheet)
    {
        PageSetup pageSetup = sheet.PageSetup;
        bool had = !string.IsNullOrEmpty(pageSetup.PrintArea) || !string.IsNullOrEmpty(pageSetup.PrintTitleRows)
            || !string.IsNullOrEmpty(pageSetup.PrintTitleColumns);
        pageSetup.PrintArea = string.Empty;
        pageSetup.PrintTitleRows = string.Empty;
        pageSetup.PrintTitleColumns = string.Empty;
        return had ? 1 : null;
    }

    private static void ApplyMargins(PageSetup pageSetup, CellsMargins margins)
    {
        if (margins.Top is { } top)
        {
            pageSetup.TopMargin = Centimetres(top);
        }

        if (margins.Bottom is { } bottom)
        {
            pageSetup.BottomMargin = Centimetres(bottom);
        }

        if (margins.Left is { } left)
        {
            pageSetup.LeftMargin = Centimetres(left);
        }

        if (margins.Right is { } right)
        {
            pageSetup.RightMargin = Centimetres(right);
        }

        if (margins.Header is { } header)
        {
            pageSetup.HeaderMargin = Centimetres(header);
        }

        if (margins.Footer is { } footer)
        {
            pageSetup.FooterMargin = Centimetres(footer);
        }
    }

    // Every Aspose.Cells PageSetup margin is measured in centimetres, while the op
    // contract states points. The unit is translated here, at the engine boundary.
    private static double Centimetres(double points) => points / 72 * 2.54;

    /// <summary>A margin the engine stores in centimetres, in points to two decimals.</summary>
    internal static double Points(double centimetres) => Math.Round(centimetres / 2.54 * 72, 2);

    // The presets by the engine's paper sizes, whose dimensions the presets state exactly.
    private static readonly (string Preset, PaperSizeType Paper)[] Presets =
    [
        (PageSizePresets.Letter, PaperSizeType.PaperLetter),
        (PageSizePresets.Legal, PaperSizeType.PaperLegal),
        (PageSizePresets.A3, PaperSizeType.PaperA3),
        (PageSizePresets.A4, PaperSizeType.PaperA4),
        (PageSizePresets.A5, PaperSizeType.PaperA5),
        (PageSizePresets.Tabloid, PaperSizeType.PaperTabloid),
    ];

    // The engine reports a paper's size in inches to two decimals.
    private const double PaperTolerance = 1.5;

    /// <summary>
    /// Sets the paper: a preset, turned by the orientation, or the standard paper whose size the
    /// width and height give in either orientation. A workbook stores a paper size, not
    /// dimensions, so a size no standard paper has is refused rather than saved as another.
    /// </summary>
    private static PaperSizeType Paper(PageSetup pageSetup, PageSizeInput size)
    {
        (double width, double height) = size.Dimensions();
        string? preset = size.Preset ?? PageSizePresets.Matching(width, height);
        if (preset is not null)
        {
            return Presets.First(item => item.Preset == preset).Paper;
        }

        PaperSizeType original = pageSetup.PaperSize;
        try
        {
            foreach (PaperSizeType paper in Enum.GetValues<PaperSizeType>().Where(static paper => paper != PaperSizeType.Custom))
            {
                pageSetup.PaperSize = paper;
                (double shorter, double longer) = Sides(pageSetup);
                if (Math.Abs(shorter - Math.Min(width, height)) <= PaperTolerance && Math.Abs(longer - Math.Max(width, height)) <= PaperTolerance)
                {
                    return paper;
                }
            }
        }
        finally
        {
            pageSetup.PaperSize = original;
        }

        throw new OperationInvalidException(
            string.Create(CultureInfo.InvariantCulture, $"a workbook stores a standard paper size, and no standard paper is {width} by {height} points"),
            "Give a preset such as a4 or letter, or the width and height of a standard paper.");
    }

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
    private static (double Shorter, double Longer) Sides(PageSetup pageSetup) =>
        (Math.Round(Math.Min(pageSetup.PaperWidth, pageSetup.PaperHeight) * 72, 2),
            Math.Round(Math.Max(pageSetup.PaperWidth, pageSetup.PaperHeight) * 72, 2));
}
