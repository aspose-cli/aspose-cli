namespace Aspose.Cli.Product.Cells.Contracts;

// Row and column outline grouping — the collapsible +/- summaries in Excel.

/// <summary>Groups a span of rows into an outline level.</summary>
public sealed record GroupRowsOp() : Op(OpNames.GroupRows)
{
    /// <summary>First row of the group (1-based).</summary>
    public required int From { get; init; }

    /// <summary>Last row of the group (1-based); the same as <see cref="From"/> when omitted.</summary>
    public int? To { get; init; }

    /// <summary>Collapse the new group; expanded when omitted.</summary>
    public bool? Collapse { get; init; }
}

/// <summary>Removes one outline level from a span of rows.</summary>
public sealed record UngroupRowsOp() : Op(OpNames.UngroupRows)
{
    /// <summary>First row of the span (1-based).</summary>
    public required int From { get; init; }

    /// <summary>Last row of the span (1-based); the same as <see cref="From"/> when omitted.</summary>
    public int? To { get; init; }
}

/// <summary>Groups a span of columns into an outline level.</summary>
public sealed record GroupColumnsOp() : Op(OpNames.GroupColumns)
{
    /// <summary>First column of the group, as letters (e.g. <c>B</c>).</summary>
    public required string From { get; init; }

    /// <summary>Last column of the group; the same as <see cref="From"/> when omitted.</summary>
    public string? To { get; init; }

    /// <summary>Collapse the new group; expanded when omitted.</summary>
    public bool? Collapse { get; init; }
}

/// <summary>Removes one outline level from a span of columns.</summary>
public sealed record UngroupColumnsOp() : Op(OpNames.UngroupColumns)
{
    /// <summary>First column of the span, as letters (e.g. <c>B</c>).</summary>
    public required string From { get; init; }

    /// <summary>Last column of the span; the same as <see cref="From"/> when omitted.</summary>
    public string? To { get; init; }
}
