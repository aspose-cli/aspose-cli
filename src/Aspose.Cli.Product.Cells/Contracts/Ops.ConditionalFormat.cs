namespace Aspose.Cli.Product.Cells.Contracts;

// Conditional formatting: highlight cells by value, colour scale, data bar or duplicates.

/// <summary>Adds a conditional format over a range.</summary>
public sealed record AddConditionalFormatOp() : Op
{
    /// <summary>The range to format, e.g. <c>B2:B100</c>.</summary>
    public required string Range { get; init; }

    /// <summary>The rule deciding when and how cells are highlighted.</summary>
    public required ConditionalRule Rule { get; init; }

    /// <summary>Style applied when a <c>cellValue</c> or <c>duplicates</c> rule matches.</summary>
    public StyleData? Style { get; init; }
}

/// <summary>A conditional-formatting rule; the fields used depend on <see cref="Kind"/>.</summary>
public sealed record ConditionalRule
{
    /// <summary>Rule kind; one of <see cref="ConditionalRuleKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Comparison operator for <c>cellValue</c>; one of <see cref="ValidationOperators"/>.</summary>
    public string? Operator { get; init; }

    /// <summary>First value/formula for <c>cellValue</c>; the lower bound for <c>between</c>.</summary>
    public string? Value1 { get; init; }

    /// <summary>Upper bound for <c>between</c>/<c>notBetween</c> (<c>cellValue</c>).</summary>
    public string? Value2 { get; init; }

    /// <summary>Low-end colour for <c>colorScale</c>, e.g. <c>#F8696B</c>.</summary>
    public string? MinColor { get; init; }

    /// <summary>Mid colour for a 3-point <c>colorScale</c>; omit for a 2-point scale.</summary>
    public string? MidColor { get; init; }

    /// <summary>High-end colour for <c>colorScale</c>.</summary>
    public string? MaxColor { get; init; }

    /// <summary>Bar colour for <c>dataBar</c>.</summary>
    public string? BarColor { get; init; }

    /// <summary>Rank for <c>topBottom</c>: top/bottom N, or N percent.</summary>
    public int? Rank { get; init; }

    /// <summary>Interpret <see cref="Rank"/> as a percentage of the range (<c>topBottom</c>).</summary>
    public bool? Percent { get; init; }

    /// <summary>Highlight the bottom rather than the top (<c>topBottom</c>).</summary>
    public bool? Bottom { get; init; }

    /// <summary>Icon set for <c>iconSet</c>; one of <see cref="IconSetNames"/>.</summary>
    public string? IconSet { get; init; }
}

/// <summary>Accepted values of <see cref="ConditionalRule.Kind"/>.</summary>
public static class ConditionalRuleKinds
{
    public const string CellValue = "cellValue";
    public const string ColorScale = "colorScale";
    public const string DataBar = "dataBar";
    public const string Duplicates = "duplicates";

    /// <summary>
    /// Formats each cell where an <c>=</c>-led formula (<c>value1</c>) is true.
    /// The formula anchors at the range's top-left cell and shifts per cell,
    /// so <c>$</c>-anchored columns express whole-row highlighting.
    /// </summary>
    public const string Formula = "formula";

    /// <summary>Highlights the top or bottom N (or N percent) of the range.</summary>
    public const string TopBottom = "topBottom";

    /// <summary>Draws a per-cell icon from a set; thresholds are automatic.</summary>
    public const string IconSet = "iconSet";

    /// <summary>Every rule kind, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [CellValue, ColorScale, DataBar, Duplicates, Formula, TopBottom, IconSet];
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

    /// <summary>Every icon set, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Arrows3, TrafficLights3, Symbols3, Rating4, Rating5];
}

/// <summary>Removes all conditional formatting overlapping a range.</summary>
public sealed record ClearConditionalFormatsOp() : Op
{
    /// <summary>The range to clear conditional formatting from.</summary>
    public required string Range { get; init; }
}
