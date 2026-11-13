using System.Globalization;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>Translates engine cells into the unified cell model.</summary>
internal static class CellMapper
{
    /// <summary>Maps one cell; <paramref name="cell"/> is null for never-written cells.</summary>
    public static CellsCellData Map(Cell? cell, bool includeFormula, string? styleId)
    {
        if (cell is null || cell.Type == CellValueType.IsNull)
        {
            // A cell can be empty yet styled (blank with a fill color), or
            // empty with a formula that produced nothing yet.
            return new CellsCellData
            {
                T = CellsCellValueTypes.Empty,
                F = includeFormula && cell is { IsFormula: true } ? cell.Formula : null,
                StyleId = styleId,
            };
        }

        string? formula = includeFormula && cell.IsFormula ? cell.Formula : null;
        (object? value, string type) = cell.Type switch
        {
            CellValueType.IsNumeric => ((object?)cell.DoubleValue, CellsCellValueTypes.Number),
            CellValueType.IsDateTime => (FormatDateTime(cell), CellsCellValueTypes.DateTime),
            CellValueType.IsBool => (cell.BoolValue, CellsCellValueTypes.Boolean),
            CellValueType.IsError => (cell.StringValue, CellsCellValueTypes.Error),
            _ => (cell.StringValue, CellsCellValueTypes.String),
        };

        return new CellsCellData { V = value, T = type, F = formula, StyleId = styleId };
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
        CellValueType.IsDateTime => FormatDateTime(cell),
        _ => null,
    };

    /// <summary>
    /// Excel date/time values are timezone-naive; they serialize as local
    /// ISO 8601 without an offset, exactly as a user would read them, with the
    /// milliseconds Excel keeps when a time has any. Shared with
    /// the text-export path so <c>convert --to csv</c> and <c>read</c> render a
    /// date identically (and both invariantly, not in the machine's locale).
    /// Serial 60 of the 1900 date system is 29 February 1900, a day Excel counts
    /// for compatibility with Lotus 1-2-3 and DateTime has not, which the cell's
    /// DateTimeValue gives as the day before.
    /// </summary>
    internal static string FormatDateTime(Cell cell)
    {
        DateTime value = cell.DateTimeValue;
        string day = cell.DoubleValue is >= 60 and < 61 && !cell.Worksheet.Workbook.Settings.Date1904
            ? "1900-02-29" : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return value.TimeOfDay == TimeSpan.Zero ? day : day + value.ToString("THH:mm:ss.FFF", CultureInfo.InvariantCulture);
    }
}
