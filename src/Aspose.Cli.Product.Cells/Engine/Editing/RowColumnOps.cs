using Aspose.Cli.Sdk.Operations;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Row and column structure operations. Contract coordinates are A1-style
/// (1-based rows, letter columns); everything is converted to the engine's
/// zero-based indexes exactly once, here.
/// </summary>
internal static class RowColumnOps
{
    // Structural inserts/deletes must update references ACROSS the whole
    // workbook, not just the edited sheet. The default InsertRows/DeleteRows
    // overloads adjust only same-sheet formulas: a =SUM(Data!A1:A4) on another
    // sheet keeps its bounds after a row is inserted into Data's span, so a
    // same-sheet SUM expands, the cross-sheet one does not, and the summary a
    // human reads under-counts with no error (probed on 26.9.0). All four
    // structural operations therefore ask the engine to update references.
    private static readonly InsertOptions UpdateAll = new() { UpdateReference = true };

    public static void InsertRows(Worksheet sheet, InsertRowsOp op)
    {
        int last = sheet.Cells.MaxDataRow;
        OperationInvalidException.Require(last < op.At - 1 || last + op.Count < A1.MaxRows,
            $"inserting {op.Count} rows at row {op.At} would push the data in row {last + 1} past the sheet's last row, {A1.MaxRows}",
            "Delete or move the data at the bottom of the sheet first, or insert fewer rows.");
        sheet.Cells.InsertRows(op.At - 1, op.Count, UpdateAll);
    }

    public static void DeleteRows(Worksheet sheet, DeleteRowsOp op)
    {
        IReadOnlyList<(Validation Validation, CellArea[] Areas)> expected = ValidationsAfterDelete(sheet, op.At - 1, op.Count, rows: true);
        sheet.Cells.DeleteRows(op.At - 1, op.Count, updateReference: true);
        KeepValidationAreas(sheet, expected);
    }

    public static void InsertColumns(Worksheet sheet, InsertColumnsOp op)
    {
        int at = ColumnIndex(op.At);
        int last = sheet.Cells.MaxDataColumn;
        OperationInvalidException.Require(last < at || last + op.Count < A1.MaxColumns,
            $"inserting {op.Count} columns at column {op.At} would push the data in column {A1.ColumnName(last)} past the sheet's last column, {A1.ColumnName(A1.MaxColumns - 1)}",
            "Delete or move the data at the right of the sheet first, or insert fewer columns.");
        sheet.Cells.InsertColumns(at, op.Count, UpdateAll);
    }

    public static void DeleteColumns(Worksheet sheet, DeleteColumnsOp op)
    {
        int at = ColumnIndex(op.At);
        IReadOnlyList<(Validation Validation, CellArea[] Areas)> expected = ValidationsAfterDelete(sheet, at, op.Count, rows: false);
        sheet.Cells.DeleteColumns(at, op.Count, updateReference: true);
        KeepValidationAreas(sheet, expected);
    }

    /// <summary>
    /// The areas each validation of the sheet covers once lines <paramref name="first"/> to
    /// <paramref name="first"/> + <paramref name="count"/> - 1 are deleted: an area below or right
    /// of them moves up or left, one they cut shrinks, and one inside them goes. The engine leaves
    /// an area whose last lines are deleted one line too long (known issue
    /// CELLS-VALIDATION-DELETE-END, KNOWN-ISSUES.md), so the delete sets these areas after it.
    /// </summary>
    private static List<(Validation Validation, CellArea[] Areas)> ValidationsAfterDelete(Worksheet sheet, int first, int count, bool rows)
    {
        int last = first + count - 1;
        var expected = new List<(Validation, CellArea[])>();
        foreach (Validation validation in sheet.Validations)
        {
            expected.Add((validation, [.. (validation.Areas ?? []).Select(area => Shrunk(area, first, last, count, rows)).OfType<CellArea>()]));
        }

        return expected;
    }

    private static CellArea? Shrunk(CellArea area, int first, int last, int count, bool rows)
    {
        (int start, int end) = rows ? (area.StartRow, area.EndRow) : (area.StartColumn, area.EndColumn);
        if (end < first)
        {
            return area;
        }

        (int newStart, int newEnd) = start > last
            ? (start - count, end - count)
            : (Math.Min(start, first), end > last ? end - count : first - 1);
        if (newEnd < newStart)
        {
            return null;
        }

        return rows
            ? new CellArea { StartRow = newStart, EndRow = newEnd, StartColumn = area.StartColumn, EndColumn = area.EndColumn }
            : new CellArea { StartRow = area.StartRow, EndRow = area.EndRow, StartColumn = newStart, EndColumn = newEnd };
    }

    /// <summary>Sets each validation to the areas it should cover after a delete, and removes one left with none.</summary>
    private static void KeepValidationAreas(Worksheet sheet, IReadOnlyList<(Validation Validation, CellArea[] Areas)> expected)
    {
        foreach ((Validation validation, CellArea[] areas) in expected)
        {
            CellArea[] actual = validation.Areas ?? [];
            if (actual.SequenceEqual(areas, AreaComparer.Instance))
            {
                continue;
            }

            foreach (CellArea area in actual)
            {
                validation.RemoveArea(area);
            }

            foreach (CellArea area in areas)
            {
                validation.AddArea(area, false, false);
            }
        }

        for (int index = sheet.Validations.Count - 1; index >= 0; index--)
        {
            if (sheet.Validations[index].Areas is not { Length: > 0 })
            {
                sheet.Validations.RemoveAt(index);
            }
        }
    }

    private sealed class AreaComparer : IEqualityComparer<CellArea>
    {
        internal static readonly AreaComparer Instance = new();

        public bool Equals(CellArea x, CellArea y) =>
            x.StartRow == y.StartRow && x.EndRow == y.EndRow && x.StartColumn == y.StartColumn && x.EndColumn == y.EndColumn;

        public int GetHashCode(CellArea area) => HashCode.Combine(area.StartRow, area.EndRow, area.StartColumn, area.EndColumn);
    }

    public static void ResizeRows(Worksheet sheet, ResizeRowsOp op)
    {
        int first = op.First - 1;
        int last = (op.Last ?? op.First) - 1;

        if (op.Height is { } height)
        {
            for (int row = first; row <= last; row++)
            {
                sheet.Cells.SetRowHeight(row, height);
            }
        }
        else
        {
            sheet.AutoFitRows(first, last);
        }
    }

    public static void ResizeColumns(Worksheet sheet, ResizeColumnsOp op)
    {
        int first = ColumnIndex(op.First);
        int last = op.Last is { } to ? ColumnIndex(to) : first;

        if (op.WidthChars is { } width)
        {
            for (int column = first; column <= last; column++)
            {
                sheet.Cells.SetColumnWidth(column, width);
            }
        }
        else
        {
            sheet.AutoFitColumns(first, last);
        }
    }

    /// <summary>Column letters to zero-based index (validated by the parser).</summary>
    private static int ColumnIndex(string letters) => A1.ParseColumn(letters);
}
