using Aspose.Cells;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Compares stored values over a bounded, sorted union of existing cell addresses.</summary>
internal static class DiffComparer
{
    /// <summary>The differences between two workbooks.</summary>
    /// <param name="Sheets">The differences of each sheet.</param>
    /// <param name="Summary">The counts over every sheet.</param>
    /// <param name="Identical">Whether the workbooks have the same sheets and no differing cells.</param>
    /// <param name="Truncated">Whether more cells differ than are listed.</param>
    /// <param name="Warnings">The row shifts of each modified sheet (ROWS_SHIFTED).</param>
    public sealed record Result(
        IReadOnlyList<SheetDiff> Sheets, DiffSummary Summary, bool Identical, bool Truncated, IReadOnlyList<Warning> Warnings);

    public static Result Compare(
        ResourceBudgetLedger budgets, Workbook left, Workbook right, bool includeFormulas, int maxDiffs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDiffs);
        var sheets = new List<SheetDiff>();
        int added = 0;
        int removed = 0;
        int renamed = 0;
        int modified = 0;
        int cellsDiffering = 0;
        int listed = 0;
        var warnings = new List<Warning>();
        Dictionary<Worksheet, Worksheet> renames = Renames(left, right);

        foreach (Worksheet sheet in left.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("compare");
            if (right.Worksheets[sheet.Name] is null && !renames.ContainsValue(sheet))
            {
                removed++;
                sheets.Add(new SheetDiff { Name = sheet.Name, Status = "removed" });
            }
        }

        foreach (Worksheet rightSheet in right.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("compare");
            Worksheet? leftSheet = left.Worksheets[rightSheet.Name] ?? renames.GetValueOrDefault(rightSheet);
            if (leftSheet is null)
            {
                added++;
                sheets.Add(new SheetDiff { Name = rightSheet.Name, Status = "added" });
                continue;
            }

            long[] addresses = Addresses(budgets, leftSheet, rightSheet);
            var cells = new List<CellDiff>();
            var shifts = new RowShifts();
            int before = cellsDiffering;
            foreach (long address in addresses)
            {
                budgets.Deadline.ThrowIfExpired("compare");
                int row = (int)(address >> 14);
                int column = (int)(address & 0x3FFF);
                Cell? a = leftSheet.Cells.CheckCell(row, column);
                Cell? b = rightSheet.Cells.CheckCell(row, column);
                shifts.Add(row, column, a, b);
                ComparisonValue leftValue = ComparisonValue.From(a);
                ComparisonValue rightValue = ComparisonValue.From(b);
                string? leftFormula = includeFormulas && a?.IsFormula == true ? a.Formula : null;
                string? rightFormula = includeFormulas && b?.IsFormula == true ? b.Formula : null;
                if (leftValue == rightValue
                    && string.Equals(leftFormula, rightFormula, StringComparison.Ordinal))
                {
                    continue;
                }

                cellsDiffering++;
                if (listed < maxDiffs)
                {
                    listed++;
                    cells.Add(new CellDiff
                    {
                        Cell = A1.FormatCell(new CellRef(row, column)),
                        Left = leftValue.Side(leftFormula),
                        Right = rightValue.Side(rightFormula),
                    });
                }
            }

            if (cellsDiffering != before && shifts.Describe(rightSheet.Name, budgets.Deadline) is { } shifted)
            {
                warnings.Add(shifted);
            }

            if (renames.ContainsKey(rightSheet))
            {
                renamed++;
                sheets.Add(new SheetDiff { Name = rightSheet.Name, Status = "renamed", From = leftSheet.Name, Cells = cells });
            }
            else if (cellsDiffering != before)
            {
                modified++;
                sheets.Add(new SheetDiff { Name = rightSheet.Name, Status = "modified", Cells = cells });
            }
        }

        var summary = new DiffSummary
        {
            SheetsAdded = added,
            SheetsRemoved = removed,
            SheetsRenamed = renamed,
            SheetsModified = modified,
            CellsDiffering = cellsDiffering,
        };
        return new Result(sheets, summary,
            added == 0 && removed == 0 && renamed == 0 && cellsDiffering == 0,
            listed < cellsDiffering,
            warnings);
    }

    /// <summary>
    /// Pairs each right sheet whose name the left lacks with the left sheet of the same internal
    /// id (TabId) whose name the right lacks: a rename keeps the id, so the pair is one sheet
    /// under a new name. Ids that several such sheets share pair nothing.
    /// </summary>
    private static Dictionary<Worksheet, Worksheet> Renames(Workbook left, Workbook right)
    {
        ILookup<int, Worksheet> leftOnly = left.Worksheets.Cast<Worksheet>()
            .Where(sheet => right.Worksheets[sheet.Name] is null).ToLookup(static sheet => sheet.TabId);
        ILookup<int, Worksheet> rightOnly = right.Worksheets.Cast<Worksheet>()
            .Where(sheet => left.Worksheets[sheet.Name] is null).ToLookup(static sheet => sheet.TabId);
        return rightOnly
            .Where(group => group.Count() == 1 && leftOnly[group.Key].Count() == 1)
            .ToDictionary(static group => group.Single(), group => leftOnly[group.Key].Single());
    }

    private static long[] Addresses(
        ResourceBudgetLedger budgets, Worksheet left, Worksheet right)
    {
        var addresses = new HashSet<long>();
        Collect(left);
        Collect(right);
        budgets.Deadline.ThrowIfExpired("compare-sort");
        long[] sorted = addresses.ToArray();
        Array.Sort(sorted);
        budgets.Deadline.ThrowIfExpired("compare-sort");
        return sorted;

        void Collect(Worksheet sheet)
        {
            // The SDK does not promise enumeration order. Count both inputs before
            // growing the per-sheet address union; the ledger spans the invocation.
            foreach (Cell cell in sheet.Cells)
            {
                budgets.Consume(CellsBudgetDomains.Cells, 1, "items", "compare-enumeration");
                addresses.Add(((long)cell.Row << 14) | (uint)cell.Column);
            }
        }
    }
}
