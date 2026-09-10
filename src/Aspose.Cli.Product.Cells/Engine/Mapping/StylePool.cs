using System.Drawing;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Deduplicating pool of unified styles for one read projection. Cells
/// reference pool entries by id (<c>s0</c>, <c>s1</c>, ...) so repeated
/// formatting never bloats the payload; cells carrying the workbook default
/// style get no id at all.
/// </summary>
internal sealed class StylePool
{
    private readonly Dictionary<StyleData, string> _idsByStyle = [];
    private readonly Dictionary<string, StyleData> _stylesById = [];
    private readonly StyleData _workbookDefault;

    public StylePool(Workbook workbook) =>
        _workbookDefault = Map(workbook.DefaultStyle);

    /// <summary>Returns the pool id for a cell's style, or null for the default style.</summary>
    public string? GetId(Cell? cell)
    {
        if (cell is null)
        {
            return null;
        }

        StyleData mapped = Map(cell.GetStyle());
        if (mapped == _workbookDefault)
        {
            return null;
        }

        if (_idsByStyle.TryGetValue(mapped, out string? existing))
        {
            return existing;
        }

        // Insertion order doubles as id order, keeping output deterministic.
        string id = "s" + _idsByStyle.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _idsByStyle.Add(mapped, id);
        _stylesById.Add(id, mapped);
        return id;
    }

    /// <summary>The pool content, or null when no non-default style was seen.</summary>
    public IReadOnlyDictionary<string, StyleData>? ToDictionary() =>
        _stylesById.Count == 0 ? null : _stylesById;

    /// <summary>Maps an engine style onto the unified style core.</summary>
    internal static StyleData Map(Style style) => new()
    {
        Font = style.Font.Name,
        Size = style.Font.Size,
        Bold = style.Font.IsBold ? true : null,
        Italic = style.Font.IsItalic ? true : null,
        Color = ToHex(style.Font.Color),
        Bg = style.Pattern == BackgroundType.None ? null : ToHex(style.ForegroundColor),
        NumberFormat = NumberFormatOf(style),
        HAlign = style.HorizontalAlignment switch
        {
            TextAlignmentType.Left => "left",
            TextAlignmentType.Center => "center",
            TextAlignmentType.Right => "right",
            _ => null,
        },
        VAlign = style.VerticalAlignment switch
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
    };

    private static string? NumberFormatOf(Style style)
    {
        // InvariantCustom resolves built-in format ids to their format string;
        // "General" is the default and carries no information.
        string format = style.InvariantCustom;
        return string.IsNullOrEmpty(format) || format == "General" ? null : format;
    }

    private static string? ToHex(Color color) =>
        color.IsEmpty || color.A == 0
            ? null
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
