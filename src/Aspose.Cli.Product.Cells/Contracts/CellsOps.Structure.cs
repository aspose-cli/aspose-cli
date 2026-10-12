using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that change the row and column structure of a sheet.

/// <summary>Inserts rows before a row.</summary>
[Operation("insert_rows")]
public sealed record InsertRowsOp : CellsSheetOp
{
    /// <summary>The 1-based row to insert before.</summary>
    [Minimum(1)] public required int At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Deletes rows starting at a row.</summary>
[Operation("delete_rows")]
public sealed record DeleteRowsOp : CellsSheetOp
{
    /// <summary>The 1-based first row to delete.</summary>
    [Minimum(1)] public required int At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Inserts columns before a column.</summary>
[Operation("insert_columns")]
public sealed record InsertColumnsOp : CellsSheetOp
{
    /// <summary>The column to insert before, such as C.</summary>
    [A1Column] public required string At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Deletes columns starting at a column.</summary>
[Operation("delete_columns")]
public sealed record DeleteColumnsOp : CellsSheetOp
{
    /// <summary>The first column to delete, such as C.</summary>
    [A1Column] public required string At { get; init; }

    [NotState]
    [Minimum(1)] public int Count { get; init; } = 1;
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
    [NotReadBack]
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
    [NotReadBack]
    [Minimum(0)] public double? WidthChars { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, First, Last);
}
