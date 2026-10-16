using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cells.Drawing;
using Aspose.Cells.Pivot;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Builds bounded, SDK-neutral layout facts for visual review.</summary>
internal static class ReviewLayoutProjection
{
    private const int MaxDimensionSamples = 5;
    private const double NarrowColumnWidth = 3;
    private const double WideColumnWidth = 80;
    private const double ShortRowHeight = 8;
    private const double TallRowHeight = 120;

    // The indent the engine draws a pivot table's outer column item labels with, in pixels at
    // 96 DPI (CELLS-PIVOT-LABEL-INDENT), and in characters of the default font rounded up (about
    // 2.3 in Arial 10), so the character pre-checks never skip a label the pixel check reports.
    private const int PivotLabelIndentPixels = 16;
    private const double PivotLabelIndentUnits = 3;

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
        bool warningSheet = CellsEvaluation.IsWarningSheet(sheet);

        return new CellsReviewSheetLayout
        {
            Name = sheet.Name,
            UsedAreaCells = content.UsedAreaCells,
            PopulatedCells = content.PopulatedCells,
            ContentRange = content.ContentRange is { } range ? A1.FormatRange(range) : null,
            HasVisualObjects = sheet.Shapes.Count > 0,
            IsEvaluationWarning = warningSheet,
            EvaluationNoticeRow = warningSheet ? null : CellsEvaluation.NoticeRow(sheet),
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
    /// filled (so it cannot spill over), and numbers and dates, which it shows as #### or rounded,
    /// as the engine's display text tells; other text spills over the empty cells to its right.
    /// Only values whose length, scaled by their font size, could exceed the column are measured.
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
        HashSet<(int Row, int Column)> pivotLabels = IndentedPivotLabels(sheet);
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
            // Column widths are measured in characters of the default font; East Asian Wide and
            // Fullwidth characters take two.
            bool pivotLabel = pivotLabels.Contains((cell.Row, cell.Column));
            double units = TextWidth.Of(cell.StringValue) + (pivotLabel ? PivotLabelIndentUnits : 0);
            double columnWidth = cells.GetColumnWidth(cell.Column);
            // A number needs room for the cell's padding too: with less than two characters to
            // spare, the engine's own display text says whether it shows in full.
            if (units * largestFontScale + (number ? 2 : 0) <= columnWidth)
            {
                continue;
            }

            if (number)
            {
                if (!string.Equals(cell.GetStringValue(CellValueFormatStrategy.DisplayString),
                        cell.GetStringValue(CellValueFormatStrategy.CellStyle), StringComparison.Ordinal)
                    && !cell.GetStyle().ShrinkToFit)
                {
                    clipped.Add(cell.Name, eastAsian: false);
                }

                continue;
            }

            Style style = cell.GetStyle();
            if (style.IsTextWrapped || style.ShrinkToFit
                || units * Math.Max(1, style.Font.Size / standardFontSize) <= columnWidth
                || cell.GetWidthOfValue() + (pivotLabel ? PivotLabelIndentPixels : 0) <= cells.GetColumnWidthPixel(cell.Column))
            {
                continue;
            }

            bool spills = !pivotLabel && cells.CheckCell(cell.Row, cell.Column + 1) is not { Value: not (null or "") };
            if (!spills)
            {
                clipped.Add(cell.Name, cell.StringValue.Any(IsEastAsian), pivotLabel);
            }
            else if (EndsNearColumnEdge(cells, cell))
            {
                overflowing.Add(cell.Name, cell.StringValue.Any(IsEastAsian));
            }
        }

        return (clipped.Build(), overflowing.Build());
    }

    /// <summary>
    /// The cells of the item labels of each pivot table column field but the innermost, such as
    /// regions over products. The engine draws such a label indented and cuts it off at its own
    /// column's edge, though the cells to its right are empty, and auto-fit leaves the indent out
    /// (known issue CELLS-PIVOT-LABEL-INDENT, KNOWN-ISSUES.md), so the label is measured with the
    /// indent and never counts as spilling. The value field captions, the subtotal and grand total
    /// labels and the innermost field's items are drawn like any cell. The last row of a pivot
    /// table's column range holds the innermost field's items, the rows above it the outer fields'.
    /// </summary>
    private static HashSet<(int Row, int Column)> IndentedPivotLabels(Worksheet sheet)
    {
        var labels = new HashSet<(int Row, int Column)>();
        foreach (PivotTable pivot in sheet.PivotTables)
        {
            PivotFieldCollection fields = pivot.ColumnFields;
            CellArea columns = pivot.ColumnRange;
            for (int index = 0; index < fields.Count - 1; index++)
            {
                PivotField field = fields[index];
                if (field.IsValuesField)
                {
                    continue;
                }

                var items = new HashSet<string>(field.Items ?? [], StringComparer.Ordinal);
                int row = columns.EndRow - (fields.Count - 1 - index);
                for (int column = columns.StartColumn; column <= columns.EndColumn; column++)
                {
                    if (sheet.Cells.CheckCell(row, column) is { Type: CellValueType.IsString } cell && items.Contains(cell.StringValue))
                    {
                        labels.Add((row, column));
                    }
                }
            }
        }

        return labels;
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
        for (int column = cell.Column; column < A1.MaxColumns; column++)
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
        private bool _pivotLabels;

        internal void Add(string name, bool eastAsian, bool pivotLabel = false)
        {
            _count++;
            if (_samples.Count < MaxDimensionSamples)
            {
                _samples.Add(name);
                _eastAsian |= eastAsian;
                _pivotLabels |= pivotLabel;
            }
        }

        internal CellsReviewCellSet Build() => new(_count, _samples, _eastAsian, _pivotLabels);
    }

    // Any character from the CJK radicals on, halfwidth forms included, which a Latin default
    // font has no glyph for (CELLS-WIDTH-EAST-ASIAN).
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
