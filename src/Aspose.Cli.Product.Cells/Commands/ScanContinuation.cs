namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// The <c>next</c> of a planned scan: the option values of the <c>cells read</c> page that
/// follows, a chain of pages that together cover one bounded region of a sheet.
/// </summary>
internal static class ScanContinuation
{
    /// <summary>The hidden option that carries the region a scan covers from page to page.</summary>
    public const string ScanOption = "--scan-range";

    /// <summary>
    /// The option values of the read's next page, or null when nothing remains. The page is
    /// planned again from the read's own inputs and the used range it reported, so the geometry
    /// stays in <see cref="ReadWindowPlanner"/> and agrees with the engine's window.
    /// </summary>
    /// <param name="result">The read whose next page is planned.</param>
    /// <param name="range">The read's explicit range or current page.</param>
    /// <param name="scan">The region the read's command named; null otherwise.</param>
    /// <param name="maxCells">The cell budget of one page.</param>
    public static IReadOnlyDictionary<string, string>? Build(CellsReadResult result, RangeRef? range, RangeRef? scan, int maxCells)
    {
        RangeRef? used = result.Sheet.UsedRange is { } text ? A1.ParseRange(text).Range : null;
        ReadPlan plan = ReadWindowPlanner.Plan(range, scan, used, maxCells);
        return plan is { Next: { } next, Region: { } region }
            ? ResultWindow.Options(("--sheet", result.Sheet.Name), ("--range", A1.FormatRange(next)), (ScanOption, A1.FormatRange(region)))
            : null;
    }
}
