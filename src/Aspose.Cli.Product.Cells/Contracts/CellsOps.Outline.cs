using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Row and column outline grouping — the collapsible +/- summaries in Excel.

/// <summary>Groups a span of rows into an outline level.</summary>
[Operation("group_rows")]
public sealed record GroupRowsOp : CellsSheetOp
{
    /// <summary>The first row of the group (1-based).</summary>
    [MistakenFor("from")]
    [Minimum(1)] public required int First { get; init; }

    /// <summary>The last row of the group (1-based), not above first; first when omitted.</summary>
    [MistakenFor("to")]
    [Minimum(1)] public int? Last { get; init; }

    /// <summary>Whether the new group is collapsed.</summary>
    [MistakenFor("collapse")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsRowGroupInfo/properties/collapsed")]
    public bool Collapsed { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => RowSpan.Check(this, First, Last);
}

/// <summary>Removes one outline level from a span of rows.</summary>
[Operation("ungroup_rows")]
public sealed record UngroupRowsOp : CellsSheetOp
{
    /// <summary>The first row of the span (1-based).</summary>
    [MistakenFor("from")]
    [Minimum(1)] public required int First { get; init; }

    /// <summary>The last row of the span (1-based), not above first; first when omitted.</summary>
    [MistakenFor("to")]
    [Minimum(1)] public int? Last { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => RowSpan.Check(this, First, Last);
}

/// <summary>Groups a span of columns into an outline level.</summary>
[Operation("group_columns")]
public sealed record GroupColumnsOp : CellsSheetOp
{
    /// <summary>The first column of the group.</summary>
    [MistakenFor("from")]
    [A1Column] public required string First { get; init; }

    /// <summary>The last column of the group, not left of first; first when omitted.</summary>
    [MistakenFor("to")]
    [A1Column] public string? Last { get; init; }

    /// <summary>Whether the new group is collapsed.</summary>
    [MistakenFor("collapse")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsColumnGroupInfo/properties/collapsed")]
    public bool Collapsed { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, First, Last);
}

/// <summary>Removes one outline level from a span of columns.</summary>
[Operation("ungroup_columns")]
public sealed record UngroupColumnsOp : CellsSheetOp
{
    /// <summary>The first column of the span.</summary>
    [MistakenFor("from")]
    [A1Column] public required string First { get; init; }

    /// <summary>The last column of the span, not left of first; first when omitted.</summary>
    [MistakenFor("to")]
    [A1Column] public string? Last { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, First, Last);
}

/// <summary>The rule of a span of rows: it must not end above its first row.</summary>
internal static class RowSpan
{
    public static CellsOp Check(CellsOp operation, int first, int? last)
    {
        OperationInvalidException.Require(last is null || last >= first, "'last' must not be smaller than 'first'");
        return operation;
    }
}

/// <summary>The rule of a span of columns: it must not end left of its first column.</summary>
internal static class ColumnSpan
{
    public static CellsOp Check(CellsOp operation, string first, string? last)
    {
        OperationInvalidException.Require(last is null || A1.ParseColumn(last) >= A1.ParseColumn(first), "'last' must not be left of 'first'");
        return operation;
    }
}
