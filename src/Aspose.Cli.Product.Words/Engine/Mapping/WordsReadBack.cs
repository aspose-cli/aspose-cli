using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Properties;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The state the edit operations write, read back under the names the operations set it with:
/// styles, document properties, the watermark, page numbering, table pagination, lists, hyperlinks
/// and what a saved document keeps. Edit snapshots, <c>--verify</c> and named addresses use these.
/// </summary>
internal static class WordsReadBack
{
    // The names Word gives the header shapes that draw a text or an image watermark.
    private static readonly string[] WatermarkNames = ["PowerPlusWaterMarkObject", "WordPictureWatermark"];

    /// <summary>The header shapes that draw the document's watermark.</summary>
    internal static IEnumerable<Shape> WatermarkShapes(Document document) =>
        document.Sections.Cast<Section>()
            .SelectMany(static section => section.HeadersFooters.Cast<HeaderFooter>())
            .SelectMany(static headerFooter => headerFooter.GetChildNodes(NodeType.Shape, true).Cast<Shape>())
            .Where(static shape => WatermarkNames.Any(name => shape.Name.StartsWith(name, StringComparison.Ordinal)));

    /// <summary>The document's watermark, or null without one.</summary>
    internal static WordsWatermarkData? Watermark(Document document)
    {
        if (document.Watermark.Type == WatermarkType.None || WatermarkShapes(document).FirstOrDefault() is not { } shape)
        {
            return null;
        }

        return document.Watermark.Type == WatermarkType.Text
            ? new WordsWatermarkData
            {
                Kind = "text",
                Text = shape.TextPath.Text,
                Font = shape.TextPath.FontFamily,
                Color = Hex(shape.FillColor),
                Faded = shape.Fill.Opacity < 1,
            }
            : new WordsWatermarkData
            {
                Kind = "image",
                // A washed-out image has its contrast lowered, as Word's washout does.
                Faded = shape.ImageData.Contrast < 0.5,
            };
    }

    /// <summary>Every style the document defines, by name.</summary>
    internal static IReadOnlyList<WordsStyleData> Styles(Document document) =>
        [.. StylesInDocumentOrder(document).Select(static style => style.Read).OrderBy(static style => style.Name, StringComparer.Ordinal)];

    /// <summary>Every style the document defines, in the order the document holds them, with what a read reports of it.</summary>
    internal static IEnumerable<(Style Style, WordsStyleData Read)> StylesInDocumentOrder(Document document) =>
        document.Styles.Cast<Style>().Select(static style => (style, new WordsStyleData
        {
            Name = style.Name,
            Kind = style.Type switch
            {
                StyleType.Character => "character",
                StyleType.Table => "table",
                StyleType.List => "list",
                _ => "paragraph",
            },
            BasedOn = string.IsNullOrEmpty(style.BaseStyleName) ? null : style.BaseStyleName,
            Font = style.Font?.Name,
            LatinFont = style.Font?.NameAscii,
            EastAsianFont = style.Font?.NameFarEast,
            FontSize = style.Font?.Size,
            Bold = style.Font?.Bold,
            Color = style.Font is { } font ? Hex(font.Color) : null,
            SpaceBefore = style.Type == StyleType.Paragraph ? style.ParagraphFormat.SpaceBefore : null,
            SpaceAfter = style.Type == StyleType.Paragraph ? style.ParagraphFormat.SpaceAfter : null,
        }));

    /// <summary>A section's page numbering and the PAGE field of its primary header, else footer.</summary>
    internal static WordsPageNumbersData PageNumbers(Section section)
    {
        PageSetup setup = section.PageSetup;
        (string Location, HeaderFooterType Type)[] places = [("header", HeaderFooterType.HeaderPrimary), ("footer", HeaderFooterType.FooterPrimary)];
        string? location = null;
        string? alignment = null;
        foreach ((string place, HeaderFooterType type) in places)
        {
            if (section.HeadersFooters[type]?.Range.Fields.Cast<Field>().FirstOrDefault(static field => field.Type == FieldType.FieldPage) is { } page)
            {
                location = place;
                alignment = (page.Start.GetAncestor(NodeType.Paragraph) as Paragraph)?.ParagraphFormat.Alignment switch
                {
                    ParagraphAlignment.Left => "left",
                    ParagraphAlignment.Center => "center",
                    ParagraphAlignment.Right => "right",
                    _ => null,
                };
                break;
            }
        }

        return new WordsPageNumbersData
        {
            Location = location,
            Alignment = alignment,
            NumberStyle = NumberStyleName(setup.PageNumberStyle),
            Start = setup.RestartPageNumbering ? setup.PageStartingNumber : null,
        };
    }

    /// <summary>How a table breaks across pages, as set_table_pagination sets it.</summary>
    internal static (bool KeepTogether, bool AllowRowBreakAcrossPages, int HeaderRowCount, bool KeepWithNext) Pagination(Table table)
    {
        Row[] rows = [.. table.Rows.Cast<Row>()];
        if (rows.Length == 0)
        {
            return (false, true, 0, false);
        }

        Paragraph[] lastEnds = [.. rows[^1].Cells.Cast<Cell>().Select(static cell => cell.LastParagraph).OfType<Paragraph>()];
        bool keepTogether = rows.All(static row => !row.RowFormat.AllowBreakAcrossPages)
            && table.GetChildNodes(NodeType.Paragraph, true).Cast<Paragraph>()
                .Where(paragraph => !lastEnds.Contains(paragraph))
                .All(static paragraph => paragraph.ParagraphFormat.KeepWithNext);
        return (
            keepTogether,
            rows.All(static row => row.RowFormat.AllowBreakAcrossPages),
            rows.TakeWhile(static row => row.RowFormat.HeadingFormat).Count(),
            lastEnds.Length > 0 && lastEnds.All(static paragraph => paragraph.ParagraphFormat.KeepWithNext));
    }

    /// <summary>A list paragraph's list, or null for a paragraph in none.</summary>
    internal static WordsListData? List(Paragraph paragraph)
    {
        if (!paragraph.ListFormat.IsListItem)
        {
            return null;
        }

        ListLevel level = paragraph.ListFormat.ListLevel;
        bool bullet = level.NumberStyle == NumberStyle.Bullet;
        return new WordsListData
        {
            Kind = bullet ? "bullet" : "number",
            Level = paragraph.ListFormat.ListLevelNumber + 1,
            NumberStyle = bullet || level.NumberStyle == NumberStyle.None ? null : NumberStyleName(level.NumberStyle),
            NumberFormat = bullet ? null : NumberFormatNotation(level.NumberFormat),
            Start = bullet ? null : level.StartAt,
        };
    }

    /// <summary>
    /// The engine's number format of a numberFormat, whose %1 to %9 the engine stores as the
    /// characters 0 to 8 that stand for the numbers of those levels, and whose %% is a percent sign.
    /// </summary>
    internal static string NumberFormatOf(string notation)
    {
        var format = new System.Text.StringBuilder(notation.Length);
        for (int index = 0; index < notation.Length; index++)
        {
            if (notation[index] == '%' && index + 1 < notation.Length && notation[index + 1] is >= '1' and <= '9')
            {
                format.Append((char)(notation[++index] - '1'));
            }
            else if (notation[index] == '%' && index + 1 < notation.Length && notation[index + 1] == '%')
            {
                format.Append(notation[++index]);
            }
            else
            {
                format.Append(notation[index]);
            }
        }

        return format.ToString();
    }

    /// <summary>The numberFormat of an engine number format: <see cref="NumberFormatOf"/> reversed.</summary>
    internal static string NumberFormatNotation(string format)
    {
        var notation = new System.Text.StringBuilder(format.Length + 2);
        foreach (char character in format)
        {
            if (character < (char)9)
            {
                notation.Append('%').Append((char)('1' + character));
            }
            else if (character == '%')
            {
                notation.Append("%%");
            }
            else
            {
                notation.Append(character);
            }
        }

        return notation.ToString();
    }

    /// <summary>The number styles of page numbers and numbered lists, by their names (<see cref="PageNumberStyles"/>).</summary>
    private static readonly (string Name, NumberStyle Style)[] NumberStyles =
    [
        (PageNumberStyles.Decimal, NumberStyle.Arabic),
        (PageNumberStyles.RomanUpper, NumberStyle.UppercaseRoman),
        (PageNumberStyles.RomanLower, NumberStyle.LowercaseRoman),
        (PageNumberStyles.LettersUpper, NumberStyle.UppercaseLetter),
        (PageNumberStyles.LettersLower, NumberStyle.LowercaseLetter),
        (PageNumberStyles.None, NumberStyle.None),
    ];

    /// <summary>The engine's number style of a number style name.</summary>
    internal static NumberStyle NumberStyleOf(string name) => NumberStyles.Single(entry => entry.Name == name).Style;

    /// <summary>The name of an engine number style, or null for one no name stands for.</summary>
    internal static string? NumberStyleName(NumberStyle style) =>
        NumberStyles.FirstOrDefault(entry => entry.Style == style).Name;

    /// <summary>The address of the hyperlink each run of a paragraph is the text of.</summary>
    internal static IReadOnlyDictionary<Run, string> Hyperlinks(Paragraph paragraph)
    {
        var links = new Dictionary<Run, string>(ReferenceEqualityComparer.Instance);
        foreach (FieldHyperlink link in paragraph.Range.Fields.OfType<FieldHyperlink>())
        {
            for (Node? node = link.Separator?.NextSibling; node is not null && !ReferenceEquals(node, link.End); node = node.NextSibling)
            {
                if (node is Run run)
                {
                    links[run] = link.Address;
                }
            }
        }

        return links;
    }

    /// <summary>The keys a read reports the built-in title, author, subject and keywords under.</summary>
    internal static readonly string[] StandardProperties = ["title", "author", "subject", "keywords"];

    /// <summary>
    /// The title, author, subject and keywords, then every custom property, each under the key a
    /// read reports it under (<see cref="PropertyKey"/>), with its value and its type.
    /// </summary>
    internal static IEnumerable<(string Key, string? Value, PropertyType Type)> Properties(Document document)
    {
        BuiltInDocumentProperties builtIn = document.BuiltInDocumentProperties;
        return StandardProperties
            .Zip([builtIn.Title, builtIn.Author, builtIn.Subject, builtIn.Keywords], static (key, value) => (key, (string?)value, PropertyType.String))
            .Concat(document.CustomDocumentProperties.Select(static property => (PropertyKey(property.Name), property.Value?.ToString(), property.Type)));
    }

    /// <summary>The address of a style, by the name a read reports it under.</summary>
    internal static string StyleAddress(string name) => Address("style", name);

    /// <summary>The address of a document property, by the key a read reports it under.</summary>
    internal static string PropertyAddress(string key) => Address("property", key);

    // The one place a named object's address is made: its unit and its name, escaped.
    private static string Address(string unit, string name) => $"{unit}/{TargetName.Escape(name)}";

    /// <summary>
    /// What a saved document keeps that inspect reports and <c>--verify</c> compares after save and
    /// reopen: its field and revision counts and its editing restriction, as the protect mode that sets it.
    /// </summary>
    internal static WordsSavedState Saved(Document document) =>
        new(document.Range.Fields.Count, document.Revisions.Count, WordsProtection.ToMode(document.ProtectionType));

    /// <summary>The prefix of the key of a custom property whose name is a standard key.</summary>
    internal const string CustomPrefix = "custom:";

    /// <summary>
    /// The key a read reports a custom property under, which its target address escapes: its
    /// name as it is, with <c>custom:</c> before a name that is a standard key, or that itself
    /// starts with <c>custom:</c>.
    /// </summary>
    internal static string PropertyKey(string name) =>
        StandardProperties.Contains(name, StringComparer.Ordinal) || name.StartsWith(CustomPrefix, StringComparison.Ordinal)
            ? CustomPrefix + name
            : name;

    /// <summary>The colors of Word's highlight palette by their names (<see cref="WordsHighlightColors"/>).</summary>
    private static readonly (string Name, int Rgb)[] HighlightPalette =
    [
        (WordsHighlightColors.Yellow, 0xFFFF00),
        (WordsHighlightColors.Green, 0x00FF00),
        (WordsHighlightColors.Cyan, 0x00FFFF),
        (WordsHighlightColors.Magenta, 0xFF00FF),
        (WordsHighlightColors.Blue, 0x0000FF),
        (WordsHighlightColors.Red, 0xFF0000),
        (WordsHighlightColors.DarkBlue, 0x000080),
        (WordsHighlightColors.DarkCyan, 0x008080),
        (WordsHighlightColors.DarkGreen, 0x008000),
        (WordsHighlightColors.DarkMagenta, 0x800080),
        (WordsHighlightColors.DarkRed, 0x800000),
        (WordsHighlightColors.DarkYellow, 0x808000),
        (WordsHighlightColors.DarkGray, 0x808080),
        (WordsHighlightColors.LightGray, 0xC0C0C0),
        (WordsHighlightColors.Black, 0x000000),
        (WordsHighlightColors.White, 0xFFFFFF),
    ];

    /// <summary>The color of a highlight palette name.</summary>
    internal static System.Drawing.Color HighlightColor(string name) =>
        System.Drawing.Color.FromArgb(unchecked((int)0xFF000000) | HighlightPalette.Single(entry => entry.Name == name).Rgb);

    /// <summary>
    /// The palette name of a run's highlight, or null without one. Word stores only palette
    /// colors; a color another format brought in reads as the palette color nearest to it, as a
    /// Word save stores it.
    /// </summary>
    internal static string? HighlightName(System.Drawing.Color color) =>
        color.IsEmpty || color.A == 0 ? null : NearestHighlight(color.ToArgb() & 0xffffff);

    /// <summary>The palette name nearest to an RGB color.</summary>
    internal static string NearestHighlight(int rgb) => HighlightPalette.MinBy(entry =>
    {
        int red = ((entry.Rgb >> 16) & 0xff) - ((rgb >> 16) & 0xff);
        int green = ((entry.Rgb >> 8) & 0xff) - ((rgb >> 8) & 0xff);
        int blue = (entry.Rgb & 0xff) - (rgb & 0xff);
        return (red * red) + (green * green) + (blue * blue);
    }).Name;

    /// <summary>A color as #RRGGBB, or null for an automatic or empty one.</summary>
    internal static string? Hex(System.Drawing.Color color) =>
        color.IsEmpty || color.A == 0 ? null : $"#{color.ToArgb() & 0xffffff:X6}";
}

/// <summary>The state of a saved document that <see cref="WordsReadBack.Saved"/> reads.</summary>
internal readonly record struct WordsSavedState(int FieldCount, int RevisionCount, string? Protection);
