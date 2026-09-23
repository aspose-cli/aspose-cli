namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for the data-cleaning pipeline: filtering, sorting and validation.

/// <summary>Adds or clears an AutoFilter over a range.</summary>
public sealed record SetAutoFilterOp() : Op
{
    /// <summary>The range to filter, including its header row, e.g. <c>A1:D20</c>. Required unless <see cref="Off"/>.</summary>
    public string? Range { get; init; }

    /// <summary>Remove the sheet's AutoFilter instead of adding one.</summary>
    public bool? Off { get; init; }
}

/// <summary>Sorts a range in place by one or more columns.</summary>
public sealed record SortRangeOp() : Op
{
    /// <summary>The range to sort, e.g. <c>A2:D100</c>.</summary>
    public required string Range { get; init; }

    /// <summary>Sort keys applied in order (primary first).</summary>
    public required IReadOnlyList<SortKey> By { get; init; }

    /// <summary>Treat the first row as a header and keep it in place.</summary>
    public bool? HasHeader { get; init; }
}

/// <summary>One column of a <see cref="SortRangeOp"/>.</summary>
public sealed record SortKey
{
    /// <summary>Column letter, e.g. <c>B</c>.</summary>
    public required string Column { get; init; }

    /// <summary>Sort order; one of <see cref="SortOrders"/>. Ascending when omitted.</summary>
    public string? Order { get; init; }
}

/// <summary>Accepted values of <see cref="SortKey.Order"/>.</summary>
public static class SortOrders
{
    public const string Asc = "asc";
    public const string Desc = "desc";

    /// <summary>Every order, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Asc, Desc];
}

/// <summary>
/// Applies data validation — a dropdown list or an input constraint — to a
/// range. For a <c>list</c> type give either <see cref="ListItems"/> or
/// <see cref="ListSource"/>; for numeric, date and length types give an
/// <see cref="Operator"/> and bounds; for <c>custom</c> give a formula.
/// </summary>
public sealed record SetValidationOp() : Op
{
    /// <summary>The range to validate, e.g. <c>B2:B100</c>.</summary>
    public required string Range { get; init; }

    /// <summary>Validation type; one of <see cref="ValidationTypes"/>.</summary>
    public required string Type { get; init; }

    /// <summary>Comparison operator; one of <see cref="ValidationOperators"/>. Required for numeric/date/length types.</summary>
    public string? Operator { get; init; }

    /// <summary>First bound, or the formula for the <c>custom</c> type; the lower bound for <c>between</c>.</summary>
    public string? Value1 { get; init; }

    /// <summary>Upper bound for <c>between</c>/<c>notBetween</c>.</summary>
    public string? Value2 { get; init; }

    /// <summary>Explicit dropdown items (for the <c>list</c> type).</summary>
    public IReadOnlyList<string>? ListItems { get; init; }

    /// <summary>Range the dropdown reads its items from, e.g. <c>Lists!A1:A20</c> (for the <c>list</c> type).</summary>
    public string? ListSource { get; init; }

    /// <summary>Prompt shown when a validated cell is selected.</summary>
    public string? InputMessage { get; init; }

    /// <summary>Message shown when a value is rejected.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Allow a validated cell to be left blank; true when omitted.</summary>
    public bool? AllowBlank { get; init; }
}

/// <summary>Removes all data validation whose area overlaps a range.</summary>
public sealed record ClearValidationOp() : Op
{
    /// <summary>The range to clear validation from, e.g. <c>B2:B100</c>.</summary>
    public required string Range { get; init; }
}

/// <summary>Removes duplicate rows from a range, comparing all columns or a subset.</summary>
public sealed record RemoveDuplicatesOp() : Op
{
    /// <summary>The range to de-duplicate, including any header row, e.g. <c>A1:D100</c>.</summary>
    public required string Range { get; init; }

    /// <summary>Columns (letters) that define a duplicate; all columns when omitted.</summary>
    public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>Treat the first row as a header and keep it; false when omitted.</summary>
    public bool? HasHeader { get; init; }
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

    /// <summary>Every validation type, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [List, WholeNumber, Decimal, Date, TextLength, Custom];
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

    /// <summary>Every operator, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Between, NotBetween, Equal, NotEqual, GreaterThan, LessThan, GreaterOrEqual, LessOrEqual];
}
