namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Cells-owned workbook style projection. This type intentionally lives in
/// the Cells contract slice: no other production product shares its number
/// format, fill, alignment, wrapping, or indentation invariants.
/// </summary>
public sealed record StyleData
{
    /// <summary>Font family name.</summary>
    public string? Font { get; init; }

    /// <summary>Font size in points.</summary>
    public double? Size { get; init; }

    /// <summary>Bold font weight.</summary>
    public bool? Bold { get; init; }

    /// <summary>Italic font style.</summary>
    public bool? Italic { get; init; }

    /// <summary>Text color as <c>#RRGGBB</c>.</summary>
    public string? Color { get; init; }

    /// <summary>Background fill color as <c>#RRGGBB</c>.</summary>
    public string? Bg { get; init; }

    /// <summary>Workbook number format string, e.g. <c>0.0%</c>.</summary>
    public string? NumberFormat { get; init; }

    /// <summary>Horizontal alignment: <c>left</c>, <c>center</c>, <c>right</c>.</summary>
    public string? HAlign { get; init; }

    /// <summary>Vertical alignment: <c>top</c>, <c>middle</c>, <c>bottom</c>.</summary>
    public string? VAlign { get; init; }

    /// <summary>Text wrapping enabled.</summary>
    public bool? Wrap { get; init; }

    /// <summary>Single underline.</summary>
    public bool? Underline { get; init; }

    /// <summary>Strikethrough.</summary>
    public bool? Strikethrough { get; init; }

    /// <summary>Indent level (0 removes the indent).</summary>
    public int? Indent { get; init; }
}

/// <summary>Accepted values of <see cref="StyleData.HAlign"/>.</summary>
public static class HorizontalAlignments
{
    public const string Left = "left";
    public const string Center = "center";
    public const string Right = "right";

    /// <summary>Every horizontal alignment, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Left, Center, Right];
}

/// <summary>Accepted values of <see cref="StyleData.VAlign"/>.</summary>
public static class VerticalAlignments
{
    public const string Top = "top";
    public const string Middle = "middle";
    public const string Bottom = "bottom";

    /// <summary>Every vertical alignment, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Top, Middle, Bottom];
}
