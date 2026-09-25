using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Cell formatting. A read reports the fields a cell's style sets; an edit changes only the
/// fields it sets and preserves the rest, and must set at least one.
/// </summary>
[MinProperties(1)]
public sealed record StyleData
{
    /// <summary>The font family name.</summary>
    [Pattern(@"\S")] public string? Font { get; init; }

    /// <summary>The font size in points; fractional sizes such as 10.5 are kept.</summary>
    [Minimum(1), Maximum(409)] public double? Size { get; init; }

    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    /// <summary>The text color.</summary>
    [HexColor] public string? Color { get; init; }

    /// <summary>The solid background fill color.</summary>
    [HexColor] public string? Bg { get; init; }

    /// <summary>The number format code, such as 0.0%.</summary>
    [MinLength(1)] public string? NumberFormat { get; init; }

    [AllowedValues(typeof(HorizontalAlignments))] public string? HAlign { get; init; }

    [AllowedValues(typeof(VerticalAlignments))] public string? VAlign { get; init; }

    /// <summary>Whether text wraps within the cell.</summary>
    public bool? Wrap { get; init; }

    /// <summary>Whether the text has a single underline.</summary>
    public bool? Underline { get; init; }

    public bool? Strikethrough { get; init; }

    /// <summary>The indent level; 0 removes the indent.</summary>
    [Minimum(0), Maximum(250)] public int? Indent { get; init; }
}

/// <summary>
/// The differential style a conditional format gives matching cells. Excel applies only font
/// emphasis, colors and the number format there; format_range sets fonts, sizes, alignment,
/// wrapping and indents. It sets at least one field.
/// </summary>
[MinProperties(1)]
public sealed record ConditionalStyle
{
    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    /// <summary>Whether the text has a single underline.</summary>
    public bool? Underline { get; init; }

    public bool? Strikethrough { get; init; }

    /// <summary>The text color.</summary>
    [HexColor] public string? Color { get; init; }

    /// <summary>The solid background fill color.</summary>
    [HexColor] public string? Bg { get; init; }

    /// <summary>The number format code, such as 0.0%.</summary>
    [MinLength(1)] public string? NumberFormat { get; init; }

    /// <summary>The same fields as a cell style.</summary>
    internal StyleData ToStyleData() => new()
    {
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        Strikethrough = Strikethrough,
        Color = Color,
        Bg = Bg,
        NumberFormat = NumberFormat,
    };
}

/// <summary>Accepted values of <see cref="StyleData.HAlign"/>.</summary>
public static class HorizontalAlignments
{
    public const string Left = "left";
    public const string Center = "center";
    public const string Right = "right";
}

/// <summary>Accepted values of <see cref="StyleData.VAlign"/>.</summary>
public static class VerticalAlignments
{
    public const string Top = "top";
    public const string Middle = "middle";
    public const string Bottom = "bottom";
}
