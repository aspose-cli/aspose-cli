using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Lists;
using Aspose.Words.Properties;
using Row = Aspose.Cli.Sdk.Tables.Row;
using WordList = Aspose.Words.Lists.List;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static partial class WordsTable
{
    /// <summary>A bookmark, addressed by its name: <c>bookmark/Price</c>.</summary>
    internal static readonly ObjectKind<Bookmark> BookmarkKind = new("bookmark", "A bookmark: Document.Range.Bookmarks[name].")
    {
        Row.Text<Bookmark>("name", "Bookmark.Name", static b => b.Name, static (b, v) => b.Name = v),
        Row.Text<Bookmark>("text", "Bookmark.Text: the text between its start and end; set, it replaces that text", static b => b.Text, static (b, v) => b.Text = v),
    };

    /// <summary>A style, addressed by its name: <c>style/Heading 1</c>.</summary>
    internal static readonly ObjectKind<Style> StyleKind = new("style", "A style: Document.Styles[name], whose name matches with case. Font and ParagraphFormat are null on some types of style, and so are their rows.")
    {
        Row.Text<Style>("name", "Style.Name", static s => s.Name, static (s, v) => s.Name = v),
        Row.Enum<Style, StyleType>("type", "Style.Type", static s => s.Type),
        Row.Text<Style>("baseStyleName", "Style.BaseStyleName: empty for none", static s => s.BaseStyleName, static (s, v) => s.BaseStyleName = v),
        Row.Text<Style>("nextParagraphStyleName", "Style.NextParagraphStyleName", static s => s.NextParagraphStyleName, static (s, v) => s.NextParagraphStyleName = v),
        Row.Text<Style>("font.name", "Style.Font.Name; setting it sets the ASCII, other and East Asian names alike, the engine's setter", static s => s.Font?.Name, static (s, v) => FontOf(s).Name = v),
        Row.Text<Style>("font.nameAscii", "Style.Font.NameAscii", static s => s.Font?.NameAscii, static (s, v) => FontOf(s).NameAscii = v),
        Row.Text<Style>("font.nameOther", "Style.Font.NameOther", static s => s.Font?.NameOther, static (s, v) => FontOf(s).NameOther = v),
        Row.Text<Style>("font.nameFarEast", "Style.Font.NameFarEast", static s => s.Font?.NameFarEast, static (s, v) => FontOf(s).NameFarEast = v),
        Row.Of<Style, double?>("font.size", ValueKinds.Optional(ValueKinds.Number), "Style.Font.Size, in points", static s => s.Font?.Size, static (s, v) => FontOf(s).Size = Given(v, "font.size")),
        Row.Of<Style, bool?>("font.bold", ValueKinds.Optional(ValueKinds.Boolean), "Style.Font.Bold", static s => s.Font?.Bold, static (s, v) => FontOf(s).Bold = Given(v, "font.bold")),
        Row.Of<Style, bool?>("font.italic", ValueKinds.Optional(ValueKinds.Boolean), "Style.Font.Italic", static s => s.Font?.Italic, static (s, v) => FontOf(s).Italic = Given(v, "font.italic")),
        Row.Of<Style, System.Drawing.Color?>("font.color", ValueKinds.Optional(ValueKinds.Color), "Style.Font.Color; null for the automatic color, Color.Empty",
            static s => s.Font is { } font ? font.Color : null, static (s, v) => FontOf(s).Color = v ?? System.Drawing.Color.Empty),
        Row.Of<Style, double?>("paragraphFormat.spaceBefore", ValueKinds.Optional(ValueKinds.Number), "Style.ParagraphFormat.SpaceBefore, in points", static s => s.ParagraphFormat?.SpaceBefore, static (s, v) => ParagraphFormatOf(s).SpaceBefore = Given(v, "paragraphFormat.spaceBefore")),
        Row.Of<Style, double?>("paragraphFormat.spaceAfter", ValueKinds.Optional(ValueKinds.Number), "Style.ParagraphFormat.SpaceAfter, in points", static s => s.ParagraphFormat?.SpaceAfter, static (s, v) => ParagraphFormatOf(s).SpaceAfter = Given(v, "paragraphFormat.spaceAfter")),
    };

    /// <summary>A list, addressed by its ListId: <c>list/1</c>.</summary>
    internal static readonly ObjectKind<WordList> ListKind = new("list", "A list definition: Document.Lists.GetListByListId(listId); a paragraph's listFormat.list names it.")
    {
        Row.Integer<WordList>("listId", "List.ListId", static l => l.ListId),
        Row.Boolean<WordList>("isMultiLevel", "List.IsMultiLevel", static l => l.IsMultiLevel),
    };

    /// <summary>A level of a list: <c>list/1/level/0</c>.</summary>
    internal static readonly ObjectKind<ListLevel> ListLevelKind = new("listLevel", "A level of a list: List.ListLevels[i], from 0 to 8.")
    {
        Row.Enum<ListLevel, NumberStyle>("numberStyle", "ListLevel.NumberStyle", static l => l.NumberStyle, static (l, v) => l.NumberStyle = v),
        Row.Text<ListLevel>("numberFormat", "ListLevel.NumberFormat: the characters \\u0000 to \\u0008 stand for the numbers of levels 0 to 8", static l => l.NumberFormat, static (l, v) => l.NumberFormat = v),
        Row.Integer<ListLevel>("startAt", "ListLevel.StartAt", static l => l.StartAt, static (l, v) => l.StartAt = v),
    };

    /// <summary>A revision: <c>revision/0</c>.</summary>
    internal static readonly ObjectKind<Revision> RevisionKind = new("revision", "A tracked change: Document.Revisions[i]; the revision actions accept and reject it.")
    {
        Row.Enum<Revision, RevisionType>("revisionType", "Revision.RevisionType", static r => r.RevisionType),
        Row.Text<Revision>("author", "Revision.Author", static r => r.Author),
        Row.Of<Revision, DateTime>("dateTime", WordsValues.DateTime, "Revision.DateTime", static r => r.DateTime),
        Row.Text<Revision>("parentNode.text", "Revision.ParentNode.GetText(): the text of the node the revision is on", static r => r.ParentNode?.GetText()),
    };

    /// <summary>A custom document property, addressed by its name: <c>customProperty/Client</c>.</summary>
    internal static readonly ObjectKind<DocumentProperty> CustomPropertyKind = new("customProperty", "A custom document property: Document.CustomDocumentProperties[name], whose name matches without case.")
    {
        Row.Text<DocumentProperty>("name", "DocumentProperty.Name", static p => p.Name),
        Row.Enum<DocumentProperty, PropertyType>("type", "DocumentProperty.Type", static p => p.Type),
        Row.Of<DocumentProperty, object>("value", WordsValues.PropertyValue, "DocumentProperty.Value", static p => p.Value, static (p, v) => p.Value = v),
    };

    private static Font FontOf(Style style) => style.Font ?? throw TableErrors.Absent("Style.Font");

    private static ParagraphFormat ParagraphFormatOf(Style style) => style.ParagraphFormat ?? throw TableErrors.Absent("Style.ParagraphFormat");

    private static T Given<T>(T? value, string row)
        where T : struct =>
        value ?? throw TableErrors.InvalidArguments($"{row} takes a value; null only reads a style without one");
}
