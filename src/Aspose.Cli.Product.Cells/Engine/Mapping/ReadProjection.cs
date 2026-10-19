using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Builds the <c>cells read</c> projection: resolves the requested sheet,
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
    public static (CellsSheetProjection Sheet, IReadOnlyDictionary<string, CellsStyleData>? Styles, ResultWindow Window) Project(
        ResourceBudgetLedger resourceBudgets,
        Workbook workbook,
        CellsReadRequest request)
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

        StylePool? stylePool = CellsReadContents.IncludesStyles(request.Content) ? new StylePool(workbook) : null;
        IReadOnlyList<IReadOnlyList<CellsCellData>>? cells = window is { } resolvedWindow
            ? BuildCells(sheet, resolvedWindow, request.Content, stylePool)
            : null;

        var projection = new CellsSheetProjection
        {
            Name = sheet.Name,
            Position = sheet.Index + 1,
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

    private static IReadOnlyList<IReadOnlyList<CellsCellData>> BuildCells(
        Worksheet sheet, RangeRef window, string content, StylePool? stylePool)
    {
        bool includeFormulas = CellsReadContents.IncludesFormulas(content);
        var rows = new List<IReadOnlyList<CellsCellData>>(window.RowCount);

        for (int row = window.Start.Row; row <= window.End.Row; row++)
        {
            var cells = new CellsCellData[window.ColumnCount];
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
