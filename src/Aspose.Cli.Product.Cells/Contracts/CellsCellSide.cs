namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// One side of a changed cell: its canonical stored value and optional formula. Dates are
/// ISO 8601 strings; an absent empty side is omitted.
/// </summary>
public sealed record CellsCellSide
{
    /// <summary>Stored type: empty, number, datetime, string, boolean or error.</summary>
    [AllowedValues(typeof(CellsCellSideTypes))]
    public required string T { get; init; }

    /// <summary>
    /// Canonical stored value: a number, an ISO 8601 string for a date or time (local, without
    /// an offset, as a read reports it), a string, a boolean or an error literal; omitted for an
    /// empty cell. Two cells holding the same serial are equal whether or not one is formatted
    /// as a date.
    /// </summary>
    [OneOfBy("t", CellsCellSideTypes.Empty)]
    [OneOfBy("t", CellsCellSideTypes.Number, Type = "number")]
    [OneOfBy("t", CellsCellSideTypes.String, CellsCellSideTypes.DateTime, CellsCellSideTypes.Error, Type = "string")]
    [OneOfBy("t", CellsCellSideTypes.Boolean, Type = "boolean")]
    public object? V { get; init; }

    /// <summary>Formula, when present and in scope.</summary>
    public string? F { get; init; }
}

/// <summary>Stored types of a <see cref="CellsCellSide"/>.</summary>
public static class CellsCellSideTypes
{
    /// <summary>An empty cell.</summary>
    public const string Empty = "empty";

    /// <summary>A number.</summary>
    public const string Number = "number";

    /// <summary>A date or time, reported as an ISO 8601 string.</summary>
    public const string DateTime = "datetime";

    /// <summary>Text.</summary>
    public const string String = "string";

    /// <summary>A boolean.</summary>
    public const string Boolean = "boolean";

    /// <summary>An error value such as <c>#DIV/0!</c>.</summary>
    public const string Error = "error";
}
