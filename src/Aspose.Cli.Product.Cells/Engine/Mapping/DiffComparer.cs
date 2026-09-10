using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Compares two workbooks sheet-by-sheet and cell-by-cell. Sheets match by
/// name; cells are compared over the union of both used ranges (empty cells are
/// never materialized). Values are always compared; formulas only when asked.
/// Output is deterministic: sheets in worksheet order, cells row-major.
/// </summary>
internal static class DiffComparer
{
    /// <summary>The outcome of a comparison.</summary>
    public sealed record Result(
        IReadOnlyList<SheetDiff> Sheets, DiffSummary Summary, bool Identical, bool Truncated);

    public static Result Compare(Workbook left, Workbook right, bool includeFormulas, int maxDiffs)
    {
        var sheets = new List<SheetDiff>();
        int added = 0;
        int removed = 0;
        int modified = 0;
        int cellsDiffering = 0;
        bool truncated = false;

        // Sheets present only on the left are removed.
        foreach (Worksheet ls in left.Worksheets)
        {
            if (right.Worksheets[ls.Name] is null)
            {
                removed++;
                sheets.Add(new SheetDiff { Name = ls.Name, Status = "removed" });
            }
        }

        // Sheets present only on the right are added; shared sheets are compared.
        foreach (Worksheet rs in right.Worksheets)
        {
            Worksheet? ls = left.Worksheets[rs.Name];
            if (ls is null)
            {
                added++;
                sheets.Add(new SheetDiff { Name = rs.Name, Status = "added" });
                continue;
            }

            var cells = new List<CellDiff>();
            int maxRow = Math.Max(ls.Cells.MaxDataRow, rs.Cells.MaxDataRow);
            int maxColumn = Math.Max(ls.Cells.MaxDataColumn, rs.Cells.MaxDataColumn);
            bool sheetModified = false;

            for (int row = 0; row <= maxRow; row++)
            {
                for (int column = 0; column <= maxColumn; column++)
                {
                    CellData a = CellMapper.Map(ls.Cells.CheckCell(row, column), includeFormulas, null);
                    CellData b = CellMapper.Map(rs.Cells.CheckCell(row, column), includeFormulas, null);
                    if (Equals(a.V, b.V) && (!includeFormulas || a.F == b.F))
                    {
                        continue;
                    }

                    // Count every difference so cellsDiffering is the true total,
                    // not the display cap: a summary that shrank to whatever
                    // --max-diffs happened to be would under-report the change.
                    // The cap only bounds the LISTED cells; truncated says the
                    // list is partial, cellsDiffering says how many there really are.
                    cellsDiffering++;
                    sheetModified = true;

                    if (cells.Count < maxDiffs)
                    {
                        cells.Add(new CellDiff
                        {
                            Cell = A1.FormatCell(new CellRef(row, column)),
                            Left = SideOf(a),
                            Right = SideOf(b),
                        });
                    }
                    else
                    {
                        truncated = true;
                    }
                }
            }

            if (sheetModified)
            {
                modified++;
                sheets.Add(new SheetDiff { Name = rs.Name, Status = "modified", Cells = cells });
            }
        }

        var summary = new DiffSummary
        {
            SheetsAdded = added,
            SheetsRemoved = removed,
            SheetsModified = modified,
            CellsDiffering = cellsDiffering,
        };
        bool identical = added == 0 && removed == 0 && cellsDiffering == 0;
        return new Result(sheets, summary, identical, truncated);
    }

    private static CellSide? SideOf(CellData cell) =>
        cell.V is null && cell.F is null ? null : new CellSide { V = cell.V, F = cell.F };
}
