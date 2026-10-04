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
    private const int MaxDimensionSamples = 5;
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
        DimensionScan columns = InspectColumns(sheet, content.OccupiedColumns);
        DimensionScan rows = InspectRows(sheet, content.OccupiedRows);
        PrintAreaScan print = InspectPrintArea(sheet, content.ContentRange);
        IReadOnlyList<CellsReviewChartLayout> charts = InspectCharts(sheet, print.Ranges);
        (CellsReviewCellSet clipped, CellsReviewCellSet overflowing) = InspectWideValues(sheet);

        return new CellsReviewSheetLayout
        {
            Name = sheet.Name,
            UsedAreaCells = content.UsedAreaCells,
            PopulatedCells = content.PopulatedCells,
            ContentRange = content.ContentRange is { } range ? A1.FormatRange(range) : null,
            HasVisualObjects = sheet.Shapes.Count > 0,
            IsEvaluationWarning = CellsEvaluation.IsWarningSheet(sheet),
            HiddenPopulatedColumns = columns.Hidden.ToSet(),
            NarrowPopulatedColumns = columns.Small.ToSet(),
            WidePopulatedColumns = columns.Large.ToSet(),
            HiddenPopulatedRows = rows.Hidden.ToSet(),
            ShortPopulatedRows = rows.Small.ToSet(),
            TallPopulatedRows = rows.Large.ToSet(),
            ClippedCells = clipped,
            OverflowingCells = overflowing,
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

    /// <summary>
    /// Finds values wider than their column. Excel shows cut off text whose right-hand neighbor is
    /// filled (so it cannot spill over), and numbers, which it shows as #### or rounded; other
    /// text spills over the empty cells to its right. Only values whose length, scaled by their
    /// font size, could exceed the column are measured.
    /// </summary>
    private static (CellsReviewCellSet Clipped, CellsReviewCellSet Overflowing) InspectWideValues(Worksheet sheet)
    {
        Aspose.Cells.Cells cells = sheet.Cells;
        Workbook workbook = sheet.Workbook;
        double standardFontSize = workbook.DefaultStyle.Font.Size;
        double largestFontScale = Math.Max(1, Enumerable.Range(0, workbook.CountOfStylesInPool)
            .Select(index => workbook.GetStyleInPool(index).Font.Size).DefaultIfEmpty(0).Max() / standardFontSize);
        var clipped = new CellSetBuilder();
        var overflowing = new CellSetBuilder();
        foreach (Cell cell in cells)
        {
            if (cell.Value is null or "" || cell.IsMerged || cells.IsColumnHidden(cell.Column))
            {
                continue;
            }

            bool number = cell.Type is CellValueType.IsNumeric or CellValueType.IsDateTime;
            if (!number && cell.Type != CellValueType.IsString)
            {
                continue;
            }

            // A font larger than the standard one widens every character, so the largest font
            // bounds the width before the cell's own style is read. GetWidthOfValue measures East
            // Asian text in a font without its glyphs unlike AutoFit and rendering (known issue
            // CELLS-WIDTH-EAST-ASIAN, KNOWN-ISSUES.md); the finding says so when its samples hold
            // such text.
            double units = DisplayUnits(cell.StringValue);
            double columnWidth = cells.GetColumnWidth(cell.Column);
            if (units * largestFontScale <= columnWidth)
            {
                continue;
            }

            Style style = cell.GetStyle();
            if (style.IsTextWrapped || style.ShrinkToFit
                || units * Math.Max(1, style.Font.Size / standardFontSize) <= columnWidth
                || cell.GetWidthOfValue() <= cells.GetColumnWidthPixel(cell.Column))
            {
                continue;
            }

            bool spills = !number && cells.CheckCell(cell.Row, cell.Column + 1) is not { Value: not (null or "") };
            if (!spills)
            {
                clipped.Add(cell.Name, !number && cell.StringValue.Any(IsEastAsian));
            }
            else if (EndsNearColumnEdge(cells, cell))
            {
                overflowing.Add(cell.Name, cell.StringValue.Any(IsEastAsian));
            }
        }

        return (clipped.Build(), overflowing.Build());
    }

    /// <summary>
    /// Whether text that spills over the empty cells to its right ends close to the right edge
    /// of one of the columns it spans, where page layout can cut its last character (known issue
    /// CELLS-OVERFLOW-EDGE). Page layout sizes columns up to about 6% apart from the cell model
    /// that the widths here come from, so an edge counts as close within 6% of its distance
    /// from the cell, and never less than 3 pixels.
    /// </summary>
    private static bool EndsNearColumnEdge(Aspose.Cells.Cells cells, Cell cell)
    {
        int end = cell.GetWidthOfValue();
        int edge = 0;
        for (int column = cell.Column; column < 16384; column++)
        {
            edge += cells.GetColumnWidthPixel(column);
            if (Math.Abs(end - edge) <= Math.Max(3, edge * 0.06))
            {
                return true;
            }
            if (edge > end || cells.CheckCell(cell.Row, column + 1) is { Value: not (null or "") })
            {
                return false;
            }
        }
        return false;
    }

    private sealed class CellSetBuilder
    {
        private readonly List<string> _samples = [];
        private int _count;
        private bool _eastAsian;

        internal void Add(string name, bool eastAsian)
        {
            _count++;
            if (_samples.Count < MaxDimensionSamples)
            {
                _samples.Add(name);
                _eastAsian |= eastAsian;
            }
        }

        internal CellsReviewCellSet Build() => new(_count, _samples, _eastAsian);
    }

    // Column widths are measured in characters of the default font; East Asian characters take two.
    private static int DisplayUnits(string text) =>
        text.Sum(static character => IsEastAsian(character) ? 2 : 1);

    private static bool IsEastAsian(char character) => character >= '⺀';

    private static DimensionScan InspectColumns(
        Worksheet sheet,
        IReadOnlyList<bool> occupied)
    {
        var scan = new DimensionScan();
        for (int column = 0; column < occupied.Count; column++)
        {
            if (!occupied[column])
            {
                continue;
            }
            double width = sheet.Cells.GetColumnWidth(column);
            if (sheet.Cells.IsColumnHidden(column))
            {
                scan.Hidden.Add(column);
            }
            else if (width < NarrowColumnWidth)
            {
                scan.Small.Add(column);
            }
            else if (width > WideColumnWidth)
            {
                scan.Large.Add(column);
            }
        }
        return scan;
    }

    private static DimensionScan InspectRows(
        Worksheet sheet,
        IReadOnlyList<bool> occupied)
    {
        var scan = new DimensionScan();
        for (int row = 0; row < occupied.Count; row++)
        {
            if (!occupied[row])
            {
                continue;
            }
            double height = sheet.Cells.GetRowHeight(row);
            if (sheet.Cells.IsRowHidden(row))
            {
                scan.Hidden.Add(row);
            }
            else if (height < ShortRowHeight)
            {
                scan.Small.Add(row);
            }
            else if (height > TallRowHeight)
            {
                scan.Large.Add(row);
            }
        }
        return scan;
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
        foreach ((Chart chart, int pages) in PrintedPages.ChartPages(sheet))
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
                PrintedPages = pages,
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

    /// <summary>The hidden, too small and too large populated dimensions of one axis.</summary>
    private sealed class DimensionScan
    {
        public DimensionAccumulator Hidden { get; } = new();

        public DimensionAccumulator Small { get; } = new();

        public DimensionAccumulator Large { get; } = new();
    }

    /// <summary>Counts every dimension in one condition and keeps the first few indexes.</summary>
    private sealed class DimensionAccumulator
    {
        private readonly List<int> _samples = [];
        private int _count;

        public void Add(int index)
        {
            _count++;
            if (_samples.Count < MaxDimensionSamples)
            {
                _samples.Add(index);
            }
        }

        public CellsReviewDimensionSet ToSet() => new(_count, _samples.ToArray());
    }

    private sealed record PrintAreaScan(
        string? Value,
        IReadOnlyList<RangeRef> Ranges,
        bool Invalid,
        bool ExcludesContent,
        bool Excessive);

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
