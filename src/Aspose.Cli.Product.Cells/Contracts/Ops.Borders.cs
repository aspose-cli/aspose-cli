namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that draw cell borders.

/// <summary>
/// Draws borders on a range. <c>edges</c> names which boundaries to draw —
/// outer edges, inner grid lines, or <c>all</c> for the full grid — and every
/// named edge gets the same line style and color. Existing cell styling
/// (fills, fonts, number formats and borders not named) is preserved.
/// </summary>
public sealed record SetBordersOp() : Op
{
    /// <summary>The range to border, e.g. <c>B2:D10</c>.</summary>
    public required string Range { get; init; }

    /// <summary>The edges to draw; values of <see cref="BorderEdges"/>.</summary>
    public required IReadOnlyList<string> Edges { get; init; }

    /// <summary>Line style, one of <see cref="BorderLineStyles"/>; <c>thin</c> when omitted.</summary>
    public string? Style { get; init; }

    /// <summary>Line color as <c>#RRGGBB</c>; black when omitted.</summary>
    public string? Color { get; init; }
}

/// <summary>Accepted values of <see cref="SetBordersOp.Edges"/>.</summary>
public static class BorderEdges
{
    /// <summary>The four outer edges of the range.</summary>
    public const string Outline = "outline";

    /// <summary>The inner grid lines between cells, in both directions.</summary>
    public const string Inside = "inside";

    /// <summary>The top edge of the range.</summary>
    public const string Top = "top";

    /// <summary>The bottom edge of the range.</summary>
    public const string Bottom = "bottom";

    /// <summary>The left edge of the range.</summary>
    public const string Left = "left";

    /// <summary>The right edge of the range.</summary>
    public const string Right = "right";

    /// <summary>The inner horizontal lines between rows.</summary>
    public const string Horizontal = "horizontal";

    /// <summary>The inner vertical lines between columns.</summary>
    public const string Vertical = "vertical";

    /// <summary>The full grid: the outline plus the inner lines.</summary>
    public const string Everything = "all";

    /// <summary>Every edge token, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Outline, Inside, Top, Bottom, Left, Right, Horizontal, Vertical, Everything];
}

/// <summary>Accepted values of <see cref="SetBordersOp.Style"/>.</summary>
public static class BorderLineStyles
{
    public const string Hair = "hair";
    public const string Thin = "thin";
    public const string Medium = "medium";
    public const string Thick = "thick";
    public const string Double = "double";
    public const string Dashed = "dashed";
    public const string Dotted = "dotted";

    /// <summary>Every line style, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Hair, Thin, Medium, Thick, Double, Dashed, Dotted];
}
