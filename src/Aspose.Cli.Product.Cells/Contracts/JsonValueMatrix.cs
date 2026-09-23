using System.Text.Json;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Normalizes JSON value matrices (from ops documents or <c>--data</c> files)
/// into plain CLR values, so engine adapters never see serializer types.
/// </summary>
internal static class JsonValueMatrix
{
    /// <summary>
    /// Converts a raw deserialized matrix into strings, doubles, booleans and
    /// nulls. Rejects empty and ragged matrices and non-scalar entries.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<object?>> Normalize(
        IReadOnlyList<IReadOnlyList<object?>> values,
        Func<string, Exception> invalid)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(invalid);

        if (values.Count == 0 || values[0].Count == 0)
        {
            throw invalid("the values matrix is empty");
        }

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
            string or bool or double => value,
            JsonElement element => NormalizeElement(element, row, column, invalid),
            _ => throw invalid($"cell [{row},{column}] has unsupported type {value.GetType().Name}"),
        };

    private static object? NormalizeElement(
        JsonElement element, int row, int column, Func<string, Exception> invalid) => element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw invalid(
                $"cell [{row},{column}] must be a string, number, boolean or null, not {element.ValueKind}"),
        };
}
