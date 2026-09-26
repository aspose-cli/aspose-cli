using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Compares stored values over a bounded, sorted union of existing cell addresses.</summary>
internal static class DiffComparer
{
    public sealed record Result(
        IReadOnlyList<SheetDiff> Sheets, DiffSummary Summary, bool Identical, bool Truncated);

    public static Result Compare(
        ResourceBudgetLedger budgets, Workbook left, Workbook right, bool includeFormulas, int maxDiffs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDiffs);
        var sheets = new List<SheetDiff>();
        int added = 0;
        int removed = 0;
        int modified = 0;
        int cellsDiffering = 0;
        int listed = 0;

        foreach (Worksheet sheet in left.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("compare");
            if (right.Worksheets[sheet.Name] is null)
            {
                removed++;
                sheets.Add(new SheetDiff { Name = sheet.Name, Status = "removed" });
            }
        }

        foreach (Worksheet rightSheet in right.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("compare");
            Worksheet? leftSheet = left.Worksheets[rightSheet.Name];
            if (leftSheet is null)
            {
                added++;
                sheets.Add(new SheetDiff { Name = rightSheet.Name, Status = "added" });
                continue;
            }

            long[] addresses = Addresses(budgets, leftSheet, rightSheet);
            var cells = new List<CellDiff>();
            int before = cellsDiffering;
            foreach (long address in addresses)
            {
                budgets.Deadline.ThrowIfExpired("compare");
                int row = (int)(address >> 14);
                int column = (int)(address & 0x3FFF);
                Cell? a = leftSheet.Cells.CheckCell(row, column);
                Cell? b = rightSheet.Cells.CheckCell(row, column);
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

            if (cellsDiffering != before)
            {
                modified++;
                sheets.Add(new SheetDiff { Name = rightSheet.Name, Status = "modified", Cells = cells });
            }
        }

        var summary = new DiffSummary
        {
            SheetsAdded = added,
            SheetsRemoved = removed,
            SheetsModified = modified,
            CellsDiffering = cellsDiffering,
        };
        return new Result(sheets, summary,
            added == 0 && removed == 0 && cellsDiffering == 0,
            listed < cellsDiffering);
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
