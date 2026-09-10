using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Reading;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Builds the <c>cells query range</c> projection: resolves the requested sheet,
/// applies the cell budget to pick the window, and materializes the row-major
/// cell matrix (with the pooled style dictionary in style-bearing scopes).
/// Mirrors <see cref="InfoProjection"/> so the engine stays a thin orchestrator.
/// </summary>
internal static class ReadProjection
{
    /// <summary>
    /// Projects one sheet's windowed cell data. Returns the sheet projection and
    /// the style pool (null outside style-bearing scopes, or when every cell
    /// carries the workbook default style).
    /// </summary>
    public static (SheetProjection Sheet, IReadOnlyDictionary<string, StyleData>? Styles) Project(
        ResourceBudgetLedger resourceBudgets,
        Workbook workbook,
        ReadRequest request)
    {
        Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
        RangeRef? usedRange = Sheets.UsedRange(sheet);
        (RangeRef? window, bool truncated) = ReadWindowPlanner.DecideWindow(request.Range, request.MaxCells, usedRange);
        resourceBudgets.EnsureWithin(
            CellsBudgetDomains.Cells,
            window?.CellCount ?? 0,
            "items",
            phase: "projection");

        StylePool? stylePool = request.Scope.IncludesStyles() ? new StylePool(workbook) : null;
        IReadOnlyList<IReadOnlyList<CellData>>? cells = window is { } resolvedWindow
            ? BuildCells(sheet, resolvedWindow, request.Scope, stylePool)
            : null;

        var projection = new SheetProjection
        {
            Name = sheet.Name,
            Index = sheet.Index,
            UsedRange = usedRange is { } used ? A1.FormatRange(used) : null,
            Window = window is { } w ? A1.FormatRange(w) : null,
            Truncated = truncated,
            Cells = cells,
        };

        return (projection, stylePool?.ToDictionary());
    }

    private static IReadOnlyList<IReadOnlyList<CellData>> BuildCells(
        Worksheet sheet, RangeRef window, ReadScope scope, StylePool? stylePool)
    {
        bool includeFormulas = scope.IncludesFormulas();
        var rows = new List<IReadOnlyList<CellData>>(window.RowCount);

        for (int row = window.Start.Row; row <= window.End.Row; row++)
        {
            var cells = new CellData[window.ColumnCount];
            for (int column = window.Start.Column; column <= window.End.Column; column++)
            {
                // CheckCell avoids materializing cells that were never written.
                Cell? cell = sheet.Cells.CheckCell(row, column);
                cells[column - window.Start.Column] = CellMapper.Map(
                    cell, includeFormulas, stylePool?.GetId(cell));
            }

            rows.Add(cells);
        }

        return rows;
    }
}
