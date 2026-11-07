using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;

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
        IReadOnlyList<CellsSheetDiff> Sheets, CellsCompareSummary Summary, bool Identical, bool Truncated, IReadOnlyList<Warning> Warnings);

    public static Result Compare(
        ResourceBudgetLedger budgets, Workbook left, Workbook right, bool includeFormulas, int maxDiffs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDiffs);
        var sheets = new List<CellsSheetDiff>();
        int added = 0;
        int removed = 0;
        int renamed = 0;
        int modified = 0;
        var listing = new CellListing(budgets, maxDiffs);
        var warnings = new List<Warning>();
        Dictionary<Worksheet, Worksheet> renames = Renames(left, right);

        foreach (Worksheet sheet in left.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("compare");
            if (right.Worksheets[sheet.Name] is null && !renames.ContainsValue(sheet))
            {
                removed++;
                sheets.Add(new CellsSheetDiff { Name = sheet.Name, Status = CellsSheetChangeStatuses.Removed });
            }
        }

        foreach (Worksheet rightSheet in right.Worksheets)
        {
            budgets.Deadline.ThrowIfExpired("compare");
            Worksheet? leftSheet = left.Worksheets[rightSheet.Name] ?? renames.GetValueOrDefault(rightSheet);
            if (leftSheet is null)
            {
                added++;
                sheets.Add(new CellsSheetDiff { Name = rightSheet.Name, Status = CellsSheetChangeStatuses.Added });
                continue;
            }

            var shifts = new RowShifts();
            int before = listing.Differing;
            List<CellsCellDiff> cells = listing.Cells(leftSheet, rightSheet,
                includeFormulas ? FormulaScope.Compared : FormulaScope.None, shifts);
            if (listing.Differing != before && shifts.Describe(rightSheet.Name, budgets.Deadline) is { } shifted)
            {
                warnings.Add(shifted);
            }

            if (renames.ContainsKey(rightSheet))
            {
                renamed++;
                sheets.Add(new CellsSheetDiff { Name = rightSheet.Name, Status = CellsSheetChangeStatuses.Renamed, From = leftSheet.Name, Cells = cells });
            }
            else if (listing.Differing != before)
            {
                modified++;
                sheets.Add(new CellsSheetDiff { Name = rightSheet.Name, Status = CellsSheetChangeStatuses.Modified, Cells = cells });
            }
        }

        var summary = new CellsCompareSummary
        {
            SheetsAdded = added,
            SheetsRemoved = removed,
            SheetsRenamed = renamed,
            SheetsModified = modified,
            CellsDiffering = listing.Differing,
        };
        return new Result(sheets, summary,
            added == 0 && removed == 0 && renamed == 0 && listing.Differing == 0,
            listing.Truncated,
            warnings);
    }

    /// <summary>
    /// The differences between <paramref name="left"/>, a workbook before an edit, and
    /// <paramref name="table"/>, a text table the edit wrote (<see cref="CellsEngineFormats.IsTextTable"/>),
    /// as far as the table can hold them. Its one sheet, named after its file, stands for
    /// <paramref name="written"/> and compares with the left sheet that became it
    /// (<see cref="TextTableSheet.Before"/>), as a modified or renamed sheet, or is added when the
    /// edit added the sheet. The table holds no formulas, so only values are compared, and a
    /// left formula is listed with its side. The other left sheets are not compared: the table
    /// cannot hold them, and the save named them (<see cref="WorkbookSavePlan.DetectSheetLoss"/>).
    /// </summary>
    public static Result CompareTextTable(
        ResourceBudgetLedger budgets, Workbook left, Workbook table, TextTableSheet written, int maxDiffs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxDiffs);
        budgets.Deadline.ThrowIfExpired("compare");
        var listing = new CellListing(budgets, maxDiffs);
        Worksheet? leftSheet = written.Before(left);
        List<CellsCellDiff>? cells = leftSheet is null ? null : listing.Cells(leftSheet, table.Worksheets[0], FormulaScope.Left, shifts: null);
        CellsSheetDiff? sheet = leftSheet is null
            ? new CellsSheetDiff { Name = written.Name, Status = CellsSheetChangeStatuses.Added }
            : left.Worksheets[written.Name] is null
                ? new CellsSheetDiff { Name = written.Name, Status = CellsSheetChangeStatuses.Renamed, From = leftSheet.Name, Cells = cells }
                : listing.Differing > 0 ? new CellsSheetDiff { Name = written.Name, Status = CellsSheetChangeStatuses.Modified, Cells = cells } : null;
        var summary = new CellsCompareSummary
        {
            SheetsAdded = sheet?.Status == CellsSheetChangeStatuses.Added ? 1 : 0,
            SheetsRemoved = 0,
            SheetsRenamed = sheet?.Status == CellsSheetChangeStatuses.Renamed ? 1 : 0,
            SheetsModified = sheet?.Status == CellsSheetChangeStatuses.Modified ? 1 : 0,
            CellsDiffering = listing.Differing,
        };
        return new Result(sheet is null ? [] : [sheet], summary, Identical: sheet is null, listing.Truncated, []);
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

    /// <summary>Which formulas a comparison of two sheets reads.</summary>
    private enum FormulaScope
    {
        /// <summary>None: only values are compared.</summary>
        None,

        /// <summary>Both sides': values and formulas are compared, and each side lists its formula.</summary>
        Compared,

        /// <summary>The left side's only, as the right holds none: only values are compared, and the left lists its formula.</summary>
        Left,
    }

    /// <summary>The differing cells of the sheet pairs of one comparison, listed up to its cap.</summary>
    private sealed class CellListing(ResourceBudgetLedger budgets, int maxDiffs)
    {
        private int _listed;

        /// <summary>The differing cells so far, listed or not.</summary>
        internal int Differing { get; private set; }

        /// <summary>Whether more cells differ than are listed.</summary>
        internal bool Truncated => _listed < Differing;

        /// <summary>
        /// The differing cells of <paramref name="left"/> and <paramref name="right"/> while the
        /// cap allows, each counted in <see cref="Differing"/>; <paramref name="shifts"/>, when
        /// given, collects every cell pair for the row shifts.
        /// </summary>
        internal List<CellsCellDiff> Cells(Worksheet left, Worksheet right, FormulaScope formulas, RowShifts? shifts)
        {
            var cells = new List<CellsCellDiff>();
            foreach (long address in Addresses(budgets, left, right))
            {
                budgets.Deadline.ThrowIfExpired("compare");
                int row = (int)(address >> 14);
                int column = (int)(address & 0x3FFF);
                Cell? a = left.Cells.CheckCell(row, column);
                Cell? b = right.Cells.CheckCell(row, column);
                shifts?.Add(row, column, a, b);
                ComparisonValue leftValue = ComparisonValue.From(a);
                ComparisonValue rightValue = ComparisonValue.From(b);
                string? leftFormula = formulas != FormulaScope.None && a?.IsFormula == true ? a.Formula : null;
                string? rightFormula = formulas == FormulaScope.Compared && b?.IsFormula == true ? b.Formula : null;
                if (leftValue == rightValue
                    && (formulas == FormulaScope.Left || string.Equals(leftFormula, rightFormula, StringComparison.Ordinal)))
                {
                    continue;
                }

                Differing++;
                if (_listed < maxDiffs)
                {
                    _listed++;
                    cells.Add(new CellsCellDiff
                    {
                        Cell = A1.FormatCell(new CellRef(row, column)),
                        Left = leftValue.Side(a, leftFormula),
                        Right = rightValue.Side(b, rightFormula),
                    });
                }
            }

            return cells;
        }
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
