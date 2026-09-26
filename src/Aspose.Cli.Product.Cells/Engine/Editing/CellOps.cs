using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using CellsRange = Aspose.Cells.Range;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// The value- and range-level ops: writing values and formulas, clearing,
/// copying, formatting and merging. Each returns the count of cells it touched
/// where that is meaningful.
/// </summary>
internal static class CellOps
{
    public static long SetValues(Worksheet sheet, SetValuesOp op)
    {
        CellRef anchor = Range(op.Range).Start;
        for (int row = 0; row < op.Values.Count; row++)
        {
            IReadOnlyList<object?> line = op.Values[row];
            for (int column = 0; column < line.Count; column++)
            {
                ValueWriter.Write(sheet.Cells[anchor.Row + row, anchor.Column + column], line[column]);
            }
        }

        return (long)op.Values.Count * op.Values[0].Count;
    }

    public static long SetFormula(Worksheet sheet, SetFormulaOp op)
    {
        RangeRef range = Range(op.Range);
        Cell anchor = sheet.Cells[range.Start.Row, range.Start.Column];
        anchor.Formula = op.Formula;

        if (range.CellCount > 1)
        {
            // Excel fill semantics: shift relative references per cell by
            // sharing the anchor's R1C1 form.
            string shared = anchor.R1C1Formula;
            for (int row = range.Start.Row; row <= range.End.Row; row++)
            {
                for (int column = range.Start.Column; column <= range.End.Column; column++)
                {
                    if (row != range.Start.Row || column != range.Start.Column)
                    {
                        sheet.Cells[row, column].R1C1Formula = shared;
                    }
                }
            }
        }

        return range.CellCount;
    }

    public static long Clear(Worksheet sheet, ClearRangeOp op)
    {
        RangeRef range = Range(op.Range);
        CellArea area = CellArea.CreateCellArea(
            range.Start.Row, range.Start.Column, range.End.Row, range.End.Column);

        switch (op.What)
        {
            case ClearTargets.Formats:
                sheet.Cells.ClearFormats(area);
                break;
            case ClearTargets.Everything:
                sheet.Cells.ClearRange(area);
                break;
            default:
                sheet.Cells.ClearContents(area);
                break;
        }

        return range.CellCount;
    }

    public static long Copy(Worksheet sheet, CopyRangeOp op)
    {
        (Worksheet fromSheet, RangeRef from) = Sheets.ResolveRange(sheet, op.From);
        (Worksheet toSheet, RangeRef to) = Sheets.ResolveRange(sheet, op.To);
        CellRef anchor = to.Start;

        CellsRange source = fromSheet.Cells.CreateRange(
            from.Start.Row, from.Start.Column, from.RowCount, from.ColumnCount);
        CellsRange destination = toSheet.Cells.CreateRange(
            anchor.Row, anchor.Column, from.RowCount, from.ColumnCount);
        destination.Copy(source);

        return from.CellCount;
    }

    public static long Format(Workbook workbook, Worksheet sheet, FormatRangeOp op)
    {
        RangeRef range = Range(op.Range);

        // A pivot table owns the style of the cells it occupies, and a flagged
        // Range.ApplyStyle over them merges the requested fields onto the
        // PIVOT's style rather than onto what the cell currently shows: styling
        // a pivot header, then setting one unrelated field, silently drops the
        // fill and resets the font colour (verified against 26.9.0). Merging
        // cell by cell keeps the documented promise — only the fields the
        // caller set change. It costs a
        // GetStyle/SetStyle per cell, so it stays scoped to pivot ranges, which
        // are bounded by the pivot itself.
        if (IntersectsPivot(sheet, range))
        {
            for (int row = range.Start.Row; row <= range.End.Row; row++)
            {
                for (int column = range.Start.Column; column <= range.End.Column; column++)
                {
                    Cell cell = sheet.Cells[row, column];
                    Style current = cell.GetStyle();
                    StyleWriter.Apply(current, op.Style);
                    cell.SetStyle(current);
                }
            }

            return range.CellCount;
        }

        (Style style, StyleFlag flag) = StyleWriter.Build(workbook, op.Style);
        CellsRange target = sheet.Cells.CreateRange(
            range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount);
        target.ApplyStyle(style, flag);

        return range.CellCount;
    }

    private static bool IntersectsPivot(Worksheet sheet, RangeRef range)
    {
        foreach (Aspose.Cells.Pivot.PivotTable pivot in sheet.PivotTables)
        {
            CellArea area = pivot.TableRange1;
            if (range.Start.Row <= area.EndRow && range.End.Row >= area.StartRow
                && range.Start.Column <= area.EndColumn && range.End.Column >= area.StartColumn)
            {
                return true;
            }
        }

        return false;
    }

    public static long Merge(Worksheet sheet, string rangeText, bool merged)
    {
        RangeRef range = Range(rangeText);
        if (merged)
        {
            sheet.Cells.Merge(range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount);
        }
        else
        {
            sheet.Cells.UnMerge(range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount);
        }

        return range.CellCount;
    }

    private static RangeRef Range(string text) => A1.ParseRange(text).Range;
}
