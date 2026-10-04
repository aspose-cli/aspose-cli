using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Conditional formatting: highlight cells by value, colour scale, data bar or duplicates.

/// <summary>
/// Adds a conditional format over a range. Each rule kind needs its own fields: cellValue an
/// operator and value1 (and value2 for between and notBetween); colorScale minColor and
/// maxColor; dataBar barColor; formula value1, to which a missing leading = is added; topBottom
/// rank, at most 100 with percent; iconSet iconSet. cellValue, duplicates, formula and
/// topBottom need a style; iconSet takes none.
/// </summary>
[Operation("add_conditional_format")]
public sealed record AddConditionalFormatOp : CellsOp
{
    /// <summary>The range to format, such as B2:B100.</summary>
    [A1Range] public required string Range { get; init; }

    public required ConditionalRule Rule { get; init; }

    /// <summary>The style matching cells receive.</summary>
    public ConditionalStyle? Style { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        ConditionalRule rule = Rule;
        bool styled = rule.Kind != ConditionalRuleKinds.IconSet;
        switch (rule.Kind)
        {
            case ConditionalRuleKinds.CellValue:
                Require(rule.Operator is not null, "a 'cellValue' rule needs 'operator'");
                Require(rule.Value1 is not null, "a 'cellValue' rule needs 'value1'");
                Require(rule.Operator is not (ValidationOperators.Between or ValidationOperators.NotBetween) || rule.Value2 is not null,
                    "'between'/'notBetween' need 'value2'");
                break;
            case ConditionalRuleKinds.ColorScale:
                Require(rule.MinColor is not null && rule.MaxColor is not null,
                    "a 'colorScale' rule needs 'minColor' and 'maxColor' (add 'midColor' for a 3-point scale)");
                styled = false;
                break;
            case ConditionalRuleKinds.DataBar:
                Require(rule.BarColor is not null, "a 'dataBar' rule needs 'barColor'");
                styled = false;
                break;
            case ConditionalRuleKinds.Formula:
                Require(rule.Value1 is not null,
                    "a 'formula' rule needs 'value1' set to a formula",
                    "The formula anchors at the range's top-left cell and shifts per cell; anchor the tested "
                    + "column with '$' to highlight whole rows, e.g. =$F2=\"OVERDUE\" over A2:F100.");
                break;
            case ConditionalRuleKinds.TopBottom:
                Require(rule.Rank is not null && (!rule.Percent || rule.Rank <= 100),
                    "a 'topBottom' rule needs 'rank' (1-1000; 1-100 with 'percent')");
                break;
        }

        if (styled)
        {
            Require(Style is not null, $"a '{rule.Kind}' rule needs a 'style' to apply");
        }
        else if (rule.Kind == ConditionalRuleKinds.IconSet)
        {
            // Icons come from the set; a style would be dead weight the caller believes took effect.
            Require(Style is null, "an 'iconSet' rule draws icons; omit 'style'");
            Require(rule.IconSet is not null, "an 'iconSet' rule needs 'iconSet'");
        }

        // The engine stores a formula without the leading '=' as a string literal that never
        // matches (probe-verified), so the prefix is added rather than the rule rejected. A
        // cellValue comparison is a value, or a formula after '='; like Excel's dialog, text
        // quoted as an Excel string literal ("Done") is that literal's formula.
        return rule.Kind switch
        {
            ConditionalRuleKinds.Formula when !rule.Value1!.StartsWith('=') =>
                this with { Rule = rule with { Value1 = "=" + rule.Value1 } },
            ConditionalRuleKinds.CellValue =>
                this with { Rule = rule with { Value1 = Comparison(rule.Value1), Value2 = Comparison(rule.Value2) } },
            _ => this,
        };

        static string? Comparison(string? value) => value is ['"', .., '"'] ? "=" + value : value;

        static void Require(bool condition, string reason, string? hint = null) =>
            OperationInvalidException.Require(condition, reason, hint);
    }
}

/// <summary>A conditional-formatting rule; the fields it uses depend on its kind.</summary>
public sealed record ConditionalRule
{
    /// <summary>
    /// cellValue compares values; colorScale and dataBar shade by value; duplicates marks
    /// repeated values; formula formats where a formula is true, anchored at the range's top-left
    /// cell and shifted per cell; topBottom marks the top or bottom N or N percent; iconSet draws
    /// icons with automatic thresholds.
    /// </summary>
    [AllowedValues(typeof(ConditionalRuleKinds))] public required string Kind { get; init; }

    /// <summary>The comparison of a cellValue rule.</summary>
    [AllowedValues(typeof(ValidationOperators))] public string? Operator { get; init; }

    /// <summary>
    /// The comparison value of a cellValue rule (the lower bound for between): a number, text as
    /// it is (Done) or quoted as in Excel ("Done"), or a formula after = (=$F$1). For a formula
    /// rule, the formula, which anchors at the range's top-left cell and shifts per cell ($ parts
    /// stay fixed).
    /// </summary>
    [Pattern(@"\S")] public string? Value1 { get; init; }

    /// <summary>The upper bound of a between or notBetween cellValue rule, written as value1 is.</summary>
    [Pattern(@"\S")] public string? Value2 { get; init; }

    /// <summary>The low-end color of a colorScale.</summary>
    [HexColor] public string? MinColor { get; init; }

    /// <summary>The middle color of a 3-point colorScale; a 2-point scale omits it.</summary>
    [HexColor] public string? MidColor { get; init; }

    /// <summary>The high-end color of a colorScale.</summary>
    [HexColor] public string? MaxColor { get; init; }

    /// <summary>The bar color of a dataBar.</summary>
    [HexColor] public string? BarColor { get; init; }

    /// <summary>The N of a topBottom rule: the top or bottom N values, or N percent.</summary>
    [Minimum(1), Maximum(1000)] public int? Rank { get; init; }

    /// <summary>Whether a topBottom rank is a percentage of the range.</summary>
    public bool Percent { get; init; }

    /// <summary>Whether a topBottom rule marks the bottom rather than the top.</summary>
    public bool Bottom { get; init; }

    /// <summary>The icons of an iconSet rule.</summary>
    [AllowedValues(typeof(IconSetNames))] public string? IconSet { get; init; }
}

/// <summary>Accepted values of <see cref="ConditionalRule.Kind"/>.</summary>
public static class ConditionalRuleKinds
{
    public const string CellValue = "cellValue";
    public const string ColorScale = "colorScale";
    public const string DataBar = "dataBar";
    public const string Duplicates = "duplicates";
    public const string Formula = "formula";
    public const string TopBottom = "topBottom";
    public const string IconSet = "iconSet";
}

/// <summary>Accepted values of <see cref="ConditionalRule.IconSet"/>.</summary>
public static class IconSetNames
{
    /// <summary>Three colored arrows: up, sideways, down.</summary>
    public const string Arrows3 = "arrows3";

    /// <summary>Three traffic lights: green, yellow, red.</summary>
    public const string TrafficLights3 = "trafficLights3";

    /// <summary>Three symbols: check, exclamation, cross.</summary>
    public const string Symbols3 = "symbols3";

    /// <summary>Four-bar signal-strength rating.</summary>
    public const string Rating4 = "rating4";

    /// <summary>Five-bar signal-strength rating.</summary>
    public const string Rating5 = "rating5";
}

/// <summary>Removes all conditional formatting that overlaps a range.</summary>
[Operation("clear_conditional_formats")]
public sealed record ClearConditionalFormatsOp : CellsOp
{
    /// <summary>The range to clear conditional formatting from.</summary>
    [A1Range] public required string Range { get; init; }
}
