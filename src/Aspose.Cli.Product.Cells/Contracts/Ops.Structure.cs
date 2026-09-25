using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that change the row and column structure of a sheet.

/// <summary>Inserts rows before a row.</summary>
[Operation("insert_rows")]
public sealed record InsertRowsOp : Op
{
    /// <summary>The 1-based row to insert before.</summary>
    [Minimum(1)] public required int At { get; init; }

    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Deletes rows starting at a row.</summary>
[Operation("delete_rows")]
public sealed record DeleteRowsOp : Op
{
    /// <summary>The 1-based first row to delete.</summary>
    [Minimum(1)] public required int At { get; init; }

    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Inserts columns before a column.</summary>
[Operation("insert_columns")]
public sealed record InsertColumnsOp : Op
{
    /// <summary>The column to insert before, such as C.</summary>
    [A1Column] public required string At { get; init; }

    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Deletes columns starting at a column.</summary>
[Operation("delete_columns")]
public sealed record DeleteColumnsOp : Op
{
    /// <summary>The first column to delete, such as C.</summary>
    [A1Column] public required string At { get; init; }

    [Minimum(1)] public int Count { get; init; } = 1;
}

/// <summary>Sets the height of a span of rows, or auto-fits them when height is omitted.</summary>
[Operation("resize_rows")]
public sealed record ResizeRowsOp : Op
{
    /// <summary>The 1-based first row.</summary>
    [Minimum(1)] public required int From { get; init; }

    /// <summary>The 1-based last row, not above from; from when omitted.</summary>
    [Minimum(1)] public int? To { get; init; }

    /// <summary>The height in points; omitted auto-fits.</summary>
    [Minimum(0)] public double? Height { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => RowSpan.Check(this, From, To);
}

/// <summary>Sets the width of a span of columns, or auto-fits them when width is omitted.</summary>
[Operation("resize_columns")]
public sealed record ResizeColumnsOp : Op
{
    /// <summary>The first column.</summary>
    [A1Column] public required string From { get; init; }

    /// <summary>The last column, not left of from; from when omitted.</summary>
    [A1Column] public string? To { get; init; }

    /// <summary>The width in characters; omitted auto-fits.</summary>
    [Minimum(0)] public double? Width { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, From, To);
}
