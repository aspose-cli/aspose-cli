using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Conditional formatting: highlight cells by value, colour scale, data bar or duplicates.

/// <summary>
/// Adds a conditional format over a range. Each rule kind needs its own fields: cell-value an
/// operator and value1 (and value2 for between and not-between); color-scale minColor and
/// maxColor; data-bar barColor; formula value1, to which a missing leading = is added; top-bottom
/// rank, at most 100 with percent; icon-set iconSet. cell-value, duplicates, formula and
/// top-bottom need a matchStyle; icon-set takes none.
/// </summary>
[Operation("add_conditional_format")]
public sealed record AddConditionalFormatOp : CellsSheetOp
{
    /// <summary>
    /// The range to format, such as B2:B100, or several separated by commas, such as
    /// B2:B10,D2:D10, as a read reports a rule over several areas: one rule formats them all.
    /// </summary>
    [A1RangeList] public required string Range { get; init; }

    /// <summary>The rule that decides which cells are formatted, or how they are shaded.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsConditionalFormatInfo/properties/rule")]
    public required CellsConditionalRule Rule { get; init; }

    /// <summary>The style matching cells receive.</summary>
    [MistakenFor("style")]
    [ReadBack("inspect-result.schema.json#/$defs/cellsConditionalFormatInfo/properties/matchStyle")]
    public CellsConditionalStyle? MatchStyle { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        CellsConditionalRule rule = Rule;
        bool styled = rule.Kind != CellsConditionalRuleKinds.IconSet;
        switch (rule.Kind)
        {
            case CellsConditionalRuleKinds.CellValue:
                Require(rule.Operator is not null, "a 'cell-value' rule needs 'operator'");
                Require(rule.Value1 is not null, "a 'cell-value' rule needs 'value1'");
                Require(rule.Operator is not (CellsValidationOperators.Between or CellsValidationOperators.NotBetween) || rule.Value2 is not null,
                    "'between'/'not-between' need 'value2'");
                break;
            case CellsConditionalRuleKinds.ColorScale:
                Require(rule.MinColor is not null && rule.MaxColor is not null,
                    "a 'color-scale' rule needs 'minColor' and 'maxColor' (add 'midColor' for a 3-point scale)");
                styled = false;
                break;
            case CellsConditionalRuleKinds.DataBar:
                Require(rule.BarColor is not null, "a 'data-bar' rule needs 'barColor'");
                styled = false;
                break;
            case CellsConditionalRuleKinds.Formula:
                Require(rule.Value1 is not null,
                    "a 'formula' rule needs 'value1' set to a formula",
                    "The formula anchors at the range's top-left cell and shifts per cell; anchor the tested "
                    + "column with '$' to highlight whole rows, e.g. =$F2=\"OVERDUE\" over A2:F100.");
                break;
            case CellsConditionalRuleKinds.TopBottom:
                Require(rule.Rank is not null && (!rule.Percent || rule.Rank <= 100),
                    "a 'top-bottom' rule needs 'rank' (1-1000; 1-100 with 'percent')");
                break;
        }

        if (styled)
        {
            Require(MatchStyle is not null, $"a '{rule.Kind}' rule needs a 'matchStyle' to apply");
        }
        else if (rule.Kind == CellsConditionalRuleKinds.IconSet)
        {
            // Icons come from the set; a style would be dead weight the caller believes took effect.
            Require(MatchStyle is null, "an 'icon-set' rule draws icons; omit 'matchStyle'");
            Require(rule.IconSet is not null, "an 'icon-set' rule needs 'iconSet'");
        }

        // The engine stores a formula without the leading '=' as a string literal that never
        // matches (probe-verified), so the prefix is added rather than the rule rejected. A
        // cell-value comparison is a value, or a formula after '='; like Excel's dialog, text
        // quoted as an Excel string literal ("Done") is that literal's formula.
        return rule.Kind switch
        {
            CellsConditionalRuleKinds.Formula when !rule.Value1!.StartsWith('=') =>
                this with { Rule = rule with { Value1 = "=" + rule.Value1 } },
            CellsConditionalRuleKinds.CellValue =>
                this with { Rule = rule with { Value1 = Comparison(rule.Value1), Value2 = Comparison(rule.Value2) } },
            _ => this,
        };

        static string? Comparison(string? value) => value is ['"', .., '"'] ? "=" + value : value;

        static void Require(bool condition, string reason, string? hint = null) =>
            OperationInvalidException.Require(condition, reason, hint);
    }
}

/// <summary>A conditional-formatting rule; the fields it uses depend on its kind.</summary>
public sealed record CellsConditionalRule
{
    /// <summary>
    /// cell-value compares values; color-scale and data-bar shade by value; duplicates marks
    /// repeated values; formula formats where a formula is true, anchored at the range's top-left
    /// cell and shifted per cell; top-bottom marks the top or bottom N or N percent; icon-set draws
    /// icons with automatic thresholds.
    /// </summary>
    [AllowedValues(typeof(CellsConditionalRuleKinds))] public required string Kind { get; init; }

    /// <summary>The comparison of a cell-value rule.</summary>
    [AllowedValues(typeof(CellsValidationOperators))] public string? Operator { get; init; }

    /// <summary>
    /// The comparison value of a cell-value rule (the lower bound for between): a number, text as
    /// it is (Done) or quoted as in Excel ("Done"), or a formula after = (=$F$1). For a formula
    /// rule, the formula, which anchors at the range's top-left cell and shifts per cell ($ parts
    /// stay fixed).
    /// </summary>
    [Pattern(@"\S")] public string? Value1 { get; init; }

    /// <summary>The upper bound of a between or not-between cell-value rule, written as value1 is.</summary>
    [Pattern(@"\S")] public string? Value2 { get; init; }

    /// <summary>The low-end color of a color-scale.</summary>
    [HexColor] public string? MinColor { get; init; }

    /// <summary>The middle color of a 3-point color-scale; a 2-point scale omits it.</summary>
    [HexColor] public string? MidColor { get; init; }

    /// <summary>The high-end color of a color-scale.</summary>
    [HexColor] public string? MaxColor { get; init; }

    /// <summary>The bar color of a data-bar.</summary>
    [HexColor] public string? BarColor { get; init; }

    /// <summary>The N of a top-bottom rule: the top or bottom N values, or N percent.</summary>
    [Minimum(1), Maximum(1000)] public int? Rank { get; init; }

    /// <summary>Whether a top-bottom rank is a percentage of the range.</summary>
    public bool Percent { get; init; }

    /// <summary>Whether a top-bottom rule marks the bottom rather than the top.</summary>
    [MistakenFor("bottom")]
    public bool FromBottom { get; init; }

    /// <summary>The icons of an icon-set rule.</summary>
    [AllowedValues(typeof(CellsIconSetNames))] public string? IconSet { get; init; }
}

/// <summary>Accepted values of <see cref="CellsConditionalRule.Kind"/>.</summary>
public static class CellsConditionalRuleKinds
{
    public const string CellValue = "cell-value";
    public const string ColorScale = "color-scale";
    public const string DataBar = "data-bar";
    public const string Duplicates = "duplicates";
    public const string Formula = "formula";
    public const string TopBottom = "top-bottom";
    public const string IconSet = "icon-set";
}

/// <summary>Accepted values of <see cref="CellsConditionalRule.IconSet"/>.</summary>
public static class CellsIconSetNames
{
    /// <summary>Three colored arrows: up, sideways, down.</summary>
    public const string Arrows3 = "arrows-3";

    /// <summary>Three traffic lights: green, yellow, red.</summary>
    public const string TrafficLights3 = "traffic-lights-3";

    /// <summary>Three symbols: check, exclamation, cross.</summary>
    public const string Symbols3 = "symbols-3";

    /// <summary>Four-bar signal-strength rating.</summary>
    public const string Rating4 = "rating-4";

    /// <summary>Five-bar signal-strength rating.</summary>
    public const string Rating5 = "rating-5";
}

/// <summary>Removes all conditional formatting that overlaps a range.</summary>
[Operation("clear_conditional_formats")]
public sealed record ClearConditionalFormatsOp : CellsSheetOp
{
    /// <summary>The range to clear conditional formatting from, or several separated by commas, such as B2:B10,D2:D10.</summary>
    [A1RangeList] public required string Range { get; init; }
}
