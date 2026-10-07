using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>A storage value for comparison, independent of display formatting and date systems.</summary>
internal readonly record struct ComparisonValue(string Type, object? Value)
{
    internal static ComparisonValue From(Cell? cell) => cell?.Type switch
    {
        null or CellValueType.IsNull => new(CellValueTypes.Empty, null),
        CellValueType.IsNumeric or CellValueType.IsDateTime => new(CellValueTypes.Number, cell.DoubleValue),
        CellValueType.IsBool => new(CellValueTypes.Boolean, cell.BoolValue),
        CellValueType.IsError => new(CellValueTypes.Error, cell.StringValue),
        CellValueType.IsString => new(CellValueTypes.String, cell.StringValue),
        _ => throw new InvalidDataException("The workbook contains an unsupported comparison value type."),
    };

    internal CellSide? Side(string? formula) =>
        Type == CellValueTypes.Empty && formula is null ? null
            : new CellSide { T = Type, V = Value, F = formula };
}
