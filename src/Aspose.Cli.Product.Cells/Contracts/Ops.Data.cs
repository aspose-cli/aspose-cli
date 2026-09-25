using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for the data-cleaning pipeline: filtering, sorting and validation.

/// <summary>Adds an AutoFilter over a range, or removes the sheet's AutoFilter.</summary>
[Operation("set_autofilter")]
[AtLeastOneOf("range", "off")]
public sealed record SetAutoFilterOp : Op
{
    /// <summary>The range to filter, including its header row, such as A1:D20; required unless off is true.</summary>
    [A1Range] public string? Range { get; init; }

    /// <summary>Whether the sheet's AutoFilter is removed instead of added.</summary>
    public bool Off { get; init; }
}

/// <summary>Sorts a range in place by one or more of its columns.</summary>
[Operation("sort_range")]
public sealed record SortRangeOp : Op
{
    /// <summary>The range to sort, such as A2:D100.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The sort keys, primary first; each column lies inside the range.</summary>
    [MinItems(1)] public required IReadOnlyList<SortKey> By { get; init; }

    /// <summary>Whether the first row is a header that stays in place.</summary>
    public bool HasHeader { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        // The engine sorts by an absolute column; one outside the range would silently sort nothing.
        RangeRef range = A1.ParseRange(Range).Range;
        foreach (SortKey key in By)
        {
            int column = A1.ParseColumn(key.Column);
            OperationInvalidException.Require(column >= range.Start.Column && column <= range.End.Column,
                $"sort column '{key.Column}' is outside the range '{Range}'");
        }

        return this;
    }
}

/// <summary>One sort key of a sort_range operation.</summary>
public sealed record SortKey
{
    [A1Column] public required string Column { get; init; }

    [AllowedValues(typeof(SortOrders))] public string Order { get; init; } = SortOrders.Asc;
}

/// <summary>Accepted values of <see cref="SortKey.Order"/>.</summary>
public static class SortOrders
{
    public const string Asc = "asc";
    public const string Desc = "desc";
}

/// <summary>
/// Applies data validation, a dropdown list or an input constraint, to a range. A list takes
/// exactly one of listItems and listSource; custom takes its formula in value1; the other types
/// take an operator and value1, and value2 too for between and notBetween.
/// </summary>
[Operation("set_validation")]
public sealed record SetValidationOp : Op
{
    /// <summary>The range to validate, such as B2:B100.</summary>
    [A1Range] public required string Range { get; init; }

    [AllowedValues(typeof(ValidationTypes))] public required string Type { get; init; }

    [AllowedValues(typeof(ValidationOperators))] public string? Operator { get; init; }

    /// <summary>The first bound (the lower bound for between), or the formula of a custom validation.</summary>
    [Pattern(@"\S")] public string? Value1 { get; init; }

    /// <summary>The upper bound for between and notBetween.</summary>
    [Pattern(@"\S")] public string? Value2 { get; init; }

    /// <summary>
    /// The dropdown items of a list. No item contains a comma or a double quote, and joined with
    /// commas they stay within Excel's 255 characters; use listSource for longer lists.
    /// </summary>
    [MinItems(1), Pattern("^[^,\"]+$")] public IReadOnlyList<string>? ListItems { get; init; }

    /// <summary>The range a list reads its items from, such as Lists!A1:A20.</summary>
    [Pattern(@"\S")] public string? ListSource { get; init; }

    /// <summary>The prompt shown when a validated cell is selected.</summary>
    public string? InputMessage { get; init; }

    /// <summary>The message shown when a value is rejected.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether a validated cell may be left blank.</summary>
    public bool AllowBlank { get; init; } = true;

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        switch (Type)
        {
            case ValidationTypes.List:
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
            case ValidationTypes.Custom:
                OperationInvalidException.Require(Value1 is not null, "a 'custom' validation needs 'value1' set to a formula");
                break;
            default:
                OperationInvalidException.Require(Operator is not null, "'operator' is required for this validation type");
                OperationInvalidException.Require(Value1 is not null, "'value1' is required");
                OperationInvalidException.Require(Operator is not (ValidationOperators.Between or ValidationOperators.NotBetween) || Value2 is not null,
                    "'between'/'notBetween' need 'value2' as the upper bound");
                break;
        }

        return this;
    }
}

/// <summary>Removes all data validation whose area overlaps a range.</summary>
[Operation("clear_validation")]
public sealed record ClearValidationOp : Op
{
    /// <summary>The range to clear validation from, such as B2:B100.</summary>
    [A1Range] public required string Range { get; init; }
}

/// <summary>Removes duplicate rows from a range, comparing every column or the listed ones.</summary>
[Operation("remove_duplicates")]
public sealed record RemoveDuplicatesOp : Op
{
    /// <summary>The range to de-duplicate, including any header row, such as A1:D100.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>The columns inside the range that define a duplicate; every column when omitted.</summary>
    [MinItems(1), A1Column] public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>Whether the first row is a header that is kept.</summary>
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

/// <summary>Accepted values of <see cref="SetValidationOp.Type"/>.</summary>
public static class ValidationTypes
{
    public const string List = "list";
    public const string WholeNumber = "wholeNumber";
    public const string Decimal = "decimal";
    public const string Date = "date";
    public const string TextLength = "textLength";
    public const string Custom = "custom";
}

/// <summary>Accepted values of <see cref="SetValidationOp.Operator"/>.</summary>
public static class ValidationOperators
{
    public const string Between = "between";
    public const string NotBetween = "notBetween";
    public const string Equal = "equal";
    public const string NotEqual = "notEqual";
    public const string GreaterThan = "greaterThan";
    public const string LessThan = "lessThan";
    public const string GreaterOrEqual = "greaterOrEqual";
    public const string LessOrEqual = "lessOrEqual";
}
