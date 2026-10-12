using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>A storage value for comparison, independent of display formatting and date systems.</summary>
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

    internal CellsCellSide? Side(string? formula) =>
        Type == CellsCellValueTypes.Empty && formula is null ? null
            : new CellsCellSide { T = Type, V = Value, F = formula };
}
