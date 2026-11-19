using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The value kinds the Cells table adds to the SDK's: a cell's value, a range's values, A1 names
/// and numbers, and the areas a call takes.
/// </summary>
internal static class CellsValues
{
    /// <summary>
    /// A cell's value: a number, a string, true or false, or null. A date is the number the engine
    /// holds for it; an error value is its text, such as <c>#N/A</c>.
    /// </summary>
    internal static ValueKind<object?> CellValue { get; } = ValueKinds.Create<object?>(
        "cell-value",
        "A number, a string, true or false, or null for none. Written, its JSON type is the type the cell gets: a string calls PutValue(string), a number PutValue(double), true or false PutValue(bool), null PutValue(null). A date is a number with a date format.",
        static () => new JsonObject { ["type"] = new JsonArray("number", "string", "boolean", "null") },
        Encode,
        Decode);

    /// <summary>A range's values: one list per row, each holding one cell value per column.</summary>
    internal static ValueKind<object?[][]> Matrix { get; } = ValueKinds.Create<object?[][]>(
        "range-value",
        "One list per row of the range, each with one value per column: a number, a string, true or false, or null. Written, the lists have the range's numbers of rows and columns.",
        static () => new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = new JsonArray("number", "string", "boolean", "null") } },
        },
        static rows => new JsonArray([.. rows.Select(static row => (JsonNode)new JsonArray([.. row.Select(Encode)]))]),
        static value => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(static row => row.ValueKind == JsonValueKind.Array)
            ? [.. value.EnumerateArray().Select(static row => row.EnumerateArray().Select(Decode).ToArray())]
            : throw ValueKinds.Expected("a list of rows, each a list of values", value));

    /// <summary>Frozen panes, as <see cref="Worksheet.GetFreezedPanes"/> gives them, the cell as A1 writes it.</summary>
    internal sealed record FreezedPanes(string CellName, int FreezedRows, int FreezedColumns);

    /// <summary>Frozen panes: <c>{"cellName": "B2", "freezedRows": 1, "freezedColumns": 1}</c>, or null.</summary>
    internal static ValueKind<FreezedPanes?> Panes { get; } = ValueKinds.Create<FreezedPanes?>(
        "freezed-panes",
        "The frozen panes: cellName, the cell as A1 writes it, and freezedRows and freezedColumns; null when the panes are not frozen.",
        static () => new JsonObject { ["type"] = new JsonArray("object", "null") },
        static panes => panes is null ? null : new JsonObject { ["cellName"] = panes.CellName, ["freezedRows"] = panes.FreezedRows, ["freezedColumns"] = panes.FreezedColumns },
        static value => throw ValueKinds.Expected("settable; the freeze_panes action sets the panes", value));

    /// <summary>A row number as A1 writes it, from 1, such as the 6 of B6.</summary>
    internal static ValueKind<int> RowNumber { get; } = ValueKinds.Create<int>(
        "row-number",
        "A row number as A1 writes it, from 1, such as the 6 of B6.",
        static () => new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
        static value => JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int row) && row >= 1
            ? row
            : throw ValueKinds.Expected("a row number from 1", value));

    /// <summary>A column name as A1 writes it, such as the B of B6.</summary>
    internal static ValueKind<string> ColumnName { get; } = ValueKinds.KeyText(KeyPart.ColumnName, "column-name");

    /// <summary>A cell name as A1 writes it, such as B6.</summary>
    internal static ValueKind<string> CellName { get; } = ValueKinds.KeyText(KeyPart.CellName, "cell-name");

    /// <summary>An area as A1 writes it, such as B2:D10, B6, 5:5 or C:C.</summary>
    internal static ValueKind<string> Area { get; } = ValueKinds.KeyText(KeyPart.Area, "area");

    /// <summary>The engine's row index of a row number, through <see cref="CellsHelper.RowNameToIndex"/>.</summary>
    internal static int RowIndex(int row) => CellsHelper.RowNameToIndex(row.ToString(CultureInfo.InvariantCulture));

    /// <summary>The engine's column index of a column name, through <see cref="CellsHelper.ColumnNameToIndex"/>.</summary>
    internal static int ColumnIndex(string column) => CellsHelper.ColumnNameToIndex(column);

    /// <summary>
    /// The engine's cell area of an A1 area on <paramref name="sheet"/>: the range
    /// <c>Cells.CreateRange(area)</c> makes of it, from its first row and column for its
    /// numbers of rows and columns.
    /// </summary>
    internal static CellArea CellArea(Worksheet sheet, string area)
    {
        Aspose.Cells.Range range = sheet.Cells.CreateRange(area);
        return Aspose.Cells.CellArea.CreateCellArea(range.FirstRow, range.FirstColumn, range.FirstRow + range.RowCount - 1, range.FirstColumn + range.ColumnCount - 1);
    }

    /// <summary>An engine cell area as A1 writes it: <c>B6</c> for one cell, <c>B2:D10</c> otherwise.</summary>
    internal static string A1(CellArea area)
    {
        string start = CellsHelper.CellIndexToName(area.StartRow, area.StartColumn);
        return area.StartRow == area.EndRow && area.StartColumn == area.EndColumn
            ? start
            : $"{start}:{CellsHelper.CellIndexToName(area.EndRow, area.EndColumn)}";
    }

    /// <summary>
    /// What <see cref="Aspose.Cells.Range.Value"/> gives, as rows: the engine gives one value for a
    /// range of one cell, and a two-dimensional array otherwise. A date is its serial number in
    /// the workbook's date system.
    /// </summary>
    internal static object?[][] Rows(Aspose.Cells.Range range)
    {
        bool date1904 = range.Worksheet.Workbook.Settings.Date1904;
        object? value = range.Value;
        if (value is not object[,] matrix)
        {
            return [[Scalar(value, date1904)]];
        }

        var rows = new object?[matrix.GetLength(0)][];
        for (int row = 0; row < rows.Length; row++)
        {
            rows[row] = new object?[matrix.GetLength(1)];
            for (int column = 0; column < rows[row].Length; column++)
            {
                rows[row][column] = Scalar(matrix[row, column], date1904);
            }
        }

        return rows;
    }

    /// <summary>Sets <see cref="Aspose.Cells.Range.Value"/> to rows that have the range's numbers of rows and columns.</summary>
    /// <exception cref="Aspose.Cli.Sdk.Errors.CliException"><c>OPS_INVALID</c> when they do not.</exception>
    internal static void SetRows(Aspose.Cells.Range range, object?[][] rows)
    {
        int columns = rows.Length == 0 ? 0 : rows[0].Length;
        if (rows.Any(row => row.Length != columns))
        {
            throw TableErrors.DoesNotFit($"the value's rows have {string.Join(", ", rows.Select(static row => row.Length).Distinct())} values, and a range's rows each have {range.ColumnCount}");
        }

        if (rows.Length != range.RowCount || columns != range.ColumnCount)
        {
            throw TableErrors.DoesNotFit($"the value is {Plural(rows.Length, "row")} by {Plural(columns, "column")} and the range {Plural(range.RowCount, "row")} by {Plural(range.ColumnCount, "column")}");
        }

        var matrix = new object?[rows.Length, columns];
        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                matrix[row, column] = rows[row][column];
            }
        }

        range.Value = matrix;
    }

    /// <summary>Sets a cell's value with the <see cref="Cell.PutValue(string)"/> overload of the value's type.</summary>
    internal static void Put(Cell cell, object? value)
    {
        switch (value)
        {
            case double number:
                cell.PutValue(number);
                break;
            case bool flag:
                cell.PutValue(flag);
                break;
            case string text:
                cell.PutValue(text);
                break;
            default:
                cell.PutValue((string?)null);
                break;
        }
    }

    /// <summary>A cell's value as <see cref="CellValue"/> writes it: a date as the number the engine holds for it.</summary>
    internal static object? Of(Cell cell) => cell.Type == CellValueType.IsDateTime ? cell.DoubleValue : cell.Value;

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static object? Scalar(object? value, bool date1904) =>
        value is DateTime date ? CellsHelper.GetDoubleFromDateTime(date, date1904) : value;

    private static JsonNode? Encode(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        double number => double.IsFinite(number) ? JsonValue.Create(number) : null,
        IConvertible number when number.GetTypeCode() is >= TypeCode.SByte and <= TypeCode.Decimal => JsonValue.Create(number.ToDouble(CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    private static object? Decode(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw ValueKinds.Expected("a number, a string, true, false or null", value),
    };
}
