using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that write cell values, formulas and formatting within a range.

/// <summary>
/// Writes a row-major matrix of values: strings, numbers, Booleans, or null to clear a cell.
/// Every row has the same number of cells, at least one. A single-cell range is the top-left
/// anchor; a larger range must match the matrix dimensions exactly.
/// </summary>
[Operation("set_values")]
public sealed record SetValuesOp : CellsOp
{
    /// <summary>The anchor cell or the exact target range, such as B2 or B2:D4.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The rows of values.</summary>
    [MinItems(1), MinItems(1, Depth = 1), JsonScalar] public required IReadOnlyList<IReadOnlyList<object?>> Values { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        IReadOnlyList<IReadOnlyList<object?>> values = JsonValueMatrix.Normalize(Values, static reason => new OperationInvalidException(reason));
        RangeRef range = A1.ParseRange(Range).Range;
        OperationInvalidException.Require(
            range.CellCount == 1 || (range.RowCount == values.Count && range.ColumnCount == values[0].Count),
            $"the range spans {range.RowCount}x{range.ColumnCount} but the matrix is {values.Count}x{values[0].Count}",
            "Use a single anchor cell (e.g. \"B2\") to write a matrix of any size, or make the range match the matrix.");
        return this with { Values = values };
    }
}

/// <summary>
/// Sets a formula on every cell of a range with Excel fill semantics: relative references shift
/// per cell, absolute (<c>$</c>) references stay.
/// </summary>
[Operation("set_formula")]
public sealed record SetFormulaOp : CellsOp
{
    /// <summary>The range to fill, such as C2:C100.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The formula of the range's top-left cell in A1 notation, such as =SUM(B2:B10).</summary>
    [Pattern("^=")] public required string Formula { get; init; }
}

/// <summary>Clears the contents, the formatting or both of a range.</summary>
[Operation("clear_range")]
public sealed record ClearRangeOp : CellsOp
{
    /// <summary>The range to clear, such as B2:D10.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>What to clear: contents keeps the formatting, formats keeps the contents, all clears both.</summary>
    [AllowedValues(typeof(ClearTargets))] public string What { get; init; } = ClearTargets.Contents;
}

/// <summary>Accepted values of <see cref="ClearRangeOp.What"/>.</summary>
public static class ClearTargets
{
    public const string Contents = "contents";
    public const string Formats = "formats";
    public const string Everything = "all";
}

/// <summary>Copies a range with its values, formulas and formatting to an anchor cell.</summary>
[Operation("copy_range")]
public sealed record CopyRangeOp : CellsOp
{
    /// <summary>The source range; it may name another sheet.</summary>
    [A1Reference] public required string From { get; init; }

    /// <summary>The destination anchor cell; it may name another sheet, such as Summary!A1.</summary>
    [A1Reference] public required string To { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(A1.ParseRange(To).Range.CellCount == 1, "to must be a single anchor cell, e.g. \"Summary!A1\"");
        return this;
    }
}

/// <summary>
/// Formats a range. Only the style fields that are set change; the rest of the existing
/// formatting is preserved. The style must set at least one field.
/// </summary>
[Operation("format_range")]
public sealed record FormatRangeOp : CellsOp
{
    /// <summary>The range to format, such as B2:D10.</summary>
    [A1Range] public required string Range { get; init; }

    public required StyleData Style { get; init; }
}

/// <summary>Merges a range into one cell.</summary>
[Operation("merge_cells")]
public sealed record MergeCellsOp : CellsOp
{
    /// <summary>The range to merge, such as A1:C1.</summary>
    [A1Range] public required string Range { get; init; }
}

/// <summary>Splits a merged range.</summary>
[Operation("unmerge_cells")]
public sealed record UnmergeCellsOp : CellsOp
{
    /// <summary>The merged range to split, such as A1:C1.</summary>
    [A1Range] public required string Range { get; init; }
}
