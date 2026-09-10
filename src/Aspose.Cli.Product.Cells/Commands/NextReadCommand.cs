using System.Globalization;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Reading;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// Assembles the ready-to-run follow-up command a windowed read advertises in
/// its <c>next</c> field. This lives in the CLI, not the engine, because the
/// command is this CLI's own spelling — a different surface (say, an MCP
/// wrapper) would advertise its own. Agents run it verbatim instead of
/// computing ranges themselves.
/// </summary>
internal static class NextReadCommand
{
    /// <summary>
    /// The follow-up command for <paramref name="result"/>, or null when the
    /// read already covered the data. The next window is recomputed from the
    /// projection the engine returned — its window, used range and truncated
    /// flag — so the geometry stays in one place (<see cref="ReadWindowPlanner"/>).
    /// </summary>
    /// <param name="windowWasPlanned">True for every generated scan page.</param>
    public static string? Build(string filePath, WorkbookReadResult result, int maxCells, bool windowWasPlanned)
    {
        RangeRef? usedRange = result.Sheet.UsedRange is { } used ? A1.ParseRange(used).Range : null;
        RangeRef? window = result.Sheet.Window is { } w ? A1.ParseRange(w).Range : null;

        if (ReadWindowPlanner.NextWindow(
                window, usedRange, maxCells, result.Sheet.Truncated, windowWasPlanned) is not { } next)
        {
            return null;
        }

        // A planned scan carries --continue-scan so the next page keeps covering
        // the whole used range; a caller's explicit-range chain does not, and
        // stays within the columns the caller chose.
        var parts = new List<string>(12)
        {
            "aspose-cli", "cells", "query", "range", Quote(filePath),
            "--sheet", Quote(result.Sheet.Name),
            "--range", A1.FormatRange(next),
        };
        if (windowWasPlanned)
        {
            parts.Add("--continue-scan");
        }

        parts.AddRange([
            "--scope", result.Scope,
            "--max-cells", maxCells.ToString(CultureInfo.InvariantCulture),
            "--output", "json",
        ]);
        return string.Join(' ', parts);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
