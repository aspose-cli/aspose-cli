using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that write cell values, formulas and formatting within a range.

/// <summary>
/// Writes a row-major matrix of values: strings, numbers, Booleans, or null to clear a cell.
/// Every row has the same number of cells, at least one. A single-cell range is the top-left
/// anchor; a larger range must match the matrix dimensions exactly.
/// </summary>
[Operation("set_values")]
public sealed record SetValuesOp : CellsSheetOp
{
    /// <summary>The anchor cell or the exact target range, such as B2 or B2:D4.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The rows of values.</summary>
    [ReadBackByPosition] // Reads back as the v of the compact cells (kept on purpose).
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
/// per cell, absolute (<c>$</c>) references stay. Every cell evaluates like Excel 365: array
/// arithmetic inside a function, such as =MAX(B2*$F$2:$F$8-$G$2:$G$8), covers the whole ranges,
/// and a formula set on a single cell whose result is an array, such as FILTER or UNIQUE, spills
/// into the cells beside and below it, or shows #SPILL! when they are not empty.
/// </summary>
[Operation("set_formula")]
public sealed record SetFormulaOp : CellsSheetOp
{
    /// <summary>The range to fill, such as C2:C100.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The formula of the range's top-left cell in A1 notation, such as =SUM(B2:B10).</summary>
    [ReadBackByPosition] // Reads back as the f of the compact cells (kept on purpose).
    [Pattern("^=")] public required string Formula { get; init; }
}

/// <summary>Clears the contents, the formatting or both of a range.</summary>
[Operation("clear_range")]
public sealed record ClearRangeOp : CellsSheetOp
{
    /// <summary>The range to clear, such as B2:D10.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>What to clear: contents keeps the formatting, formats keeps the contents, all clears both and the sparklines drawn in the range.</summary>
    [NotState]
    [AllowedValues(typeof(CellsClearTargets))] public string What { get; init; } = CellsClearTargets.Contents;
}

/// <summary>Accepted values of <see cref="ClearRangeOp.What"/>.</summary>
public static class CellsClearTargets
{
    public const string Contents = "contents";
    public const string Formats = "formats";
    public const string Everything = "all";
}

/// <summary>Copies a range with its values, formulas and formatting to an anchor cell.</summary>
[Operation("copy_range")]
public sealed record CopyRangeOp : CellsSheetOp
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
[MistakenFor("set_style", "format_cells")]
public sealed record FormatRangeOp : CellsSheetOp
{
    /// <summary>The range to format, such as B2:D10.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The formatting to apply; each field left out keeps the cells' current value.</summary>
    public required CellsStyleData Style { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        // Excel indents only text aligned left or right: an indent puts centered text on the left.
        OperationInvalidException.Require(Style is not { Alignment: CellsHorizontalAlignments.Center, Indent: > 0 },
            "an indent applies only to text aligned left or right, so centered text with an indent stays on the left",
            "Drop indent, or set alignment to left or right.");
        return this;
    }
}

/// <summary>Merges a range into one cell.</summary>
[Operation("merge_cells")]
public sealed record MergeCellsOp : CellsSheetOp
{
    /// <summary>The range to merge, such as A1:C1.</summary>
    [A1Range] public required string Range { get; init; }
}

/// <summary>Splits a merged range.</summary>
[Operation("unmerge_cells")]
public sealed record UnmergeCellsOp : CellsSheetOp
{
    /// <summary>The merged range to split, such as A1:C1.</summary>
    [A1Range] public required string Range { get; init; }
}

/// <summary>
/// Replaces literal or regular-expression text within the text values of a sheet's cells, in
/// address order, row by row. Formulas, numbers, dates and Booleans are never changed; a cell's
/// whole text is matched, not the text its number format shows.
/// </summary>
[Operation("replace_text")]
[MistakenFor("find_replace", "replace")]
public sealed record ReplaceTextOp : CellsSheetOp
{
    /// <summary>The range to replace in, such as A1:D20; every cell of the sheet when omitted.</summary>
    [A1Range] public string? Range { get; init; }

    /// <summary>The text to find, or the pattern when regex is true.</summary>
    [NotState]
    [MinLength(1)] public required string Find { get; init; }

    /// <summary>With regex, .NET substitutions such as $1 and ${name} apply; write $$ for a literal $.</summary>
    [MistakenFor("replacement", "replaceWith")]
    [NotState]
    public required string Replace { get; init; }

    /// <summary>Whether find is a .NET regular expression rather than literal text.</summary>
    [NotState]
    public bool Regex { get; init; }

    /// <summary>Whether a match must have the same letter case as find.</summary>
    [MistakenFor("matchCase")]
    [NotState]
    public bool CaseSensitive { get; init; }

    /// <summary>Whether a match must not have a letter, digit or underscore just before or after it.</summary>
    [NotState]
    public bool WholeWord { get; init; }

    /// <summary>The most matches replaced; every match when omitted.</summary>
    [NotState]
    [Minimum(1)] public int? MaxReplacementCount { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Regex)
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(Find, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            }
            catch (ArgumentException exception)
            {
                throw new OperationInvalidException($"find is not a valid .NET regular expression: {exception.Message}",
                    "Escape the characters . $ ^ { [ ( | ) * + ? \\ with a backslash, or set regex to false for literal text.");
            }
        }

        return this;
    }
}
