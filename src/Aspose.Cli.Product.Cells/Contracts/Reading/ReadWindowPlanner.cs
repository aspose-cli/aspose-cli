using Aspose.Cli.Product.Cells.Contracts.Addressing;

namespace Aspose.Cli.Product.Cells.Contracts.Reading;

/// <summary>One planned read: the range it returns, the region it covers, and the page after it.</summary>
/// <param name="Window">The range returned as cells; null for a summary-only or empty read.</param>
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
    /// Plans a read. An explicit range without a scan region is a complete request: it is
    /// returned whole and has no next page (the read command refuses one over the budget
    /// before the engine opens the workbook). A default read scans the used range and a
    /// generated page scans the region its command carries; an over-budget default read
    /// degrades to a summary whose next page is the first window of the data.
    /// </summary>
    /// <param name="range">The explicit range, or the current page of a scan.</param>
    /// <param name="scan">The region a generated page belongs to; null otherwise.</param>
    /// <param name="usedRange">The sheet's used range; null for an empty sheet.</param>
    /// <param name="maxCells">The cell budget of one read.</param>
    public static ReadPlan Plan(RangeRef? range, RangeRef? scan, RangeRef? usedRange, int maxCells)
    {
        if (range is { } requested && scan is null)
        {
            return new ReadPlan(requested, requested, null);
        }

        if (usedRange is not { } used)
        {
            return new ReadPlan(range, null, null);
        }

        RangeRef? window = range ?? (used.CellCount <= maxCells ? used : null);
        RangeRef? region = Intersect(scan ?? used, used);
        RangeRef? next = region is { } bounds ? NextWindow(window, bounds, maxCells) : null;
        return new ReadPlan(window, region, next);
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
    /// the scan has covered everything it promises to. A summary (no window) continues with
    /// the first window; otherwise the window shifts down one page of the same shape.
    /// </summary>
    /// <remarks>
    /// A scan owes the caller the WHOLE region: when the budget is smaller than the region
    /// is wide, the window clamps to the leftmost columns, and paging down alone would end
    /// having never returned the right-hand columns. So the scan moves to the next column
    /// band once its rows run out.
    /// </remarks>
    private static RangeRef? NextWindow(RangeRef? window, RangeRef region, int maxCells)
    {
        if (window is not { } w)
        {
            return FirstWindow(region, maxCells);
        }

        if (w.Start.Column > region.End.Column)
        {
            return null;
        }

        if (w.End.Row < region.End.Row)
        {
            // More rows below the window: same shape, shifted down.
            int nextStartRow = w.End.Row + 1;
            int nextEndRow = Math.Min(nextStartRow + w.RowCount - 1, region.End.Row);
            return new RangeRef(
                new CellRef(nextStartRow, w.Start.Column),
                new CellRef(nextEndRow, w.End.Column));
        }

        if (w.End.Column < region.End.Column)
        {
            // Rows exhausted but columns remain: start the next band at the top.
            int startColumn = w.End.Column + 1;
            int endColumn = Math.Min(startColumn + w.ColumnCount - 1, region.End.Column);
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
