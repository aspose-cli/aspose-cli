namespace Aspose.Cli.Product.Cells.Contracts.Reading;

/// <summary>One planned read: the range it returns, the region it covers, and the page after it.</summary>
/// <param name="Window">The range returned as cells; null when the read covers no data.</param>
/// <param name="Region">
/// The cells the read covers: an explicit range, or for a planned scan the scanned region
/// within the used range. Null when the sheet has no data to scan.
/// </param>
/// <param name="Next">The following page of a planned scan; null when nothing remains.</param>
internal readonly record struct ReadPlan(RangeRef? Window, RangeRef? Region, RangeRef? Next);

/// <summary>
/// The read cell-budget geometry as pure functions: which range a read returns, and the
/// follow-up page that scans the rest. The engine reports the read's window from this plan
/// and the read command spells the next page from the same plan, so both agree without
/// repeating the geometry. It is independent of the engine SDK so the behavior is
/// unit-testable without opening a workbook.
/// </summary>
internal static class ReadWindowPlanner
{
    /// <summary>
    /// Plans a read. A read within the budget returns its range whole: an explicit range as
    /// requested, by default the used range. A read over the budget is a planned scan of the
    /// data it covers (the used range, the part of an explicit range inside it, or the region a
    /// generated page's command carries): it returns the first window, or the page its command
    /// names, and the page after it.
    /// </summary>
    /// <param name="range">The explicit range, or the current page of a scan.</param>
    /// <param name="scan">The region a generated page belongs to; null otherwise.</param>
    /// <param name="usedRange">The sheet's used range; null for an empty sheet.</param>
    /// <param name="maxCells">The cell budget of one read.</param>
    public static ReadPlan Plan(RangeRef? range, RangeRef? scan, RangeRef? usedRange, int maxCells)
    {
        if (range is { } requested && scan is null && requested.CellCount <= maxCells)
        {
            return new ReadPlan(requested, requested, null);
        }

        if (usedRange is not { } used || Intersect(scan ?? range ?? used, used) is not { } region)
        {
            return new ReadPlan(null, null, null);
        }

        RangeRef window = scan is not null && range is { } page
            ? page
            : region.CellCount <= maxCells ? region : FirstWindow(region, maxCells);
        return new ReadPlan(window, region, NextWindow(window, region, maxCells));
    }

    /// <summary>First window of at most <paramref name="maxCells"/> cells inside a range.</summary>
    public static RangeRef FirstWindow(RangeRef range, int maxCells)
    {
        int columns = Math.Min(range.ColumnCount, Math.Max(1, maxCells));
        int rows = Math.Min(range.RowCount, Math.Max(1, maxCells / columns));
        return new RangeRef(
            range.Start,
            new CellRef(range.Start.Row + rows - 1, range.Start.Column + columns - 1));
    }

    /// <summary>
    /// The page after <paramref name="window"/> inside <paramref name="region"/>, or null when
    /// the scan has covered everything it promises to: the window shifts down one page of the
    /// same shape.
    /// </summary>
    /// <remarks>
    /// A scan owes the caller the WHOLE region: when the budget is smaller than the region
    /// is wide, the window clamps to the leftmost columns, and paging down alone would end
    /// having never returned the right-hand columns. So the scan moves to the next column
    /// band once its rows run out.
    /// </remarks>
    private static RangeRef? NextWindow(RangeRef window, RangeRef region, int maxCells)
    {
        if (window.Start.Column > region.End.Column)
        {
            return null;
        }

        if (window.End.Row < region.End.Row)
        {
            // More rows below the window: same shape, shifted down.
            int nextStartRow = window.End.Row + 1;
            int nextEndRow = Math.Min(nextStartRow + window.RowCount - 1, region.End.Row);
            return new RangeRef(
                new CellRef(nextStartRow, window.Start.Column),
                new CellRef(nextEndRow, window.End.Column));
        }

        if (window.End.Column < region.End.Column)
        {
            // Rows exhausted but columns remain: start the next band at the top.
            int startColumn = window.End.Column + 1;
            int endColumn = Math.Min(startColumn + window.ColumnCount - 1, region.End.Column);
            int rows = Math.Min(region.RowCount, Math.Max(1, maxCells / (endColumn - startColumn + 1)));
            return new RangeRef(
                new CellRef(region.Start.Row, startColumn),
                new CellRef(Math.Min(region.Start.Row + rows - 1, region.End.Row), endColumn));
        }

        return null;
    }

    private static RangeRef? Intersect(RangeRef left, RangeRef right)
    {
        int top = Math.Max(left.Start.Row, right.Start.Row);
        int leftColumn = Math.Max(left.Start.Column, right.Start.Column);
        int bottom = Math.Min(left.End.Row, right.End.Row);
        int rightColumn = Math.Min(left.End.Column, right.End.Column);
        return top <= bottom && leftColumn <= rightColumn
            ? new RangeRef(new CellRef(top, leftColumn), new CellRef(bottom, rightColumn))
            : null;
    }
}
