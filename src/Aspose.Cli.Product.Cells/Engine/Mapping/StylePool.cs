using System.Drawing;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Deduplicating pool of unified styles for one read projection. Cells
/// reference pool entries by id (<c>s0</c>, <c>s1</c>, ...) so repeated
/// formatting never bloats the payload; cells carrying the workbook default
/// style get no id at all.
/// </summary>
internal sealed class StylePool
{
    // Keyed by the style without its borders and the borders as text: a record compares its
    // list of borders by reference.
    private readonly Dictionary<(CellsStyleData Style, string Borders), string> _idsByStyle = [];
    private readonly Dictionary<string, CellsStyleData> _stylesById = [];
    private readonly (CellsStyleData Style, string Borders) _workbookDefault;

    public StylePool(Workbook workbook) =>
        _workbookDefault = Key(Map(workbook.DefaultStyle));

    private static (CellsStyleData Style, string Borders) Key(CellsStyleData style) =>
        (style with { Borders = null },
         string.Join(";", (style.Borders ?? []).Select(static border => $"{border.Side} {border.LineStyle} {border.Color}")));

    /// <summary>Returns the pool id for a cell's style, or null for the default style.</summary>
    public string? GetId(Cell? cell)
    {
        if (cell is null)
        {
            return null;
        }

        CellsStyleData mapped = Map(cell.GetStyle());
        (CellsStyleData Style, string Borders) key = Key(mapped);
        if (key == _workbookDefault)
        {
            return null;
        }

        if (_idsByStyle.TryGetValue(key, out string? existing))
        {
            return existing;
        }

        // Insertion order doubles as id order, keeping output deterministic.
        string id = "s" + _idsByStyle.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _idsByStyle.Add(key, id);
        _stylesById.Add(id, mapped);
        return id;
    }

    /// <summary>The pool content, or null when no non-default style was seen.</summary>
    public IReadOnlyDictionary<string, CellsStyleData>? ToDictionary() =>
        _stylesById.Count == 0 ? null : _stylesById;

    /// <summary>Maps an engine style onto the unified style core.</summary>
    internal static CellsStyleData Map(Style style) => new()
    {
        Font = style.Font.Name,
        FontSize = style.Font.DoubleSize,
        Bold = style.Font.IsBold ? true : null,
        Italic = style.Font.IsItalic ? true : null,
        Color = ToHex(style.Font.Color),
        FillColor = style.Pattern == BackgroundType.None ? null : ToHex(style.ForegroundColor),
        NumberFormat = NumberFormatOf(style),
        Alignment = style.HorizontalAlignment switch
        {
            TextAlignmentType.Left => "left",
            TextAlignmentType.Center => "center",
            TextAlignmentType.Right => "right",
            _ => null,
        },
        VerticalAlignment = style.VerticalAlignment switch
        {
            TextAlignmentType.Top => "top",
            TextAlignmentType.Center => "middle",
            TextAlignmentType.Bottom => "bottom",
            _ => null,
        },
        Wrap = style.IsTextWrapped ? true : null,
        Underline = style.Font.Underline == FontUnderlineType.None ? null : true,
        Strikethrough = style.Font.IsStrikeout ? true : null,
        Indent = style.IndentLevel == 0 ? null : style.IndentLevel,
        Locked = style.IsLocked ? null : false,
        Borders = Borders(style),
    };

    /// <summary>The sides of a style that have a line, in the order top, bottom, left, right.</summary>
    private static IReadOnlyList<CellsBorderData>? Borders(Style style)
    {
        var borders = new List<CellsBorderData>(4);
        foreach (string side in (string[])[CellsBorderSides.Top, CellsBorderSides.Bottom, CellsBorderSides.Left, CellsBorderSides.Right])
        {
            Border border = style.Borders[BorderVocabulary.ToBorderType(side)];
            if (BorderVocabulary.FromLineType(border.LineStyle) is { } line)
            {
                borders.Add(new CellsBorderData { Side = side, LineStyle = line, Color = ToHex(border.Color) ?? "#000000" });
            }
        }

        return borders.Count == 0 ? null : borders;
    }

    private static string? NumberFormatOf(Style style)
    {
        // InvariantCustom resolves built-in format ids to their format string;
        // "General" is the default and carries no information.
        string format = style.InvariantCustom;
        return string.IsNullOrEmpty(format) || format == "General" ? null : format;
    }

    internal static string? ToHex(Color color) =>
        color.IsEmpty || color.A == 0
            ? null
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
