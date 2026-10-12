using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// A storage value for comparison, independent of display formatting and date systems: a date
/// compares as its serial number, so formatting a number as a date is no difference.
/// </summary>
internal readonly record struct ComparisonValue(string Type, object? Value)
{
    internal static ComparisonValue From(Cell? cell) => cell?.Type switch
    {
        null or CellValueType.IsNull => new(CellsCellValueTypes.Empty, null),
        CellValueType.IsNumeric or CellValueType.IsDateTime => new(CellsCellValueTypes.Number, cell.DoubleValue),
        CellValueType.IsBool => new(CellsCellValueTypes.Boolean, cell.BoolValue),
        CellValueType.IsError => new(CellsCellValueTypes.Error, cell.StringValue),
        CellValueType.IsString => new(CellsCellValueTypes.String, cell.StringValue),
        _ => throw new InvalidDataException("The workbook contains an unsupported comparison value type."),
    };

    /// <summary>One side of a difference: a date reported as ISO 8601, as a read reports it.</summary>
    /// <param name="cell">The cell the value came from.</param>
    /// <param name="formula">The cell's formula, when in scope.</param>
    internal CellsCellSide? Side(Cell? cell, string? formula) =>
        Type == CellsCellValueTypes.Empty && formula is null ? null
            : cell?.Type == CellValueType.IsDateTime
                ? new CellsCellSide { T = CellsCellSideTypes.DateTime, V = CellMapper.FormatDateTime(cell.DateTimeValue), F = formula }
                : new CellsCellSide { T = Type, V = Value, F = formula };
}
