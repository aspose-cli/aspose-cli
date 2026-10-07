namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// Assembles the ready-to-run follow-up commands of a planned scan: a chain of
/// <c>cells query range</c> pages that together cover one bounded region of a sheet.
/// The engine returns the projection and its window; the command spells the follow-up
/// with the shared continuation builder, so agents run it verbatim instead of computing
/// ranges.
/// </summary>
internal static class NextReadCommand
{
    /// <summary>The hidden option that carries the region a scan covers from page to page.</summary>
    public const string ScanOption = "--scan-range";

    /// <summary>
    /// The follow-up command of a read, or null when nothing remains. The page is planned
    /// again from the read's own inputs and the used range it reported, so the geometry
    /// stays in <see cref="ReadWindowPlanner"/> and agrees with the engine's window.
    /// </summary>
    /// <param name="result">The read whose follow-up is planned.</param>
    /// <param name="range">The read's explicit range or current page.</param>
    /// <param name="scan">The region the read's command carried; null otherwise.</param>
    /// <param name="resume">The invocation's continuation (<see cref="StandardInvocation.Continuation"/>).</param>
    /// <param name="maxCells">The cell budget of one page.</param>
    public static string? Build(
        ContinuationCommand resume, WorkbookReadResult result, RangeRef? range, RangeRef? scan, int maxCells)
    {
        RangeRef? used = result.Sheet.UsedRange is { } text ? A1.ParseRange(text).Range : null;
        ReadPlan plan = ReadWindowPlanner.Plan(range, scan, used, maxCells);
        return plan is { Next: { } next, Region: { } region }
            ? Page(resume, result.Sheet.Name, next, region, result.Scope, maxCells)
            : null;
    }

    /// <summary>The first page of a scan over an explicit region too large for one read.</summary>
    public static string First(ContinuationCommand resume, string? sheet, RangeRef region, string scope, int maxCells) =>
        Page(resume, sheet, ReadWindowPlanner.FirstWindow(region, maxCells), region, scope, maxCells);

    private static string Page(ContinuationCommand command, string? sheet, RangeRef window, RangeRef region, string scope, int maxCells)
    {
        if (sheet is not null)
        {
            command.Option("--sheet", sheet);
        }

        return command
            .Option("--range", A1.FormatRange(window))
            .Option(ScanOption, A1.FormatRange(region))
            .Option("--scope", scope)
            .Option("--max-cells", maxCells)
            .ToString();
    }
}
