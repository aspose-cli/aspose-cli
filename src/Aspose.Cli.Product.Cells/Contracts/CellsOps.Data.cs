using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for the data-cleaning pipeline: filtering, sorting and validation.

/// <summary>Sets the sheet's AutoFilter over a range; clear_autofilter removes it.</summary>
[Operation("set_autofilter", Hint = "To remove the AutoFilter, use clear_autofilter.")]
public sealed record SetAutoFilterOp : CellsSheetOp
{
    /// <summary>The range to filter, including its header row, such as A1:D20.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsAutofilterInfo/properties/range")]
    [A1Range] public required string Range { get; init; }
}

/// <summary>Removes the sheet's AutoFilter.</summary>
[Operation("clear_autofilter")]
public sealed record ClearAutoFilterOp : CellsSheetOp;

/// <summary>Sorts a range in place by one or more of its columns.</summary>
[Operation("sort_range")]
public sealed record SortRangeOp : CellsSheetOp
{
    /// <summary>The range to sort, such as A2:D100.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The sort keys, primary first; each column lies inside the range.</summary>
    [NotState]
    [MinItems(1)] public required IReadOnlyList<CellsSortKey> By { get; init; }

    /// <summary>Whether the first row is a header that stays in place.</summary>
    [NotState]
    public bool HasHeader { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        // The engine sorts by an absolute column; one outside the range would silently sort nothing.
        RangeRef range = A1.ParseRange(Range).Range;
        foreach (CellsSortKey key in By)
        {
            int column = A1.ParseColumn(key.Column);
            OperationInvalidException.Require(column >= range.Start.Column && column <= range.End.Column,
                $"sort column '{key.Column}' is outside the range '{Range}'");
        }

        return this;
    }
}

/// <summary>One sort key of a sort_range operation.</summary>
public sealed record CellsSortKey
{
    [A1Column] public required string Column { get; init; }

    [AllowedValues(typeof(CellsSortOrders))] public string Order { get; init; } = CellsSortOrders.Asc;
}

/// <summary>Accepted values of <see cref="CellsSortKey.Order"/>.</summary>
public static class CellsSortOrders
{
    public const string Asc = "asc";
    public const string Desc = "desc";
}

/// <summary>
/// Applies data validation, a dropdown list or an input constraint, to a range, replacing the
/// validation the range had. A list takes
/// exactly one of listItems and listSource; custom takes its formula in value1; the other kinds
/// take an operator and value1, and value2 too for between and not-between.
/// </summary>
[Operation("set_validation")]
public sealed record SetValidationOp : CellsSheetOp
{
    /// <summary>
    /// The range to validate, such as B2:B100, or several separated by commas, such as
    /// B2:B10,D2:D10, as a read reports a validation over several areas: one validation covers them all.
    /// </summary>
    [A1RangeList] public required string Range { get; init; }

    [MistakenFor("type")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/kind")]
    [AllowedValues(typeof(CellsValidationTypes))] public required string Kind { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/operator")]
    [AllowedValues(typeof(CellsValidationOperators))] public string? Operator { get; init; }

    /// <summary>The first bound (the lower bound for between), or the formula of a custom validation.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/value1")]
    [Pattern(@"\S")] public string? Value1 { get; init; }

    /// <summary>The upper bound for between and not-between.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/value2")]
    [Pattern(@"\S")] public string? Value2 { get; init; }

    /// <summary>
    /// The dropdown items of a list. No item contains a comma or a double quote, and joined with
    /// commas they stay within Excel's 255 characters; use listSource for longer lists.
    /// </summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/listItems")]
    [MinItems(1), Pattern("^[^,\"]+$")] public IReadOnlyList<string>? ListItems { get; init; }

    /// <summary>The range a list reads its items from, such as Lists!A1:A20.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/listSource")]
    [Pattern(@"\S")] public string? ListSource { get; init; }

    /// <summary>The prompt shown when a validated cell is selected.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/inputMessage")]
    public string? InputMessage { get; init; }

    /// <summary>The message shown when a value is rejected.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>Whether a validated cell may be left blank.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsValidationInfo/properties/allowBlank")]
    public bool AllowBlank { get; init; } = true;

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        switch (Kind)
        {
            case CellsValidationTypes.List:
                OperationInvalidException.Require((ListItems is null) != (ListSource is null),
                    "a 'list' validation needs exactly one of 'listItems' or 'listSource'");
                if (ListItems is { } items)
                {
                    // Excel stores the items as one quoted, comma-separated literal of at most 255 characters.
                    int length = items.Sum(static item => item.Length) + items.Count - 1;
                    OperationInvalidException.Require(length <= 255, $"'listItems' joined with commas is {length} characters; Excel allows 255",
                        "Put the items in cells and give 'listSource' instead, e.g. \"Lists!A1:A40\".");
                }

                break;
            case CellsValidationTypes.Custom:
                OperationInvalidException.Require(Value1 is not null, "a 'custom' validation needs 'value1' set to a formula");
                break;
            default:
                OperationInvalidException.Require(Operator is not null, "'operator' is required for this validation kind");
                OperationInvalidException.Require(Value1 is not null, "'value1' is required");
                OperationInvalidException.Require(Operator is not (CellsValidationOperators.Between or CellsValidationOperators.NotBetween) || Value2 is not null,
                    "'between'/'not-between' need 'value2' as the upper bound");
                break;
        }

        return this;
    }
}

/// <summary>Removes all data validation whose area overlaps a range.</summary>
[Operation("clear_validation")]
public sealed record ClearValidationOp : CellsSheetOp
{
    /// <summary>The range to clear validation from, such as B2:B100, or several separated by commas, such as B2:B10,D2:D10.</summary>
    [A1RangeList] public required string Range { get; init; }
}

/// <summary>Removes duplicate rows from a range, comparing every column or the listed ones.</summary>
[Operation("delete_duplicates")]
[MistakenFor("remove_duplicates")]
public sealed record DeleteDuplicatesOp : CellsSheetOp
{
    /// <summary>The range to de-duplicate, including any header row, such as A1:D100.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The columns inside the range that define a duplicate; every column when omitted.</summary>
    [NotState]
    [MinItems(1), A1Column] public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>Whether the first row is a header that is kept.</summary>
    [NotState]
    public bool HasHeader { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        RangeRef range = A1.ParseRange(Range).Range;
        foreach (string column in Columns ?? [])
        {
            int index = A1.ParseColumn(column);
            OperationInvalidException.Require(index >= range.Start.Column && index <= range.End.Column,
                $"column '{column}' is outside the range '{Range}'");
        }

        return this;
    }
}

/// <summary>Accepted values of <see cref="SetValidationOp.Operator"/>.</summary>
public static class CellsValidationOperators
{
    public const string Between = "between";
    public const string NotBetween = "not-between";
    public const string Equal = "equal";
    public const string NotEqual = "not-equal";
    public const string GreaterThan = "greater-than";
    public const string LessThan = "less-than";
    public const string GreaterOrEqual = "greater-or-equal";
    public const string LessOrEqual = "less-or-equal";
}
