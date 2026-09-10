namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that write cell values, formulas and formatting within a range.

/// <summary>Recalculate all workbook formulas at this point in the batch.</summary>
public sealed record RecalculateOp() : Op(OpNames.Recalculate);

/// <summary>
/// Writes a matrix of values. A single-cell <c>range</c> is the top-left
/// anchor; a multi-cell range must match the matrix dimensions exactly.
/// Values are strings, numbers, booleans or null (clears the cell).
/// </summary>
public sealed record SetValuesOp() : Op(OpNames.SetValues)
{
    /// <summary>Anchor cell or exact target range, e.g. <c>B2</c> or <c>B2:D4</c>.</summary>
    public required string Range { get; init; }

    /// <summary>Row-major value matrix.</summary>
    public required IReadOnlyList<IReadOnlyList<object?>> Values { get; init; }
}

/// <summary>
/// Sets a formula on every cell of a range with Excel fill semantics:
/// relative references shift per cell, absolute (<c>$</c>) references stay.
/// </summary>
public sealed record SetFormulaOp() : Op(OpNames.SetFormula)
{
    /// <summary>The range to fill with the formula, e.g. <c>C2:C100</c>.</summary>
    public required string Range { get; init; }

    /// <summary>Formula in A1 notation for the range's top-left cell, e.g. <c>=SUM(B2:B10)</c>.</summary>
    public required string Formula { get; init; }
}

/// <summary>Clears a range.</summary>
/// <remarks>
/// Optional fields with wire defaults are nullable by design: the serializer
/// materializes absent members as null and the default is applied downstream,
/// so the contract never depends on C# property-initializer behavior.
/// </remarks>
public sealed record ClearRangeOp() : Op(OpNames.ClearRange)
{
    /// <summary>The range to clear, e.g. <c>B2:D10</c>.</summary>
    public required string Range { get; init; }

    /// <summary><c>contents</c> (the default when omitted), <c>formats</c> or <c>all</c>.</summary>
    public string? What { get; init; }
}

/// <summary>Accepted values of <see cref="ClearRangeOp.What"/>.</summary>
public static class ClearTargets
{
    /// <summary>Clear cell contents, keeping formatting (the default when omitted).</summary>
    public const string Contents = "contents";

    /// <summary>Clear formatting, keeping contents.</summary>
    public const string Formats = "formats";

    /// <summary>Clear both contents and formatting.</summary>
    public const string Everything = "all";

    /// <summary>Every target, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Contents, Formats, Everything];
}

/// <summary>Copies a range (values, formulas and formatting).</summary>
public sealed record CopyRangeOp() : Op(OpNames.CopyRange)
{
    /// <summary>Source range; may be sheet-qualified.</summary>
    public required string From { get; init; }

    /// <summary>Destination anchor cell; may be sheet-qualified.</summary>
    public required string To { get; init; }
}

/// <summary>
/// Applies formatting to a range. Only the style fields present in
/// <see cref="Style"/> are touched; existing formatting is preserved.
/// </summary>
public sealed record FormatRangeOp() : Op(OpNames.FormatRange)
{
    /// <summary>The range to format, e.g. <c>B2:D10</c>.</summary>
    public required string Range { get; init; }

    /// <summary>The style fields to apply; only the fields that are set are touched.</summary>
    public required StyleData Style { get; init; }
}

/// <summary>Merges a range into one cell.</summary>
public sealed record MergeCellsOp() : Op(OpNames.MergeCells)
{
    /// <summary>The range to merge, e.g. <c>A1:C1</c>.</summary>
    public required string Range { get; init; }
}

/// <summary>Reverts a merged range.</summary>
public sealed record UnmergeCellsOp() : Op(OpNames.UnmergeCells)
{
    /// <summary>The merged range to split, e.g. <c>A1:C1</c>.</summary>
    public required string Range { get; init; }
}
