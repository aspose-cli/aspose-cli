using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Reading;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// Assembles the ready-to-run follow-up commands of a planned scan: a chain of
/// <c>cells query range</c> windows that together cover one bounded region of a sheet.
/// The engine returns the projection; the command spells the follow-up with the shared
/// continuation builder, so agents run it verbatim instead of computing ranges.
/// </summary>
internal static class NextReadCommand
{
    /// <summary>The hidden option that carries the region a scan covers from page to page.</summary>
    public const string ScanOption = "--scan-range";

    /// <summary>
    /// The follow-up command for <paramref name="result"/>, or null when the scan covered
    /// its region. The next window is recomputed from the returned window, the region and
    /// the truncated flag, so the geometry stays in <see cref="ReadWindowPlanner"/>.
    /// </summary>
    /// <param name="region">The region the scan covers; the used range bounds it.</param>
    public static string? Build(string filePath, WorkbookReadResult result, int maxCells, RangeRef region)
    {
        if (result.Sheet.UsedRange is not { } used
            || Intersect(region, A1.ParseRange(used).Range) is not { } bounds)
        {
            return null;
        }

        RangeRef? window = result.Sheet.Window is { } text ? A1.ParseRange(text).Range : null;
        return ReadWindowPlanner.NextWindow(window, bounds, maxCells, result.Sheet.Truncated) is { } next
            ? Page(filePath, result.Sheet.Name, next, bounds, result.Scope, maxCells)
            : null;
    }

    /// <summary>The first page of a scan over an explicit region too large for one read.</summary>
    public static string First(string filePath, string? sheet, RangeRef region, string scope, int maxCells) =>
        Page(filePath, sheet, ReadWindowPlanner.FirstWindow(region, maxCells), region, scope, maxCells);

    private static string Page(string filePath, string? sheet, RangeRef window, RangeRef region, string scope, int maxCells)
    {
        var command = new ContinuationCommand("cells", "query", "range").Argument(filePath);
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
