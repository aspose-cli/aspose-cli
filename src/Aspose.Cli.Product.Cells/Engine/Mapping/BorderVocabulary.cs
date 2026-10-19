using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The border sides and line styles as the engine names them, which a read reports and the
/// border and style operations write.
/// </summary>
internal static class BorderVocabulary
{
    /// <summary>The cell side of a <see cref="CellsBorderData"/>.</summary>
    internal static BorderType ToBorderType(string side) => side switch
    {
        CellsBorderSides.Top => BorderType.TopBorder,
        CellsBorderSides.Bottom => BorderType.BottomBorder,
        CellsBorderSides.Left => BorderType.LeftBorder,
        CellsBorderSides.Right => BorderType.RightBorder,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Border side is missing from the engine mapper."),
    };

    /// <summary>
    /// The line style of an engine border, null for none; the dash patterns outside the
    /// vocabulary read as dashed.
    /// </summary>
    internal static string? FromLineType(CellBorderType line) => line switch
    {
        CellBorderType.None => null,
        CellBorderType.Hair => CellsBorderLineStyles.Hair,
        CellBorderType.Thin => CellsBorderLineStyles.Thin,
        CellBorderType.Medium => CellsBorderLineStyles.Medium,
        CellBorderType.Thick => CellsBorderLineStyles.Thick,
        CellBorderType.Double => CellsBorderLineStyles.Double,
        CellBorderType.Dotted => CellsBorderLineStyles.Dotted,
        _ => CellsBorderLineStyles.Dashed,
    };

    internal static CellBorderType ToLineType(string style) => style switch
    {
        CellsBorderLineStyles.Hair => CellBorderType.Hair,
        CellsBorderLineStyles.Thin => CellBorderType.Thin,
        CellsBorderLineStyles.Medium => CellBorderType.Medium,
        CellsBorderLineStyles.Thick => CellBorderType.Thick,
        CellsBorderLineStyles.Double => CellBorderType.Double,
        CellsBorderLineStyles.Dashed => CellBorderType.Dashed,
        CellsBorderLineStyles.Dotted => CellBorderType.Dotted,
        _ => throw new ArgumentOutOfRangeException(
            nameof(style), style, "Border line style is missing from the engine mapper."),
    };
}
