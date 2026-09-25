using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Row and column outline grouping — the collapsible +/- summaries in Excel.

/// <summary>Groups a span of rows into an outline level.</summary>
[Operation("group_rows")]
public sealed record GroupRowsOp : Op
{
    /// <summary>The first row of the group (1-based).</summary>
    [Minimum(1)] public required int From { get; init; }

    /// <summary>The last row of the group (1-based), not above from; from when omitted.</summary>
    [Minimum(1)] public int? To { get; init; }

    /// <summary>Whether the new group is collapsed.</summary>
    public bool Collapse { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => RowSpan.Check(this, From, To);
}

/// <summary>Removes one outline level from a span of rows.</summary>
[Operation("ungroup_rows")]
public sealed record UngroupRowsOp : Op
{
    /// <summary>The first row of the span (1-based).</summary>
    [Minimum(1)] public required int From { get; init; }

    /// <summary>The last row of the span (1-based), not above from; from when omitted.</summary>
    [Minimum(1)] public int? To { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => RowSpan.Check(this, From, To);
}

/// <summary>Groups a span of columns into an outline level.</summary>
[Operation("group_columns")]
public sealed record GroupColumnsOp : Op
{
    /// <summary>The first column of the group.</summary>
    [A1Column] public required string From { get; init; }

    /// <summary>The last column of the group, not left of from; from when omitted.</summary>
    [A1Column] public string? To { get; init; }

    /// <summary>Whether the new group is collapsed.</summary>
    public bool Collapse { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, From, To);
}

/// <summary>Removes one outline level from a span of columns.</summary>
[Operation("ungroup_columns")]
public sealed record UngroupColumnsOp : Op
{
    /// <summary>The first column of the span.</summary>
    [A1Column] public required string From { get; init; }

    /// <summary>The last column of the span, not left of from; from when omitted.</summary>
    [A1Column] public string? To { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated() => ColumnSpan.Check(this, From, To);
}

/// <summary>The rule of a span of rows: it must not end above its first row.</summary>
internal static class RowSpan
{
    public static Op Check(Op operation, int from, int? to)
    {
        OperationInvalidException.Require(to is null || to >= from, "'to' must not be smaller than 'from'");
        return operation;
    }
}

/// <summary>The rule of a span of columns: it must not end left of its first column.</summary>
internal static class ColumnSpan
{
    public static Op Check(Op operation, string from, string? to)
    {
        OperationInvalidException.Require(to is null || A1.ParseColumn(to) >= A1.ParseColumn(from), "'to' must not be left of 'from'");
        return operation;
    }
}
