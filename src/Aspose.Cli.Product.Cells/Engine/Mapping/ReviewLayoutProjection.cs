using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Ports;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Builds bounded, SDK-neutral layout facts for visual review.</summary>
internal static class ReviewLayoutProjection
{
    private const int MaxIssueSamplesPerSheet = 100;
    private const double NarrowColumnWidth = 3;
    private const double WideColumnWidth = 80;
    private const double ShortRowHeight = 8;
    private const double TallRowHeight = 120;

    public static CellsReviewLayout Inspect(Workbook workbook)
    {
        var sheets = new List<CellsReviewSheetLayout>(workbook.Worksheets.Count);
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            sheets.Add(InspectSheet(sheet));
        }
        return new CellsReviewLayout(sheets);
    }

    private static CellsReviewSheetLayout InspectSheet(Worksheet sheet)
    {
        SheetContentScan content = ScanContent(sheet);
        var issues = new List<CellsReviewDimensionIssue>();
        DimensionScan columns = InspectColumns(sheet, content.OccupiedColumns, issues);
        DimensionScan rows = InspectRows(sheet, content.OccupiedRows, issues);
        PrintAreaScan print = InspectPrintArea(sheet, content.ContentRange);
        IReadOnlyList<CellsReviewChartLayout> charts = InspectCharts(sheet, print.Ranges);

        return new CellsReviewSheetLayout
        {
            Name = sheet.Name,
            UsedAreaCells = content.UsedAreaCells,
            PopulatedCells = content.PopulatedCells,
            ContentRange = content.ContentRange is { } range ? A1.FormatRange(range) : null,
            HasVisualObjects = sheet.Shapes.Count > 0,
            HiddenPopulatedColumns = columns.Hidden,
            NarrowPopulatedColumns = columns.Small,
            WidePopulatedColumns = columns.Large,
            HiddenPopulatedRows = rows.Hidden,
            ShortPopulatedRows = rows.Small,
            TallPopulatedRows = rows.Large,
            DimensionIssues = issues,
            PrintArea = print.Value,
            PrintAreaInvalid = print.Invalid,
            PrintAreaExcludesContent = print.ExcludesContent,
            PrintAreaExcessive = print.Excessive,
            Charts = charts,
        };
    }

    private static SheetContentScan ScanContent(Worksheet sheet)
    {
        int maxRow = sheet.Cells.MaxDataRow;
        int maxColumn = sheet.Cells.MaxDataColumn;
        bool[] occupiedRows = maxRow < 0 ? [] : new bool[maxRow + 1];
        bool[] occupiedColumns = maxColumn < 0 ? [] : new bool[maxColumn + 1];
        long populated = 0;
        int firstRow = int.MaxValue;
        int firstColumn = int.MaxValue;
        int lastRow = -1;
        int lastColumn = -1;
        foreach (Cell cell in sheet.Cells)
        {
            if (cell.Value is null && string.IsNullOrEmpty(cell.Formula))
            {
                continue;
            }
            populated++;
            occupiedRows[cell.Row] = true;
            occupiedColumns[cell.Column] = true;
            firstRow = Math.Min(firstRow, cell.Row);
            firstColumn = Math.Min(firstColumn, cell.Column);
            lastRow = Math.Max(lastRow, cell.Row);
            lastColumn = Math.Max(lastColumn, cell.Column);
        }

        RangeRef? contentRange = lastRow < 0
            ? null
            : new RangeRef(
                new CellRef(firstRow, firstColumn),
                new CellRef(lastRow, lastColumn));
        long usedAreaCells = maxRow < 0 || maxColumn < 0
            ? 0
            : checked((long)(maxRow + 1) * (maxColumn + 1));
        return new SheetContentScan(
            occupiedRows,
            occupiedColumns,
            populated,
            contentRange,
            usedAreaCells);
    }

    private static DimensionScan InspectColumns(
        Worksheet sheet,
        IReadOnlyList<bool> occupied,
        ICollection<CellsReviewDimensionIssue> issues)
    {
        int hiddenColumns = 0;
        int narrowColumns = 0;
        int wideColumns = 0;
        for (int column = 0; column < occupied.Count; column++)
        {
            if (!occupied[column])
            {
                continue;
            }
            double width = sheet.Cells.GetColumnWidth(column);
            if (sheet.Cells.IsColumnHidden(column))
            {
                hiddenColumns++;
                AddIssue(issues, "hidden-column", column, width);
            }
            else if (width < NarrowColumnWidth)
            {
                narrowColumns++;
                AddIssue(issues, "narrow-column", column, width);
            }
            else if (width > WideColumnWidth)
            {
                wideColumns++;
                AddIssue(issues, "wide-column", column, width);
            }
        }
        return new DimensionScan(hiddenColumns, narrowColumns, wideColumns);
    }

    private static DimensionScan InspectRows(
        Worksheet sheet,
        IReadOnlyList<bool> occupied,
        ICollection<CellsReviewDimensionIssue> issues)
    {
        int hiddenRows = 0;
        int shortRows = 0;
        int tallRows = 0;
        for (int row = 0; row < occupied.Count; row++)
        {
            if (!occupied[row])
            {
                continue;
            }
            double height = sheet.Cells.GetRowHeight(row);
            if (sheet.Cells.IsRowHidden(row))
            {
                hiddenRows++;
                AddIssue(issues, "hidden-row", row, height);
            }
            else if (height < ShortRowHeight)
            {
                shortRows++;
                AddIssue(issues, "short-row", row, height);
            }
            else if (height > TallRowHeight)
            {
                tallRows++;
                AddIssue(issues, "tall-row", row, height);
            }
        }
        return new DimensionScan(hiddenRows, shortRows, tallRows);
    }

    private static PrintAreaScan InspectPrintArea(
        Worksheet sheet,
        RangeRef? contentRange)
    {
        string? printArea = Normalize(sheet.PageSetup.PrintArea);
        (IReadOnlyList<RangeRef> printRanges, bool invalid) = ParsePrintArea(printArea);
        bool excludesContent = contentRange is { } content
            && printRanges.Count > 0
            && !printRanges.Any(area => Contains(area, content));
        long contentAreaCells = contentRange is { } contentBounds
            ? checked((long)contentBounds.RowCount * contentBounds.ColumnCount)
            : 0;
        long printAreaCells = printRanges.Sum(static area =>
            checked((long)area.RowCount * area.ColumnCount));
        bool excessivePrintArea = contentAreaCells > 0
            && printAreaCells > Math.Max(1_000, checked(contentAreaCells * 20));
        return new PrintAreaScan(
            printArea,
            printRanges,
            invalid,
            excludesContent,
            excessivePrintArea);
    }

    private static IReadOnlyList<CellsReviewChartLayout> InspectCharts(
        Worksheet sheet,
        IReadOnlyList<RangeRef> printRanges)
    {
        var charts = new List<CellsReviewChartLayout>(sheet.Charts.Count);
        foreach (Chart chart in sheet.Charts)
        {
            ChartShape shape = chart.ChartObject;
            var chartRange = new RangeRef(
                new CellRef(shape.UpperLeftRow, shape.UpperLeftColumn),
                new CellRef(shape.LowerRightRow, shape.LowerRightColumn));
            charts.Add(new CellsReviewChartLayout
            {
                Name = chart.Name,
                Hidden = shape.IsHidden,
                WidthPixels = shape.Width,
                HeightPixels = shape.Height,
                SeriesCount = chart.NSeries.Count,
                AnchoredInHiddenCells = AnchoredInHiddenCells(sheet, chartRange),
                ExcludedByPrintArea = printRanges.Count > 0
                    && !printRanges.Any(area => Intersects(area, chartRange)),
            });
        }
        return charts;
    }

    private sealed record SheetContentScan(
        IReadOnlyList<bool> OccupiedRows,
        IReadOnlyList<bool> OccupiedColumns,
        long PopulatedCells,
        RangeRef? ContentRange,
        long UsedAreaCells);

    private sealed record DimensionScan(int Hidden, int Small, int Large);

    private sealed record PrintAreaScan(
        string? Value,
        IReadOnlyList<RangeRef> Ranges,
        bool Invalid,
        bool ExcludesContent,
        bool Excessive);

    private static void AddIssue(
        ICollection<CellsReviewDimensionIssue> issues,
        string kind,
        int index,
        double size)
    {
        if (issues.Count < MaxIssueSamplesPerSheet)
        {
            issues.Add(new CellsReviewDimensionIssue(kind, index, size));
        }
    }

    private static (IReadOnlyList<RangeRef> Ranges, bool Invalid) ParsePrintArea(
        string? printArea)
    {
        if (printArea is null)
        {
            return ([], false);
        }
        var ranges = new List<RangeRef>();
        try
        {
            foreach (string part in printArea.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                ranges.Add(A1.ParseRange(part.TrimStart('=')).Range);
            }
            return (ranges, ranges.Count == 0);
        }
        catch (CliException)
        {
            return ([], true);
        }
    }

    private static bool AnchoredInHiddenCells(Worksheet sheet, RangeRef range) =>
        sheet.Cells.IsRowHidden(range.Start.Row)
        || sheet.Cells.IsColumnHidden(range.Start.Column)
        || sheet.Cells.IsRowHidden(range.End.Row)
        || sheet.Cells.IsColumnHidden(range.End.Column);

    private static bool Contains(RangeRef outer, RangeRef inner) =>
        outer.Start.Row <= inner.Start.Row
        && outer.Start.Column <= inner.Start.Column
        && outer.End.Row >= inner.End.Row
        && outer.End.Column >= inner.End.Column;

    private static bool Intersects(RangeRef left, RangeRef right) =>
        left.Start.Row <= right.End.Row
        && left.End.Row >= right.Start.Row
        && left.Start.Column <= right.End.Column
        && left.End.Column >= right.Start.Column;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
