using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Contracts.Reading;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Builds the <c>cells query range</c> projection: resolves the requested sheet,
/// applies the cell budget to pick the returned range, and materializes the row-major
/// cell matrix (with the pooled style dictionary in style-bearing scopes).
/// Mirrors <see cref="InfoProjection"/> so the engine stays a thin orchestrator.
/// </summary>
internal static class ReadProjection
{
    /// <summary>
    /// Projects one sheet's budgeted cell data. Returns the sheet projection, the style pool
    /// (null outside style-bearing scopes, or when every cell carries the workbook default
    /// style) and the read's window; the read command adds the window's next page.
    /// </summary>
    public static (SheetProjection Sheet, IReadOnlyDictionary<string, StyleData>? Styles, ResultWindow Window) Project(
        ResourceBudgetLedger resourceBudgets,
        Workbook workbook,
        ReadRequest request)
    {
        Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
        RangeRef? usedRange = Sheets.UsedRange(sheet);
        ReadPlan plan = ReadWindowPlanner.Plan(request.Range, request.Scan, usedRange, request.MaxCells);
        RangeRef? window = plan.Window;
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
            Position = sheet.Index,
            UsedRange = usedRange is { } used ? A1.FormatRange(used) : null,
            Range = window is { } w ? A1.FormatRange(w) : null,
            Cells = cells,
        };

        return (projection, stylePool?.ToDictionary(), Window(plan));
    }

    /// <summary>
    /// The read's window counted in cells, the unit of the cell budget and of each scan
    /// page. The total is the covered region's cell count.
    /// </summary>
    private static ResultWindow Window(ReadPlan plan)
    {
        // The budget caps a returned range at one million cells, so the cast is exact.
        int returned = (int)(plan.Window?.CellCount ?? 0);
        long total = plan.Region?.CellCount ?? returned;
        return new ResultWindow
        {
            Unit = "cell",
            Returned = returned,
            Total = total,
            Truncated = plan.Next is not null,
        };
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
