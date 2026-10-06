using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cells.Rendering;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// How a worksheet's charts fall on its printed pages: the page areas printing and PDF export
/// lay out, and the charts whose cells reach into more than one of them, which the output
/// splits across pages.
/// </summary>
internal static class PrintedPages
{
    /// <summary>
    /// The number of printed pages each chart of <paramref name="sheet"/> reaches, in chart order;
    /// empty when the sheet has no chart, so a sheet without one is never paginated.
    /// </summary>
    internal static IReadOnlyList<(Chart Chart, int Pages)> ChartPages(Worksheet sheet)
    {
        if (sheet.Charts.Count == 0)
        {
            return [];
        }

        CellArea[] pages = sheet.GetPrintingPageBreaks(new ImageOrPrintOptions());
        var counts = new List<(Chart, int)>(sheet.Charts.Count);
        foreach (Chart chart in sheet.Charts)
        {
            ChartShape shape = chart.ChartObject;
            int lastRow = LastCell(shape.UpperLeftRow, shape.LowerRightRow, shape.LowerDeltaY);
            int lastColumn = LastCell(shape.UpperLeftColumn, shape.LowerRightColumn, shape.LowerDeltaX);
            counts.Add((chart, pages.Count(page =>
                page.StartRow <= lastRow && shape.UpperLeftRow <= page.EndRow
                && page.StartColumn <= lastColumn && shape.UpperLeftColumn <= page.EndColumn)));
        }
        return counts;
    }

    // A chart whose edge lies on a cell boundary (offset 0 into its lower-right cell) ends in
    // the cell before, so a chart that ends exactly at a page break stays on its page.
    private static int LastCell(int first, int last, int offset) => offset == 0 && last > first ? last - 1 : last;

    /// <summary>
    /// Warns about the visible charts a PDF of <paramref name="workbook"/> splits across pages:
    /// those of sheet <paramref name="selectedSheet"/>, or of every visible sheet; null when none.
    /// </summary>
    internal static Warning? SplitChartsWarning(Workbook workbook, int? selectedSheet)
    {
        var split = new List<string>();
        var sheets = new HashSet<string>(StringComparer.Ordinal);
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            if (selectedSheet is { } selected ? sheet.Index != selected : !sheet.IsVisible)
            {
                continue;
            }

            foreach ((Chart chart, int pages) in ChartPages(sheet))
            {
                if (pages > 1 && !chart.ChartObject.IsHidden)
                {
                    split.Add($"'{chart.Name}' on sheet {Sheets.QuotedName(sheet)} ({pages} pages)");
                    sheets.Add(sheet.Name);
                }
            }
        }

        return split.Count == 0 ? null : new Warning
        {
            Code = CellsDiagnostics.ChartSplitAcrossPages,
            Message = $"The PDF splits {(split.Count == 1 ? "chart" : "charts")} {string.Join(", ", split)} across pages.",
            Hint = $"Fit the sheet on fewer pages with the set_page_setup operation of 'cells edit' {CellsDiagnostics.ChartSplitRemedy}, then convert again.",
            Location = sheets.Count == 1 ? Sheets.QuotedName(sheets.Single()) : null,
        };
    }
}
