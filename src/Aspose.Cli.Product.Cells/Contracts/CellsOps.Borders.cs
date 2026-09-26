using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that draw cell borders.

/// <summary>
/// Draws borders on a range, every named edge with the same line style and color; the rest of
/// the cells' formatting is preserved. A single cell has no inner lines, so it needs an outer edge.
/// </summary>
[Operation("set_borders")]
public sealed record SetBordersOp : CellsOp
{
    /// <summary>The range to border, such as B2:D10.</summary>
    [A1Range] public required string Range { get; init; }

    /// <summary>
    /// The edges to draw: outline (the four outer edges), inside (the inner grid lines), single
    /// edges, horizontal and vertical inner lines, or all (the full grid).
    /// </summary>
    [MinItems(1), AllowedValues(typeof(BorderEdges))] public required IReadOnlyList<string> Edges { get; init; }

    [AllowedValues(typeof(BorderLineStyles))] public string Style { get; init; } = BorderLineStyles.Thin;

    [HexColor] public string Color { get; init; } = "#000000";

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        // Inner edges alone on a single cell would draw nothing, a silent no-op the caller reads as success.
        OperationInvalidException.Require(
            A1.ParseRange(Range).Range.CellCount > 1
                || Edges.Any(static edge => edge is not (BorderEdges.Inside or BorderEdges.Horizontal or BorderEdges.Vertical)),
            $"a single-cell range has no inner boundaries, so edges [{string.Join(", ", Edges)}] would draw nothing",
            "Use outline (or top/bottom/left/right) on a single cell, or a multi-cell range for inside borders.");
        return this;
    }
}

/// <summary>Accepted values of <see cref="SetBordersOp.Edges"/>.</summary>
public static class BorderEdges
{
    /// <summary>The four outer edges of the range.</summary>
    public const string Outline = "outline";

    /// <summary>The inner grid lines between cells, in both directions.</summary>
    public const string Inside = "inside";

    public const string Top = "top";
    public const string Bottom = "bottom";
    public const string Left = "left";
    public const string Right = "right";

    /// <summary>The inner horizontal lines between rows.</summary>
    public const string Horizontal = "horizontal";

    /// <summary>The inner vertical lines between columns.</summary>
    public const string Vertical = "vertical";

    /// <summary>The full grid: the outline plus the inner lines.</summary>
    public const string Everything = "all";
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
}
