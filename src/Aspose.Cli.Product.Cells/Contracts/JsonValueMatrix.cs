using System.Text.Json;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Normalizes JSON value matrices (from ops documents)
/// into plain CLR values, so engine adapters never see serializer types.
/// </summary>
internal static class JsonValueMatrix
{
    /// <summary>
    /// Converts a raw deserialized matrix, which the contract keeps non-empty, into strings,
    /// doubles, booleans and nulls; a JSON number a number cell would not keep in full stays the
    /// text it is written as (<see cref="NumberText.DropsCharacters"/>), as a <c>--set</c> value
    /// does. Rejects ragged matrices and non-scalar entries.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<object?>> Normalize(
        IReadOnlyList<IReadOnlyList<object?>> values,
        Func<string, Exception> invalid)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(invalid);

        int width = values[0].Count;
        var rows = new List<IReadOnlyList<object?>>(values.Count);
        for (int rowIndex = 0; rowIndex < values.Count; rowIndex++)
        {
            IReadOnlyList<object?> row = values[rowIndex];
            if (row.Count != width)
            {
                throw invalid($"the values matrix is ragged: row 0 has {width} cells but row {rowIndex} has {row.Count}");
            }

            var cells = new object?[width];
            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                cells[columnIndex] = NormalizeScalar(row[columnIndex], rowIndex, columnIndex, invalid);
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static object? NormalizeScalar(
        object? value, int row, int column, Func<string, Exception> invalid) => value switch
        {
            null => null,
            string or bool or double or decimal => value,
            JsonElement element => NormalizeElement(element, row, column, invalid),
            _ => throw invalid($"cell [{row},{column}] has unsupported type {value.GetType().Name}"),
        };

    private static object? NormalizeElement(
        JsonElement element, int row, int column, Func<string, Exception> invalid) => element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => NumberText.DropsCharacters(element.GetRawText()) ? element.GetRawText() : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw invalid(
                $"cell [{row},{column}] must be a string, number, boolean or null, not {element.ValueKind}"),
        };
}
