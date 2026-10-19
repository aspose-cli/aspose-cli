using System.Drawing;
using System.Globalization;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Translates the unified style core into an engine style plus the matching
/// <see cref="StyleFlag"/>, so <c>format_range</c> touches exactly the fields
/// the caller set and preserves everything else.
/// </summary>
internal static class StyleWriter
{
    /// <summary>
    /// Builds a standalone style for <c>format_range</c>, where the companion
    /// <see cref="StyleFlag"/> is what limits the write to the requested fields.
    /// Callers that need a truly differential style (conditional formatting,
    /// whose dxf must carry ONLY the requested fields) must instead
    /// <see cref="Apply"/> onto a style the engine hands them: on a workbook
    /// opened from disk — always, for this CLI — <c>CreateStyle</c> returns a
    /// style whose font, number format and alignment already count as set.
    /// </summary>
    public static (Style Style, StyleFlag Flag) Build(Workbook workbook, CellsStyleData data)
    {
        Style style = workbook.CreateStyle();
        return (style, Apply(style, data));
    }

    /// <summary>
    /// Applies the style core onto an existing engine style, reporting which
    /// fields were touched. The style is mutated in place.
    /// </summary>
    public static StyleFlag Apply(Style style, CellsStyleData data)
    {
        var flag = new StyleFlag();

        if (data.Font is { } font)
        {
            style.Font.Name = font;
            flag.FontName = true;
        }

        if (data.FontSize is { } size)
        {
            style.Font.DoubleSize = size;
            flag.FontSize = true;
        }

        if (data.Bold is { } bold)
        {
            style.Font.IsBold = bold;
            flag.FontBold = true;
        }

        if (data.Italic is { } italic)
        {
            style.Font.IsItalic = italic;
            flag.FontItalic = true;
        }

        if (data.Color is { } color)
        {
            style.Font.Color = ParseHex(color);
            flag.FontColor = true;
        }

        if (data.FillColor is { } background)
        {
            style.ForegroundColor = ParseHex(background);
            style.Pattern = BackgroundType.Solid;
            flag.CellShading = true;
        }

        if (data.NumberFormat is { } numberFormat)
        {
            style.Custom = numberFormat;
            flag.NumberFormat = true;
        }

        if (data.Alignment is { } horizontal)
        {
            style.HorizontalAlignment = horizontal switch
            {
                CellsHorizontalAlignments.Left => TextAlignmentType.Left,
                CellsHorizontalAlignments.Center => TextAlignmentType.Center,
                CellsHorizontalAlignments.Right => TextAlignmentType.Right,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(data), horizontal, "Horizontal alignment is missing from the engine mapper."),
            };
            flag.HorizontalAlignment = true;
        }

        if (data.VerticalAlignment is { } vertical)
        {
            style.VerticalAlignment = vertical switch
            {
                CellsVerticalAlignments.Top => TextAlignmentType.Top,
                CellsVerticalAlignments.Middle => TextAlignmentType.Center,
                CellsVerticalAlignments.Bottom => TextAlignmentType.Bottom,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(data), vertical, "Vertical alignment is missing from the engine mapper."),
            };
            flag.VerticalAlignment = true;
        }

        if (data.Wrap is { } wrap)
        {
            style.IsTextWrapped = wrap;
            flag.WrapText = true;
        }

        if (data.Underline is { } underline)
        {
            style.Font.Underline = underline ? FontUnderlineType.Single : FontUnderlineType.None;
            flag.FontUnderline = true;
        }

        if (data.Strikethrough is { } strikethrough)
        {
            style.Font.IsStrikeout = strikethrough;
            flag.FontStrike = true;
        }

        if (data.Indent is { } indent)
        {
            style.IndentLevel = indent;
            flag.Indent = true;
        }

        if (data.Locked is { } locked)
        {
            style.IsLocked = locked;
            flag.Locked = true;
        }

        foreach (CellsBorderData border in data.Borders ?? [])
        {
            BorderType side = BorderVocabulary.ToBorderType(border.Side);
            style.SetBorder(side, BorderVocabulary.ToLineType(border.LineStyle), ParseHex(border.Color));
            flag.TopBorder |= side == BorderType.TopBorder;
            flag.BottomBorder |= side == BorderType.BottomBorder;
            flag.LeftBorder |= side == BorderType.LeftBorder;
            flag.RightBorder |= side == BorderType.RightBorder;
        }

        return flag;
    }

    /// <summary>Parses <c>#RRGGBB</c>; the ops parser guarantees the shape.</summary>
    internal static Color ParseHex(string hex) => Color.FromArgb(
        int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
