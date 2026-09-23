using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Reading;

/// <summary>
/// The read cell-budget geometry as pure functions: which window a read
/// returns, and the follow-up window that pages through the rest. It remains
/// independent of the engine SDK so the behavior is unit-testable without
/// opening a workbook.
/// </summary>
internal static class ReadWindowPlanner
{
    /// <summary>
    /// Applies the cell budget to a read. An explicit over-budget range is an
    /// honest error; an over-budget default read degrades to a summary (no
    /// window) the caller pages through, instead of flooding it.
    /// </summary>
    /// <returns>
    /// The window to read — null for a summary-only response — and whether the
    /// read was truncated to a summary.
    /// </returns>
    /// <exception cref="CliException"><c>RANGE_TOO_LARGE</c> when an explicit range exceeds the budget.</exception>
    public static (RangeRef? Window, bool Truncated) DecideWindow(
        RangeRef? explicitRange, int maxCells, RangeRef? usedRange)
    {
        if (explicitRange is { } range)
        {
            if (range.CellCount > maxCells)
            {
                RangeRef suggested = FirstWindow(range, maxCells);
                throw CellsErrors.RangeTooLarge(
                    range.CellCount,
                    maxCells,
                    $"Request at most {maxCells} cells per call, such as --range {A1.FormatRange(suggested)}, or raise --max-cells.");
            }

            return (range, false);
        }

        if (usedRange is not { } used)
        {
            return (null, false);
        }

        return used.CellCount <= maxCells
            ? (used, false)
            : (null, true);
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
    /// The follow-up window, or null when the chain has covered everything it
    /// promises to. A truncated summary suggests the first window; otherwise
    /// the window shifts down one page of the same shape.
    /// </summary>
    /// <param name="windowWasPlanned">
    /// True when this CLI chose the window (a read without an explicit range),
    /// in which case the chain owes the caller the WHOLE used range: when the
    /// budget is smaller than the used range is wide, the window clamps to the
    /// leftmost columns, and paging down alone would end — with no 'next' and
    /// truncated:false — having never returned the right-hand columns. The
    /// caller, doing exactly what the skill tells it ("execute it verbatim
    /// instead of computing ranges yourself"), would take that for the whole
    /// sheet. So a planned chain moves to the next column band once its rows
    /// run out. An explicit range is the caller's own choice of columns: its
    /// chain keeps walking down those columns and never wanders sideways.
    /// </param>
    public static RangeRef? NextWindow(
        RangeRef? window, RangeRef? usedRange, int maxCells, bool truncated, bool windowWasPlanned = true)
    {
        if (usedRange is not { } used)
        {
            return null;
        }

        if (truncated)
        {
            // Summary-only response: suggest the first window of the data.
            return FirstWindow(used, maxCells);
        }

        if (window is not { } w || w.Start.Column > used.End.Column)
        {
            return null;
        }

        if (w.End.Row < used.End.Row)
        {
            // More rows below the window: same shape, shifted down.
            int nextStartRow = w.End.Row + 1;
            int nextEndRow = Math.Min(nextStartRow + w.RowCount - 1, used.End.Row);
            return new RangeRef(
                new CellRef(nextStartRow, w.Start.Column),
                new CellRef(nextEndRow, w.End.Column));
        }

        if (windowWasPlanned && w.End.Column < used.End.Column)
        {
            // Rows exhausted but columns remain: start the next band at the top.
            int startColumn = w.End.Column + 1;
            int endColumn = Math.Min(startColumn + w.ColumnCount - 1, used.End.Column);
            int rows = Math.Min(used.RowCount, Math.Max(1, maxCells / (endColumn - startColumn + 1)));
            return new RangeRef(
                new CellRef(used.Start.Row, startColumn),
                new CellRef(Math.Min(used.Start.Row + rows - 1, used.End.Row), endColumn));
        }

        return null;
    }
}
