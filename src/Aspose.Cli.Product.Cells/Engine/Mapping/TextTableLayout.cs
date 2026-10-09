using System.Globalization;
using Aspose.Cells;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>The shape of one imported text row: how many cells hold a value, and the first text value.</summary>
/// <param name="Row">The zero-based row index.</param>
/// <param name="Filled">The number of cells that hold a value other than blank text.</param>
/// <param name="FirstText">The trimmed text of the leftmost cell that holds text, or null.</param>
/// <param name="NumberColumn">The zero-based column of the leftmost number, or null.</param>
internal readonly record struct TextRowShape(int Row, int Filled, string? FirstText, int? NumberColumn = null);

internal enum TextTableFindingKind
{
    /// <summary>The header is not the first row; the rows before it are a title, notes or empty.</summary>
    Preamble,

    /// <summary>Empty rows lie between the header and the last row of the table.</summary>
    BlankRows,

    /// <summary>One of the last rows of the table is labeled as a total.</summary>
    TotalRows,

    /// <summary>The last row is the notice an evaluation CSV or TSV export wrote after the data.</summary>
    EvaluationNotice,
}

/// <summary>One layout finding; rows and columns are zero-based.</summary>
/// <param name="Kind">What the finding reports.</param>
/// <param name="HeaderRow">The header row of the table.</param>
/// <param name="LastRow">The last row of the table.</param>
/// <param name="Rows">The rows the finding concerns.</param>
/// <param name="Labels">The distinct total labels of those rows; empty for other findings.</param>
/// <param name="NumberColumn">For total rows, the column the first one holds a number in, or null.</param>
internal sealed record TextTableFinding(
    TextTableFindingKind Kind,
    int HeaderRow,
    int LastRow,
    IReadOnlyList<int> Rows,
    IReadOnlyList<string> Labels,
    int? NumberColumn = null);

/// <summary>
/// Warns when a delimited text input is not a plain table that starts at row 1: a title or
/// query-condition rows before the header, empty rows inside the table, or a total row at its
/// end, as ERP and report exports write them, and the evaluation notice row an unlicensed export
/// writes last. The import itself stays unchanged; the warnings
/// only tell the caller which rows are data. The detector is a pure function over row shapes,
/// kept apart from the workbook access that reads them.
/// </summary>
internal static class TextTableLayout
{
    /// <summary>The rows read from the top of the sheet; the remainder is checked only for a total row.</summary>
    internal const int ScanRows = 10_000;

    /// <summary>The trailing rows read when the sheet is longer than <see cref="ScanRows"/>.</summary>
    internal const int TailRows = 3;

    /// <summary>A preamble longer than this is not reported: the table is then not recognizable.</summary>
    private const int MaxPreambleRows = 10;

    private const int ListedRows = 5;

    // Compared after removing whitespace and a trailing colon, ignoring case.
    private static readonly HashSet<string> TotalLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "合计", "合計", "总计", "總計", "小计", "小計", "总和", "總和",
        "total", "totals", "grandtotal", "subtotal", "sub-total", "sum",
    };

    /// <summary>
    /// The layout warnings for a loaded delimited text input, or none for any other input.
    /// </summary>
    internal static IReadOnlyList<Warning> Warnings(LoadedWorkbook loaded, ResourceBudgetLedger budgets)
    {
        if (!loaded.IsDelimitedText || loaded.Workbook.Worksheets.Count == 0)
        {
            return [];
        }

        Worksheet sheet = loaded.Workbook.Worksheets[0];
        if (Sheets.UsedRange(sheet) is not { } used)
        {
            return [];
        }

        (IReadOnlyList<TextRowShape> head, IReadOnlyList<TextRowShape> tail) = ReadShapes(sheet, used, budgets);
        return Describe(Detect(head, tail), sheet.Name, used.End.Column, capped: tail.Count > 0);
    }

    /// <summary>
    /// Finds the layout problems in a table's rows. <paramref name="head"/> holds every row from
    /// row 0 on, one per row; <paramref name="tail"/> holds the last rows of a longer sheet, after
    /// a gap, and is empty when <paramref name="head"/> reaches the last row.
    /// </summary>
    internal static IReadOnlyList<TextTableFinding> Detect(IReadOnlyList<TextRowShape> head, IReadOnlyList<TextRowShape> tail)
    {
        TextRowShape[] filled = [.. head.Concat(tail).Where(static shape => shape.Filled > 0)];
        TextTableFinding? notice = null;
        if (filled is [.., { Filled: 1 } last] && CellsEvaluation.IsNotice(last.FirstText))
        {
            notice = new TextTableFinding(TextTableFindingKind.EvaluationNotice, 0, last.Row, [last.Row], []);
            filled = filled[..^1];
        }

        if (filled.Length == 0)
        {
            return notice is null ? [] : [notice];
        }

        int width = filled.Max(static shape => shape.Filled);
        // A header is more than half as wide as the widest row; with one column, any value is.
        // The rows above it are a preamble only when each holds at most one value, as a title or
        // a note does: an earlier row of several values is a header narrower than its data, as a
        // CSV with unnamed trailing columns has, and the table starts at the first such row.
        TextRowShape header = width < 2 ? filled[0] : filled.First(shape => shape.Filled * 2 > width);
        if (filled.FirstOrDefault(shape => shape.Row < header.Row && shape.Filled > 1) is { Filled: > 1 } narrower)
        {
            header = narrower;
        }

        header = SkipPreambleBlocks(head, filled, header, width);
        int lastRow = filled[^1].Row;
        var findings = new List<TextTableFinding>();
        if (header.Row > 0 && header.Row <= MaxPreambleRows && lastRow > header.Row)
        {
            findings.Add(new TextTableFinding(TextTableFindingKind.Preamble, header.Row, lastRow,
                [.. Enumerable.Range(0, header.Row)], []));
        }

        int[] blanks = [.. head.Where(shape => shape.Filled == 0 && shape.Row > header.Row && shape.Row < lastRow)
            .Select(static shape => shape.Row)];
        if (blanks.Length > 0)
        {
            findings.Add(new TextTableFinding(TextTableFindingKind.BlankRows, header.Row, lastRow, blanks, []));
        }

        TextRowShape[] totals = [.. filled.Where(shape => shape.Row > header.Row).TakeLast(3)
            .Where(static shape => IsTotalLabel(shape.FirstText))];
        if (totals.Length > 0)
        {
            findings.Add(new TextTableFinding(TextTableFindingKind.TotalRows, header.Row, lastRow,
                [.. totals.Select(static shape => shape.Row)],
                [.. totals.Select(static shape => shape.FirstText!).Distinct(StringComparer.Ordinal)],
                totals[0].NumberColumn));
        }

        if (notice is not null)
        {
            findings.Add(notice);
        }

        return findings;
    }

    /// <summary>
    /// Moves the header past the blocks of notes an export writes before its table, such as an
    /// "export time, query" row of several values: a block that ends in an empty row is notes when
    /// the first row after the empty rows is wider than every row of the block, wide enough for a
    /// header, and holds no number, as a header does. A block of data rows split by an empty row
    /// fails the test, because its rows are as wide as the rows after it or hold numbers.
    /// </summary>
    private static TextRowShape SkipPreambleBlocks(
        IReadOnlyList<TextRowShape> head, TextRowShape[] filled, TextRowShape header, int width)
    {
        while (header.Row <= MaxPreambleRows)
        {
            int blockEnd = header.Row;
            while (blockEnd + 1 < head.Count && head[blockEnd + 1].Filled > 0)
            {
                blockEnd++;
            }

            int blockWidth = filled.Where(shape => shape.Row >= header.Row && shape.Row <= blockEnd).Max(static shape => shape.Filled);
            if (blockEnd + 1 >= head.Count
                || filled.FirstOrDefault(shape => shape.Row > blockEnd) is not { Filled: > 0 } next
                || next.Row > MaxPreambleRows
                || next.NumberColumn is not null
                || next.Filled <= blockWidth
                || next.Filled * 2 <= width)
            {
                return header;
            }

            header = next;
        }

        return header;
    }

    internal static bool IsTotalLabel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string compact = string.Concat(text.Where(static character => !char.IsWhiteSpace(character)))
            .TrimEnd(':', '：');
        return TotalLabels.Contains(compact);
    }

    /// <summary>
    /// Words the findings on sheet <paramref name="sheetName"/> as warnings; rows are reported
    /// one-based, as A1 row references.
    /// </summary>
    internal static IReadOnlyList<Warning> Describe(IReadOnlyList<TextTableFinding> findings, string sheetName, int lastColumn, bool capped)
    {
        string last = A1.ColumnName(lastColumn);
        return [.. findings.Select(finding => finding.Kind switch
        {
            TextTableFindingKind.Preamble => Preamble(finding, sheetName, last),
            TextTableFindingKind.BlankRows => BlankRows(finding, last, capped),
            TextTableFindingKind.TotalRows => TotalRows(finding),
            _ => EvaluationNotice(finding),
        })];
    }

    private static Warning Preamble(TextTableFinding finding, string sheetName, string lastColumn)
    {
        int header = finding.HeaderRow + 1;
        string rows = finding.Rows.Count == 1 ? "row 1 looks" : $"rows 1-{finding.Rows.Count} look";
        return Build(
            $"The table header is probably row {header}; {rows} like a title, notes or empty lines, not data.",
            $"Read the header with 'cells query range <file> --sheet \"{sheetName}\" --range A{header}:{lastColumn}{header}' and start data ranges, formulas and sorts at row {header + 1}, not row 2.",
            RowReference(1, finding.Rows.Count));
    }

    private static Warning BlankRows(TextTableFinding finding, string lastColumn, bool capped)
    {
        string listed = string.Join(", ", finding.Rows.Take(ListedRows).Select(static row => (row + 1).ToString(CultureInfo.InvariantCulture)))
            + (finding.Rows.Count > ListedRows ? ", ..." : string.Empty);
        string subject = finding.Rows.Count == 1 ? $"Row {listed} is" : $"{finding.Rows.Count} rows ({listed}) are";
        string scope = capped ? $" (only the first {ScanRows} rows were checked)" : string.Empty;
        return Build(
            $"{subject} empty inside the table{scope}; sorting, filtering and anything that stops at the first empty row sees separate blocks.",
            $"Address the whole table explicitly (A{finding.HeaderRow + 1}:{lastColumn}{finding.LastRow + 1}), or remove the empty rows with the delete_rows operation of 'cells edit' after converting to xlsx; row numbers below them then move up.",
            string.Join(",", finding.Rows.Take(ListedRows).Select(static row => RowReference(row + 1, row + 1))));
    }

    private static Warning TotalRows(TextTableFinding finding)
    {
        int first = finding.Rows[0] + 1;
        string labels = string.Join("', '", finding.Labels);
        string subject = finding.Rows.Count == 1
            ? $"Row {first} is a total row ('{labels}')"
            : $"Rows {string.Join(", ", finding.Rows.Select(static row => (row + 1).ToString(CultureInfo.InvariantCulture)))} are total rows ('{labels}')";
        int dataStart = finding.HeaderRow + 2;
        // The example sums a column the total row holds a number in, so it is never a text column.
        string example = finding.NumberColumn is int column
            ? $", for example =SUM({A1.ColumnName(column)}{dataStart}:{A1.ColumnName(column)}{first - 1}) for column {A1.ColumnName(column)}"
            : string.Empty;
        return Build(
            $"{subject}; a sum or formula over a column that includes it counts the data twice, and sorts, pivots and charts take it for data.",
            first > dataStart
                ? $"End data ranges at row {first - 1}{example}, and leave row {first} out of sorts, pivots and charts."
                : $"Leave row {first} out of formulas, sorts, pivots and charts.",
            string.Join(",", finding.Rows.Select(static row => RowReference(row + 1, row + 1))));
    }

    private static Warning EvaluationNotice(TextTableFinding finding)
    {
        int row = finding.LastRow + 1;
        return Build(
            $"Row {row} is not data: it is the evaluation notice an Aspose.Cells export without a license wrote after the data.",
            $"Tell the user, and leave row {row} out of every range, or delete it with the delete_rows operation of 'cells edit' after converting to xlsx.",
            RowReference(row, row));
    }

    private static Warning Build(string message, string hint, string location) => new(CellsDiagnostics.TextTableLayout, message)
    {
        Hint = hint,
        Docs = "cells/troubleshooting",
        Location = location,
    };

    private static string RowReference(int first, int last) =>
        string.Create(CultureInfo.InvariantCulture, $"{first}:{last}");

    private static (IReadOnlyList<TextRowShape> Head, IReadOnlyList<TextRowShape> Tail) ReadShapes(
        Worksheet sheet, RangeRef used, ResourceBudgetLedger budgets)
    {
        int lastRow = used.End.Row;
        int headEnd = Math.Min(lastRow, ScanRows - 1);
        var head = new List<TextRowShape>(headEnd + 1);
        for (int row = 0; row <= headEnd; row++)
        {
            if (row % 1024 == 0)
            {
                budgets.Deadline.ThrowIfExpired("text-table-layout");
            }

            head.Add(Shape(sheet.Cells, row));
        }

        var tail = new List<TextRowShape>();
        for (int row = Math.Max(headEnd + 1, lastRow - TailRows + 1); row <= lastRow; row++)
        {
            tail.Add(Shape(sheet.Cells, row));
        }

        return (head, tail);
    }

    private static TextRowShape Shape(Aspose.Cells.Cells cells, int rowIndex)
    {
        if (cells.CheckRow(rowIndex) is not { } row)
        {
            return new TextRowShape(rowIndex, 0, null);
        }

        int filled = 0;
        int textColumn = int.MaxValue;
        string? text = null;
        int? numberColumn = null;
        foreach (Cell cell in row)
        {
            if (cell.Type == CellValueType.IsNull)
            {
                continue;
            }

            if (cell.Type == CellValueType.IsNumeric && cell.Column < (numberColumn ?? int.MaxValue))
            {
                numberColumn = cell.Column;
            }

            if (cell.Type == CellValueType.IsString)
            {
                string value = cell.StringValue.Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (cell.Column < textColumn)
                {
                    (textColumn, text) = (cell.Column, value);
                }
            }

            filled++;
        }

        return new TextRowShape(rowIndex, filled, text, numberColumn);
    }
}
