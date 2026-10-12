using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The differential style a conditional format gives matching cells. Excel applies only font
/// emphasis, colors and the number format there; format_range sets fonts, sizes, alignment,
/// wrapping and indents. It sets at least one field.
/// </summary>
[MinProperties(1)]
public sealed record CellsConditionalStyle
{
    /// <summary>Whether the text is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Whether the text is italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>Whether the text has a single underline.</summary>
    public bool? Underline { get; init; }

    /// <summary>Whether the text is struck through.</summary>
    public bool? Strikethrough { get; init; }

    /// <summary>The text color.</summary>
    [HexColor] public string? Color { get; init; }

    /// <summary>The solid background fill color.</summary>
    [MistakenFor("bg", "backgroundColor", "bgColor", "background", "fill")]
    [HexColor] public string? FillColor { get; init; }

    /// <summary>The number format code, such as 0.0%.</summary>
    [MinLength(1)] public string? NumberFormat { get; init; }

    /// <summary>The same fields as a cell style.</summary>
    internal CellsStyleData ToStyleData() => new()
    {
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        Strikethrough = Strikethrough,
        Color = Color,
        FillColor = FillColor,
        NumberFormat = NumberFormat,
    };
}
