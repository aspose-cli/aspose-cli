using System.Globalization;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Translates engine cells into the unified cell model.</summary>
internal static class CellMapper
{
    /// <summary>Maps one cell; <paramref name="cell"/> is null for never-written cells.</summary>
    public static CellData Map(Cell? cell, bool includeFormula, string? styleId)
    {
        if (cell is null || cell.Type == CellValueType.IsNull)
        {
            // A cell can be empty yet styled (blank with a fill color), or
            // empty with a formula that produced nothing yet.
            return new CellData
            {
                T = CellValueTypes.Empty,
                F = includeFormula && cell is { IsFormula: true } ? cell.Formula : null,
                StyleId = styleId,
            };
        }

        string? formula = includeFormula && cell.IsFormula ? cell.Formula : null;
        (object? value, string type) = cell.Type switch
        {
            CellValueType.IsNumeric => ((object?)cell.DoubleValue, CellValueTypes.Number),
            CellValueType.IsDateTime => (FormatDateTime(cell.DateTimeValue), CellValueTypes.DateTime),
            CellValueType.IsBool => (cell.BoolValue, CellValueTypes.Boolean),
            CellValueType.IsError => (cell.StringValue, CellValueTypes.Error),
            _ => (cell.StringValue, CellValueTypes.String),
        };

        return new CellData { V = value, T = type, F = formula, StyleId = styleId };
    }

    /// <summary>
    /// The cell's underlying value as the string <c>read</c> reports — invariant,
    /// unformatted — or <c>null</c> when that already equals the display text
    /// (strings, errors, blanks). Lets <c>search</c> match on the raw value a
    /// caller just read (e.g. <c>13.75572</c>), not only its formatted display
    /// (<c>13.76</c>), so a read-then-search round-trip never misses.
    /// </summary>
    internal static string? RawValueString(Cell cell) => cell.Type switch
    {
        CellValueType.IsNumeric => cell.DoubleValue.ToString(CultureInfo.InvariantCulture),
        CellValueType.IsDateTime => FormatDateTime(cell.DateTimeValue),
        _ => null,
    };

    /// <summary>
    /// Excel date/time values are timezone-naive; they serialize as local
    /// ISO 8601 without an offset, exactly as a user would read them. Shared with
    /// the text-export path so <c>convert --to csv</c> and <c>read</c> render a
    /// date identically (and both invariantly, not in the machine's locale).
    /// </summary>
    internal static string FormatDateTime(DateTime value) =>
        value.ToString(
            value.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-ddTHH:mm:ss",
            CultureInfo.InvariantCulture);
}
