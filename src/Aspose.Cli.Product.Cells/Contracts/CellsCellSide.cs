namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// One side of a changed cell: its canonical stored value and optional formula. Dates are raw
/// numeric serials; an absent empty side is omitted.
/// </summary>
public sealed record CellsCellSide
{
    /// <summary>Stored type: empty, number, string, boolean or error. Dates are numbers.</summary>
    [AllowedValues(typeof(CellsCellSideTypes))]
    public required string T { get; init; }

    /// <summary>
    /// Canonical stored value: a number (raw Excel serials regardless of date formatting), a
    /// string, a boolean or an error literal; omitted for an empty cell.
    /// </summary>
    [OneOfBy("t", CellsCellSideTypes.Empty)]
    [OneOfBy("t", CellsCellSideTypes.Number, Type = "number")]
    [OneOfBy("t", CellsCellSideTypes.String, CellsCellSideTypes.Error, Type = "string")]
    [OneOfBy("t", CellsCellSideTypes.Boolean, Type = "boolean")]
    public object? V { get; init; }

    /// <summary>Formula, when present and in scope.</summary>
    public string? F { get; init; }
}
