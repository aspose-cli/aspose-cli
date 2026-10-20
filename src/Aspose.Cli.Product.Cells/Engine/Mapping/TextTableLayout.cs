using System.Globalization;
using Aspose.Cells;
using Aspose.Cli.Sdk.IO.Budgets;

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
/// <param name="DataEnd">For total rows, the last data row before them.</param>
internal sealed record TextTableFinding(
    TextTableFindingKind Kind,
    int HeaderRow,
    int LastRow,
    IReadOnlyList<int> Rows,
    IReadOnlyList<string> Labels,
    int? NumberColumn = null,
    int? DataEnd = null);

/// <summary>
/// Warns when a delimited text input is not a plain table that starts at row 1: a title or
/// query-condition rows before the header, empty rows inside the table, or a total row at its
/// end, as ERP and report exports write them, and the evaluation notice row an unlicensed export
/// writes last. The import itself stays unchanged; the warnings
/// only tell the caller which rows are data. A JSON export, which takes each sheet's first used
/// row for its keys, is checked the same way for every sheet it writes. The detector is a pure
/// function over row shapes, kept apart from the workbook access that reads them.
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

    // The words of a total label, ignoring case; "grand" only qualifies another of them.
    private static readonly HashSet<string> TotalWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "合计", "合計", "总计", "總計", "小计", "小計", "总和", "總和",
        "total", "totals", "grandtotal", "subtotal", "sub-total", "sum",
    };

    private const string Grand = "grand";

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
    /// The layout warnings for the sheets a JSON export writes. The export takes each sheet's
    /// first used row for the keys and writes every row below it as a record, so a title above
    /// the header, an empty row inside the table and a total row each end up in the records.
    /// Empty rows above the first used row are not reported: the export skips them.
    /// </summary>
    internal static IReadOnlyList<Warning> JsonWarnings(IEnumerable<Worksheet> sheets, ResourceBudgetLedger budgets)
    {
        var warnings = new List<Warning>();
        foreach (Worksheet sheet in sheets)
        {
            if (Sheets.UsedRange(sheet) is not { } used)
            {
                continue;
            }

            (IReadOnlyList<TextRowShape> head, IReadOnlyList<TextRowShape> tail) = ReadShapes(sheet, used, budgets);
            int firstUsed = head.FirstOrDefault(static shape => shape.Filled > 0) is { Filled: > 0 } first ? first.Row : 0;
            warnings.AddRange(Detect(head, tail)
                .Where(finding => finding.Kind != TextTableFindingKind.Preamble || finding.HeaderRow > firstUsed)
                .Select(finding => JsonWarning(finding, sheet.Name, firstUsed, capped: tail.Count > 0)));
        }

        return warnings;
    }

    private static Warning JsonWarning(TextTableFinding finding, string sheetName, int firstUsed, bool capped)
    {
        string sheet = $"sheet '{sheetName}'";
        string listed = string.Join(", ", finding.Rows.Take(ListedRows).Select(static row => (row + 1).ToString(CultureInfo.InvariantCulture)))
            + (finding.Rows.Count > ListedRows ? ", ..." : string.Empty);
        (string message, string hint) = finding.Kind switch
        {
            TextTableFindingKind.Preamble => (
                $"The JSON of {sheet} takes its keys from row {firstUsed + 1}, but the table header is probably row {finding.HeaderRow + 1}; "
                    + (finding.HeaderRow - firstUsed == 1 ? $"row {firstUsed + 1} looks" : $"rows {firstUsed + 1}-{finding.HeaderRow} look")
                    + " like a title or notes, so the header and the rows below it are written as records under the wrong keys.",
                RemoveRows(sheetName, "the rows above the header", $", \"at\": {firstUsed + 1}, \"count\": {finding.HeaderRow - firstUsed}")),
            TextTableFindingKind.BlankRows => (
                (finding.Rows.Count == 1 ? $"Row {listed} of {sheet} is" : $"{finding.Rows.Count} rows of {sheet} ({listed}) are")
                    + " empty inside the table" + (capped ? $" (only the first {ScanRows} rows were checked)" : string.Empty)
                    + $"; the JSON writes {(finding.Rows.Count == 1 ? "it" : "each")} as a null record.",
                "Skip null records downstream, or " + LowerFirst(RemoveRows(sheetName, "the empty rows", " and the \"at\" of each row, one operation per row, the bottom row first,"))),
            TextTableFindingKind.TotalRows => (
                (finding.Rows.Count == 1 ? $"Row {finding.Rows[0] + 1} of {sheet} is a total row" : $"Rows {listed} of {sheet} are total rows")
                    + $" ('{string.Join("', '", finding.Labels)}'); the JSON writes {(finding.Rows.Count == 1 ? "it as a record" : "them as records")}, "
                    + "so a sum over the records counts the data twice.",
                "Leave the last records out downstream, or " + LowerFirst(RemoveRows(sheetName, "them",
                    $", \"at\": {finding.Rows[0] + 1}, \"count\": {finding.LastRow - finding.Rows[0] + 1}"))),
            _ => (
                $"Row {finding.LastRow + 1} of {sheet} is not data: it is the evaluation notice an Aspose.Cells export without a license "
                    + "wrote after the data, and the JSON writes it as a record.",
                "Tell the user, and leave the last record out, or " + LowerFirst(RemoveRows(sheetName, "it", $", \"at\": {finding.LastRow + 1}"))),
        };
        string rows = finding.Kind == TextTableFindingKind.Preamble
            ? RowReference(firstUsed + 1, finding.HeaderRow)
            : string.Join(",", finding.Rows.Take(ListedRows).Select(static row => RowReference(row + 1, row + 1)));
        return Build(message, hint, sheetName, rows);
    }

    private static string RemoveRows(string sheetName, string what, string rows) =>
        $"Convert a copy without {what}: delete_rows with \"sheet\": \"{sheetName}\"{rows} in 'cells edit' with --out, then convert the copy.";

    private static string LowerFirst(string text) => char.ToLowerInvariant(text[0]) + text[1..];

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

        TextRowShape[] totals = [.. filled.Where(shape => shape.Row > header.Row).TakeLast(3)
            .Where(static shape => IsTotalLabel(shape.FirstText))];

        // The data ends before the first total row: an empty row there is not inside the data,
        // and the whole table to address ends where the total rows' finding says the data does.
        int dataEnd = totals.Length > 0
            ? filled.LastOrDefault(shape => shape.Row > header.Row && shape.Row < totals[0].Row) is { Filled: > 0 } lastData ? lastData.Row : header.Row
            : lastRow;
        int[] blanks = [.. head.Where(shape => shape.Filled == 0 && shape.Row > header.Row && shape.Row < dataEnd)
            .Select(static shape => shape.Row)];
        if (blanks.Length > 0)
        {
            findings.Add(new TextTableFinding(TextTableFindingKind.BlankRows, header.Row, dataEnd, blanks, []));
        }

        if (totals.Length > 0)
        {
            findings.Add(new TextTableFinding(TextTableFindingKind.TotalRows, header.Row, lastRow,
                [.. totals.Select(static shape => shape.Row)],
                [.. totals.Select(static shape => shape.FirstText!).Distinct(StringComparer.Ordinal)],
                totals[0].NumberColumn,
                dataEnd));
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
    /// it holds a title, a row of one value, or a row that starts with a "label:" note, and the
    /// first row after the empty rows is wider than every row of the block, wide enough for a
    /// header, and holds no number, as a header does. A block of data rows split by an empty row
    /// fails the test, because its rows are as wide as the rows after it or hold numbers, and so
    /// does a header narrower than its data rows, which holds labels only.
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

            TextRowShape[] block = [.. filled.Where(shape => shape.Row >= header.Row && shape.Row <= blockEnd)];
            int blockWidth = block.Max(static shape => shape.Filled);
            if (blockEnd + 1 >= head.Count
                || !block.Any(static shape => shape.Filled == 1 || shape.FirstText?.IndexOfAny([':', '：']) >= 0)
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

        string[] words = Words(text);
        return words.Any(static word => TotalWords.Contains(word))
            && words.All(static word => TotalWords.Contains(word) || string.Equals(word, Grand, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The words of a label: runs of letters and digits, split at white space and punctuation
    /// other than a hyphen, and where Chinese characters meet Latin ones, so that a bilingual
    /// label such as "合计 Total" or "合计Total" is two words, while "Totalizer" stays one.
    /// </summary>
    private static string[] Words(string text)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        bool? cjk = null;
        foreach (char character in text)
        {
            bool part = char.IsLetterOrDigit(character) || character == '-';
            bool isCjk = character is >= '⺀' and <= '鿿';
            if (!part || (word.Length > 0 && cjk != isCjk))
            {
                Flush();
            }

            if (part)
            {
                word.Append(character);
                cjk = isCjk;
            }
        }

        Flush();
        return [.. words];

        void Flush()
        {
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }
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
            TextTableFindingKind.BlankRows => BlankRows(finding, sheetName, last, capped),
            TextTableFindingKind.TotalRows => TotalRows(finding, sheetName),
            _ => EvaluationNotice(finding, sheetName),
        })];
    }

    private static Warning Preamble(TextTableFinding finding, string sheetName, string lastColumn)
    {
        int header = finding.HeaderRow + 1;
        string rows = finding.Rows.Count == 1 ? "row 1 looks" : $"rows 1-{finding.Rows.Count} look";
        return Build(
            $"The table header is probably row {header}; {rows} like a title, notes or empty lines, not data.",
            $"Read the header with 'cells read <file> --sheet \"{sheetName}\" --range A{header}:{lastColumn}{header}' and start data ranges, formulas and sorts at row {header + 1}, not row 2.",
            sheetName, RowReference(1, finding.Rows.Count));
    }

    private static Warning BlankRows(TextTableFinding finding, string sheetName, string lastColumn, bool capped)
    {
        string listed = string.Join(", ", finding.Rows.Take(ListedRows).Select(static row => (row + 1).ToString(CultureInfo.InvariantCulture)))
            + (finding.Rows.Count > ListedRows ? ", ..." : string.Empty);
        string subject = finding.Rows.Count == 1 ? $"Row {listed} is" : $"{finding.Rows.Count} rows ({listed}) are";
        string scope = capped ? $" (only the first {ScanRows} rows were checked)" : string.Empty;
        return Build(
            $"{subject} empty inside the table{scope}; sorting, filtering and anything that stops at the first empty row sees separate blocks.",
            $"Address the whole table explicitly (A{finding.HeaderRow + 1}:{lastColumn}{finding.LastRow + 1}), or remove the empty rows with the delete_rows operation of 'cells edit' after converting to xlsx; row numbers below them then move up.",
            sheetName, string.Join(",", finding.Rows.Take(ListedRows).Select(static row => RowReference(row + 1, row + 1))));
    }

    private static Warning TotalRows(TextTableFinding finding, string sheetName)
    {
        int first = finding.Rows[0] + 1;
        int end = (finding.DataEnd ?? finding.Rows[0] - 1) + 1;
        string labels = string.Join("', '", finding.Labels);
        string subject = finding.Rows.Count == 1
            ? $"Row {first} is a total row ('{labels}')"
            : $"Rows {string.Join(", ", finding.Rows.Select(static row => (row + 1).ToString(CultureInfo.InvariantCulture)))} are total rows ('{labels}')";
        int dataStart = finding.HeaderRow + 2;
        // The example sums a column the total row holds a number in, so it is never a text column.
        string example = finding.NumberColumn is int column
            ? $", for example =SUM({A1.ColumnName(column)}{dataStart}:{A1.ColumnName(column)}{end}) for column {A1.ColumnName(column)}"
            : string.Empty;
        return Build(
            $"{subject}; a sum or formula over a column that includes it counts the data twice, and sorts, pivots and charts take it for data.",
            end >= dataStart
                ? $"End data ranges at row {end}{example}, and leave row {first} out of sorts, pivots and charts."
                : $"Leave row {first} out of formulas, sorts, pivots and charts.",
            sheetName, string.Join(",", finding.Rows.Select(static row => RowReference(row + 1, row + 1))));
    }

    private static Warning EvaluationNotice(TextTableFinding finding, string sheetName)
    {
        int row = finding.LastRow + 1;
        return Build(
            $"Row {row} is not data: it is the evaluation notice an Aspose.Cells export without a license wrote after the data.",
            $"Tell the user, and leave row {row} out of every range, or delete it with the delete_rows operation of 'cells edit' after converting to xlsx.",
            sheetName, RowReference(row, row));
    }

    /// <summary>A layout warning located at the <paramref name="rows"/> of sheet <paramref name="sheetName"/>, such as <c>row/Sales!1:3</c>.</summary>
    private static Warning Build(string message, string hint, string sheetName, string rows) => new(CellsDiagnostics.TextTableLayout, message)
    {
        Hint = hint,
        Docs = "cells/troubleshooting",
        Location = CellsAddresses.At("row", sheetName, rows),
    };

    // One row as its number, as a row's target names it (row/Sales!5); a span of rows as an A1 row range (row/Sales!1:3).
    private static string RowReference(int first, int last) => first == last
        ? first.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{first}:{last}");

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
