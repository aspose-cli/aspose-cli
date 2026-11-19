using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cells;
using Aspose.Cli.Sdk.Tables;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The members of an Aspose.Cells <see cref="Style"/> the table names: each is a row of a cell's
/// <c>style</c> (<see cref="Cell.GetStyle()"/>, that one member set, <see cref="Cell.SetStyle(Style)"/>)
/// and a key of the style <c>apply_style</c> builds on <see cref="Workbook.CreateStyle()"/>.
/// </summary>
internal static class CellsStyles
{
    private static readonly (string Name, BorderType Type)[] Sides =
    [
        ("top", BorderType.TopBorder), ("bottom", BorderType.BottomBorder), ("left", BorderType.LeftBorder), ("right", BorderType.RightBorder),
    ];

    /// <summary>Every member, in the order a cell's style lists them.</summary>
    internal static IReadOnlyList<StyleMember> Members { get; } =
    [
        Member("font.name", ValueKinds.Text, "Style.Font.Name", static s => s.Font.Name, static (s, v) => s.Font.Name = v),
        Member("font.doubleSize", ValueKinds.Number, "Style.Font.DoubleSize, in points", static s => s.Font.DoubleSize, static (s, v) => s.Font.DoubleSize = v),
        Member("font.isBold", ValueKinds.Boolean, "Style.Font.IsBold", static s => s.Font.IsBold, static (s, v) => s.Font.IsBold = v),
        Member("font.isItalic", ValueKinds.Boolean, "Style.Font.IsItalic", static s => s.Font.IsItalic, static (s, v) => s.Font.IsItalic = v),
        Member("font.underline", ValueKinds.Enum<FontUnderlineType>(), "Style.Font.Underline", static s => s.Font.Underline, static (s, v) => s.Font.Underline = v),
        Member("font.isStrikeout", ValueKinds.Boolean, "Style.Font.IsStrikeout", static s => s.Font.IsStrikeout, static (s, v) => s.Font.IsStrikeout = v),
        Member("font.color", ValueKinds.Color, "Style.Font.Color", static s => s.Font.Color, static (s, v) => s.Font.Color = v),
        Member("foregroundColor", ValueKinds.Color, "Style.ForegroundColor: the fill's color, which shows under a Pattern other than None", static s => s.ForegroundColor, static (s, v) => s.ForegroundColor = v),
        Member("backgroundColor", ValueKinds.Color, "Style.BackgroundColor: the color of a pattern's background", static s => s.BackgroundColor, static (s, v) => s.BackgroundColor = v),
        Member("pattern", ValueKinds.Enum<BackgroundType>(), "Style.Pattern: the fill's pattern; Solid fills with foregroundColor", static s => s.Pattern, static (s, v) => s.Pattern = v),
        Member("custom", ValueKinds.Text, "Style.Custom: the number format's code, such as #,##0; empty for a built-in format", static s => s.Custom, static (s, v) => s.Custom = v),
        Member("number", ValueKinds.Integer, "Style.Number: the built-in number format's id", static s => s.Number, static (s, v) => s.Number = v),
        Member("horizontalAlignment", ValueKinds.Enum<TextAlignmentType>(), "Style.HorizontalAlignment", static s => s.HorizontalAlignment, static (s, v) => s.HorizontalAlignment = v),
        Member("verticalAlignment", ValueKinds.Enum<TextAlignmentType>(), "Style.VerticalAlignment", static s => s.VerticalAlignment, static (s, v) => s.VerticalAlignment = v),
        Member("isTextWrapped", ValueKinds.Boolean, "Style.IsTextWrapped", static s => s.IsTextWrapped, static (s, v) => s.IsTextWrapped = v),
        Member("indentLevel", ValueKinds.Integer, "Style.IndentLevel", static s => s.IndentLevel, static (s, v) => s.IndentLevel = v),
        Member("isLocked", ValueKinds.Boolean, "Style.IsLocked", static s => s.IsLocked, static (s, v) => s.IsLocked = v),
        Member("quotePrefix", ValueKinds.Boolean, "Style.QuotePrefix", static s => s.QuotePrefix, static (s, v) => s.QuotePrefix = v),
        .. Sides.SelectMany(static side => new StyleMember[]
        {
            Member($"borders.{side.Name}.lineStyle", ValueKinds.Enum<CellBorderType>(), $"Style.Borders[BorderType.{side.Type}].LineStyle",
                s => s.Borders[side.Type].LineStyle, (s, v) => s.Borders[side.Type].LineStyle = v),
            Member($"borders.{side.Name}.color", ValueKinds.Color, $"Style.Borders[BorderType.{side.Type}].Color",
                s => s.Borders[side.Type].Color, (s, v) => s.Borders[side.Type].Color = v),
        }),
    ];

    /// <summary>The rows of a cell's style, one per member: read through GetStyle(), set through GetStyle(), the member, SetStyle().</summary>
    internal static IEnumerable<Row<Cell>> CellRows => Members.Select(static member => member.OnCell());

    /// <summary>
    /// A style to apply: an object whose keys are the rows of a cell's style, each in its row's
    /// form; the values are set on a new <see cref="Workbook.CreateStyle()"/>.
    /// </summary>
    internal static ValueKind<Action<Style>> Settings { get; } = ValueKinds.Create<Action<Style>>(
        "style-settings",
        "An object whose keys are rows of a cell's style (see the style kind), each in that row's form.",
        static () => new JsonObject
        {
            ["type"] = "object",
            ["minProperties"] = 1,
            ["propertyNames"] = new JsonObject { ["enum"] = new JsonArray([.. Members.Select(static member => (JsonNode)member.Name)]) },
        },
        static _ => null,
        static value =>
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw ValueKinds.Expected("an object of style rows", value);
            }

            var settings = new List<Action<Style>>();
            foreach (JsonProperty property in value.EnumerateObject())
            {
                StyleMember member = Members.FirstOrDefault(candidate => candidate.Name == property.Name)
                    ?? throw new TableValueException($"'{property.Name}' is no row of a cell's style; it is one of: {string.Join(", ", Members.Select(static candidate => candidate.Name))}");
                settings.Add(member.Decode(property.Value));
            }

            return style => settings.ForEach(set => set(style));
        });

    private static StyleMember<T> Member<T>(string name, ValueKind<T> kind, string description, Func<Style, T> get, Action<Style, T> set) =>
        new(name, kind, description, get, set);
}

/// <summary>One member of a <see cref="Style"/> the table names.</summary>
internal abstract class StyleMember
{
    /// <summary>The row's name, such as <c>font.isBold</c>.</summary>
    internal abstract string Name { get; }

    /// <summary>The member as a row of a cell's style.</summary>
    internal abstract Row<Cell> OnCell();

    /// <summary>Reads a value the caller wrote for the member, as the setting of a style.</summary>
    /// <exception cref="TableValueException">The value is not of the member's kind.</exception>
    internal abstract Action<Style> Decode(JsonElement value);
}

/// <summary>One member of a <see cref="Style"/> whose values are <typeparamref name="T"/>.</summary>
internal sealed class StyleMember<T>(string name, ValueKind<T> kind, string description, Func<Style, T> get, Action<Style, T> set) : StyleMember
{
    internal override string Name => name;

    internal override Row<Cell> OnCell() => Row.Of<Cell, T>(
        name,
        kind,
        $"{description}; Cell.GetStyle(), then Cell.SetStyle(style)",
        cell => get(cell.GetStyle()),
        (cell, value) =>
        {
            Style style = cell.GetStyle();
            set(style, value);
            cell.SetStyle(style);
        });

    internal override Action<Style> Decode(JsonElement value)
    {
        T decoded = kind.Decode(value);
        return style => set(style, decoded);
    }
}

/// <summary>The properties of the engine's <see cref="StyleFlag"/>, by name: the members <c>apply_style</c> applies.</summary>
internal static class CellsStyleFlags
{
    private static readonly Dictionary<string, Action<StyleFlag>> Flags = new(StringComparer.Ordinal)
    {
        [nameof(StyleFlag.All)] = static flag => flag.All = true,
        [nameof(StyleFlag.Alignments)] = static flag => flag.Alignments = true,
        [nameof(StyleFlag.Borders)] = static flag => flag.Borders = true,
        [nameof(StyleFlag.BottomBorder)] = static flag => flag.BottomBorder = true,
        [nameof(StyleFlag.CellShading)] = static flag => flag.CellShading = true,
        [nameof(StyleFlag.DiagonalDownBorder)] = static flag => flag.DiagonalDownBorder = true,
        [nameof(StyleFlag.DiagonalUpBorder)] = static flag => flag.DiagonalUpBorder = true,
        [nameof(StyleFlag.Font)] = static flag => flag.Font = true,
        [nameof(StyleFlag.FontBold)] = static flag => flag.FontBold = true,
        [nameof(StyleFlag.FontColor)] = static flag => flag.FontColor = true,
        [nameof(StyleFlag.FontItalic)] = static flag => flag.FontItalic = true,
        [nameof(StyleFlag.FontName)] = static flag => flag.FontName = true,
        [nameof(StyleFlag.FontScript)] = static flag => flag.FontScript = true,
        [nameof(StyleFlag.FontSize)] = static flag => flag.FontSize = true,
        [nameof(StyleFlag.FontStrike)] = static flag => flag.FontStrike = true,
        [nameof(StyleFlag.FontUnderline)] = static flag => flag.FontUnderline = true,
        [nameof(StyleFlag.HideFormula)] = static flag => flag.HideFormula = true,
        [nameof(StyleFlag.HorizontalAlignment)] = static flag => flag.HorizontalAlignment = true,
        [nameof(StyleFlag.Indent)] = static flag => flag.Indent = true,
        [nameof(StyleFlag.LeftBorder)] = static flag => flag.LeftBorder = true,
        [nameof(StyleFlag.Locked)] = static flag => flag.Locked = true,
        [nameof(StyleFlag.NumberFormat)] = static flag => flag.NumberFormat = true,
        [nameof(StyleFlag.QuotePrefix)] = static flag => flag.QuotePrefix = true,
        [nameof(StyleFlag.RightBorder)] = static flag => flag.RightBorder = true,
        [nameof(StyleFlag.Rotation)] = static flag => flag.Rotation = true,
        [nameof(StyleFlag.ShrinkToFit)] = static flag => flag.ShrinkToFit = true,
        [nameof(StyleFlag.TextDirection)] = static flag => flag.TextDirection = true,
        [nameof(StyleFlag.TopBorder)] = static flag => flag.TopBorder = true,
        [nameof(StyleFlag.VerticalAlignment)] = static flag => flag.VerticalAlignment = true,
        [nameof(StyleFlag.WrapText)] = static flag => flag.WrapText = true,
    };

    /// <summary>The StyleFlag properties set to true, by name.</summary>
    internal static ValueKind<StyleFlag> Kind { get; } = ValueKinds.Create<StyleFlag>(
        "style-flag",
        "The names of the engine's StyleFlag properties to set to true, each saying which members of the style ApplyStyle applies; CellShading applies Pattern, ForegroundColor and BackgroundColor together, NumberFormat applies Number and Custom together.",
        static () => new JsonObject
        {
            ["type"] = "array",
            ["minItems"] = 1,
            ["items"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. Flags.Keys.Order(StringComparer.Ordinal).Select(static name => (JsonNode)name)]) },
        },
        static _ => null,
        static value =>
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw ValueKinds.Expected("a list of StyleFlag property names", value);
            }

            if (value.GetArrayLength() == 0)
            {
                throw new TableValueException("the list is empty; it names at least one StyleFlag property");
            }

            var flag = new StyleFlag();
            foreach (JsonElement name in value.EnumerateArray())
            {
                if (name.ValueKind != JsonValueKind.String || !Flags.TryGetValue(name.GetString()!, out Action<StyleFlag>? set))
                {
                    throw new TableValueException($"{(name.ValueKind == JsonValueKind.String ? $"'{name.GetString()}'" : "the value")} is no property of StyleFlag; it is one of: {string.Join(", ", Flags.Keys.Order(StringComparer.Ordinal))}");
                }

                set(flag);
            }

            return flag;
        });
}
