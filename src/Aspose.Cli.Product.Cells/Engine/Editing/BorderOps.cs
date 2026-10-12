using System.Drawing;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// The border op. Edge tokens expand to the six primitive boundaries (four
/// outer, two inner) and each is painted without disturbing any other styling:
/// outer edges via <c>Range.SetOutlineBorder</c>, inner lines via a
/// border-only flagged <c>ApplyStyle</c> — safe even though a disk-opened
/// workbook's <c>CreateStyle</c> comes back pre-set (see
/// <see cref="StyleWriter.Build"/>), because the flag masks every non-border
/// field. No per-cell loop anywhere; a 10,000-row grid is a handful of range
/// calls.
/// </summary>
internal static class BorderOps
{
    public static void SetBorders(Workbook workbook, Worksheet sheet, SetBordersOp op)
    {
        RangeRef range = A1.ParseRange(op.Range).Range;
        CellBorderType line = ToLineType(op.LineStyle);
        Color color = StyleWriter.ParseHex(op.Color);

        (bool top, bool bottom, bool left, bool right, bool horizontal, bool vertical) = Expand(op.Edges);

        if (top && bottom && left && right && horizontal && vertical)
        {
            // Full grid: all four borders of every cell in one flagged
            // ApplyStyle — the outer edges and inner lines land together.
            (Style style, StyleFlag flag) = BuildBorderStyle(workbook,
                BorderType.TopBorder | BorderType.BottomBorder | BorderType.LeftBorder | BorderType.RightBorder,
                line, color);
            SubRange(sheet, range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount)
                .ApplyStyle(style, flag);
            return;
        }

        Aspose.Cells.Range target = SubRange(sheet, range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount);
        if (top)
        {
            target.SetOutlineBorder(BorderType.TopBorder, line, color);
        }

        if (bottom)
        {
            target.SetOutlineBorder(BorderType.BottomBorder, line, color);
        }

        if (left)
        {
            target.SetOutlineBorder(BorderType.LeftBorder, line, color);
        }

        if (right)
        {
            target.SetOutlineBorder(BorderType.RightBorder, line, color);
        }

        // Inner lines: the bottom border of every row but the last draws the
        // horizontal boundaries, the right border of every column but the last
        // the vertical ones. A single row/column has no inner boundary — the
        // sub-range would be empty, so the edge is skipped.
        if (horizontal && range.RowCount > 1)
        {
            (Style style, StyleFlag flag) = BuildBorderStyle(workbook, BorderType.BottomBorder, line, color);
            SubRange(sheet, range.Start.Row, range.Start.Column, range.RowCount - 1, range.ColumnCount)
                .ApplyStyle(style, flag);
        }

        if (vertical && range.ColumnCount > 1)
        {
            (Style style, StyleFlag flag) = BuildBorderStyle(workbook, BorderType.RightBorder, line, color);
            SubRange(sheet, range.Start.Row, range.Start.Column, range.RowCount, range.ColumnCount - 1)
                .ApplyStyle(style, flag);
        }
    }

    /// <summary>Expands the edge tokens into the six primitive boundaries.</summary>
    private static (bool Top, bool Bottom, bool Left, bool Right, bool Horizontal, bool Vertical) Expand(
        IReadOnlyList<string> edges)
    {
        bool top = false, bottom = false, left = false, right = false, horizontal = false, vertical = false;
        foreach (string edge in edges)
        {
            switch (edge)
            {
                case CellsBorderEdges.Outline:
                    top = bottom = left = right = true;
                    break;
                case CellsBorderEdges.Inside:
                    horizontal = vertical = true;
                    break;
                case CellsBorderEdges.Top:
                    top = true;
                    break;
                case CellsBorderEdges.Bottom:
                    bottom = true;
                    break;
                case CellsBorderEdges.Left:
                    left = true;
                    break;
                case CellsBorderEdges.Right:
                    right = true;
                    break;
                case CellsBorderEdges.Horizontal:
                    horizontal = true;
                    break;
                case CellsBorderEdges.Vertical:
                    vertical = true;
                    break;
                case CellsBorderEdges.Everything:
                    top = bottom = left = right = horizontal = vertical = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(edges), edge, "Border edge is missing from the engine mapper.");
            }
        }

        return (top, bottom, left, right, horizontal, vertical);
    }

    /// <summary>
    /// A style carrying only the requested borders plus the flag that limits
    /// the write to exactly those border fields.
    /// </summary>
    private static (Style Style, StyleFlag Flag) BuildBorderStyle(
        Workbook workbook, BorderType borders, CellBorderType line, Color color)
    {
        Style style = workbook.CreateStyle();
        style.SetBorder(borders, line, color);
        var flag = new StyleFlag
        {
            TopBorder = borders.HasFlag(BorderType.TopBorder),
            BottomBorder = borders.HasFlag(BorderType.BottomBorder),
            LeftBorder = borders.HasFlag(BorderType.LeftBorder),
            RightBorder = borders.HasFlag(BorderType.RightBorder),
        };
        return (style, flag);
    }

    private static Aspose.Cells.Range SubRange(Worksheet sheet, int row, int column, int rows, int columns) =>
        sheet.Cells.CreateRange(row, column, rows, columns);

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
