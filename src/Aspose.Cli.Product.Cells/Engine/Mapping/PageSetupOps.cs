using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

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

        if (op.Orientation is { } orientation)
        {
            pageSetup.Orientation = orientation == PageOrientations.Landscape
                ? PageOrientationType.Landscape
                : PageOrientationType.Portrait;
        }

        if (op.PaperSize is { } paper)
        {
            pageSetup.PaperSize = ToPaperSize(paper);
        }

        if (op.FitToWidth is { } wide)
        {
            pageSetup.FitToPagesWide = wide;
        }

        if (op.FitToHeight is { } tall)
        {
            pageSetup.FitToPagesTall = tall;
        }

        if (op.Scale is { } scale)
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

        // An empty print area clears it; a range restricts printing to it.
        pageSetup.PrintArea = op.Range ?? string.Empty;

        if (op.TitleRows is { } rows)
        {
            pageSetup.PrintTitleRows = NormalizeTitle(rows);
        }

        if (op.TitleColumns is { } columns)
        {
            pageSetup.PrintTitleColumns = NormalizeTitle(columns);
        }

        return null;
    }

    private static void ApplyMargins(PageSetup pageSetup, MarginsData margins)
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
    // contract states inches. The unit is translated here, at the engine boundary.
    private static double Centimetres(double inches) => inches * 2.54;

    private static PaperSizeType ToPaperSize(string paper) => paper switch
    {
        PaperSizes.Letter => PaperSizeType.PaperLetter,
        PaperSizes.Legal => PaperSizeType.PaperLegal,
        PaperSizes.A3 => PaperSizeType.PaperA3,
        PaperSizes.A4 => PaperSizeType.PaperA4,
        PaperSizes.A5 => PaperSizeType.PaperA5,
        PaperSizes.Tabloid => PaperSizeType.PaperTabloid,
        _ => PaperSizeType.PaperLetter, // the parser guarantees a known value
    };

    // Excel stores print titles as "$1:$2" / "$A:$B"; accept the friendlier
    // "1:2" / "A:B" too, since the parser only checks they are non-empty.
    private static string NormalizeTitle(string title)
    {
        if (title.Contains('$', StringComparison.Ordinal))
        {
            return title;
        }

        string[] parts = title.Split(':');
        return parts.Length == 2 ? $"${parts[0]}:${parts[1]}" : title;
    }
}
