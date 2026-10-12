using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Cell formatting. A read reports the fields a cell's style sets; an edit changes only the
/// fields it sets and preserves the rest, and must set at least one.
/// </summary>
[MinProperties(1)]
public sealed record CellsStyleData
{
    /// <summary>The font family name.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/font")]
    [Pattern(@"\S")] public string? Font { get; init; }

    /// <summary>The font size in points; fractional sizes such as 10.5 are kept.</summary>
    [MistakenFor("size")]
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/fontSize")]
    [Minimum(1), Maximum(409)] public double? FontSize { get; init; }

    /// <summary>Whether the text is bold.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/bold")]
    public bool? Bold { get; init; }

    /// <summary>Whether the text is italic.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/italic")]
    public bool? Italic { get; init; }

    /// <summary>The text color.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/color")]
    [HexColor] public string? Color { get; init; }

    /// <summary>The solid background fill color.</summary>
    [MistakenFor("bg", "backgroundColor", "bgColor", "background", "fill")]
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/fillColor")]
    [HexColor] public string? FillColor { get; init; }

    /// <summary>The number format code, such as 0.0%.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/numberFormat")]
    [MinLength(1)] public string? NumberFormat { get; init; }

    /// <summary>The horizontal alignment of the text.</summary>
    [MistakenFor("hAlign", "horizontalAlignment", "align", "textAlign")]
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/alignment")]
    [AllowedValues(typeof(CellsHorizontalAlignments))] public string? Alignment { get; init; }

    /// <summary>The vertical alignment of the text.</summary>
    [MistakenFor("vAlign", "valign")]
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/verticalAlignment")]
    [AllowedValues(typeof(CellsVerticalAlignments))] public string? VerticalAlignment { get; init; }

    /// <summary>Whether text wraps within the cell.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/wrap")]
    public bool? Wrap { get; init; }

    /// <summary>Whether the text has a single underline.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/underline")]
    public bool? Underline { get; init; }

    /// <summary>Whether the text is struck through.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/strikethrough")]
    public bool? Strikethrough { get; init; }

    /// <summary>The indent level; 0 removes the indent.</summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/indent")]
    [Minimum(0), Maximum(250)] public int? Indent { get; init; }

    /// <summary>
    /// Whether Excel keeps the cell read-only once its sheet is protected. Every cell is locked
    /// unless set false, as for the input cells of a protected form; a read reports only false.
    /// </summary>
    [ReadBack("read-result.schema.json#/$defs/cellsStyleData/properties/locked")]
    public bool? Locked { get; init; }
}
