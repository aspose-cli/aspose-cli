using Aspose.Cells;

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

        // A range restricts printing to it; titles alone leave the print area as
        // it is, and an op with no fields clears it.
        if (op.Range is { } range)
        {
            pageSetup.PrintArea = range;
        }
        else if (op.TitleRows is null && op.TitleColumns is null)
        {
            pageSetup.PrintArea = string.Empty;
        }

        // The parser normalized the titles to Excel's absolute band form.
        if (op.TitleRows is { } rows)
        {
            pageSetup.PrintTitleRows = rows;
        }

        if (op.TitleColumns is { } columns)
        {
            pageSetup.PrintTitleColumns = columns;
        }

        return null;
    }

    private static void ApplyMargins(PageSetup pageSetup, Margins margins)
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
        _ => throw new ArgumentOutOfRangeException(
            nameof(paper), paper, "Paper size is missing from the engine mapper."),
    };
}
