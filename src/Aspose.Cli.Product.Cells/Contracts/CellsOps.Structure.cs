using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that change the row and column structure of a sheet.

/// <summary>
/// Inserts rows before a row. The rows inserted end at the sheet's last row, 1048576, at the
/// latest, and the rows they push down must not hold data past it.
/// </summary>
[Operation("insert_rows")]
public sealed record InsertRowsOp : CellsSheetOp
{
    /// <summary>The 1-based row to insert before.</summary>
    [Minimum(1), Maximum(A1.MaxRows)] public required int At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;

    /// <inheritdoc />
    protected override BoundedOperation Validated() => LineSpan.Rows(this, At, Count);
}

/// <summary>Deletes rows starting at a row, up to the sheet's last row, 1048576.</summary>
[Operation("delete_rows")]
public sealed record DeleteRowsOp : CellsSheetOp
{
    /// <summary>The 1-based first row to delete.</summary>
    [Minimum(1), Maximum(A1.MaxRows)] public required int At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;

    /// <inheritdoc />
    protected override BoundedOperation Validated() => LineSpan.Rows(this, At, Count);
}

/// <summary>
/// Inserts columns before a column. The columns inserted end at the sheet's last column, XFD, at
/// the latest, and the columns they push right must not hold data past it.
/// </summary>
[Operation("insert_columns")]
public sealed record InsertColumnsOp : CellsSheetOp
{
    /// <summary>The column to insert before, such as C.</summary>
    [A1Column] public required string At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;

    /// <inheritdoc />
    protected override BoundedOperation Validated() => LineSpan.Columns(this, At, Count);
}

/// <summary>Deletes columns starting at a column, up to the sheet's last column, XFD.</summary>
[Operation("delete_columns")]
public sealed record DeleteColumnsOp : CellsSheetOp
{
    /// <summary>The first column to delete, such as C.</summary>
    [A1Column] public required string At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;

    /// <inheritdoc />
    protected override BoundedOperation Validated() => LineSpan.Columns(this, At, Count);
}

/// <summary>The rule of the rows or columns an operation inserts or deletes: they lie within the sheet.</summary>
internal static class LineSpan
{
    public static CellsOp Rows(CellsOp operation, int at, int count)
    {
        long last = (long)at + count - 1;
        OperationInvalidException.Require(last <= A1.MaxRows,
            $"rows {at}-{last} run past the sheet's last row, {A1.MaxRows}",
            $"Lower count to at most {A1.MaxRows - at + 1}.");
        return operation;
    }

    public static CellsOp Columns(CellsOp operation, string at, int count)
    {
        int first = A1.ParseColumn(at);
        long last = (long)first + count - 1;
        OperationInvalidException.Require(last < A1.MaxColumns,
            $"{count} columns from {A1.ColumnName(first)} run past the sheet's last column, {A1.ColumnName(A1.MaxColumns - 1)}",
            $"Lower count to at most {A1.MaxColumns - first}.");
        return operation;
    }
}

/// <summary>Sets the height of a span of rows, or auto-fits them when height is omitted.</summary>
[Operation("resize_rows")]
public sealed record ResizeRowsOp : CellsSheetOp
{
    /// <summary>The 1-based first row.</summary>
    [MistakenFor("from")]
    [Minimum(1)] public required int First { get; init; }

    /// <summary>The 1-based last row, not above first; first when omitted.</summary>
    [MistakenFor("to")]
    [Minimum(1)] public int? Last { get; init; }

    /// <summary>The height in points; omitted auto-fits.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsRowHeightInfo/properties/height")]
    [Minimum(0)] public double? Height { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => RowSpan.Check(this, First, Last);
}

/// <summary>Sets the width of a span of columns, or auto-fits them when widthChars is omitted.</summary>
[Operation("resize_columns")]
public sealed record ResizeColumnsOp : CellsSheetOp
{
    /// <summary>The first column.</summary>
    [MistakenFor("from")]
    [A1Column] public required string First { get; init; }

    /// <summary>The last column, not left of first; first when omitted.</summary>
    [MistakenFor("to")]
    [A1Column] public string? Last { get; init; }

    /// <summary>The width in characters of the default font's digit 0; omitted auto-fits.</summary>
    [MistakenFor("width")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsColumnWidthInfo/properties/widthChars")]
    [Minimum(0)] public double? WidthChars { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, First, Last);
}
