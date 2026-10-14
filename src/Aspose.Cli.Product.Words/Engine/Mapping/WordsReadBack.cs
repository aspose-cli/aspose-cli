using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The state the edit operations write, read back under the names the operations set it with:
/// a style's definition, the watermark, a section's page numbering, how a table breaks across
/// pages, a paragraph's list, and the hyperlinks of a paragraph's runs.
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
    [
        .. document.Styles.Cast<Style>().OrderBy(static style => style.Name, StringComparer.Ordinal).Select(static style => new WordsStyleData
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
        }),
    ];

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
            NumberStyle = setup.PageNumberStyle switch
            {
                NumberStyle.Arabic => PageNumberStyles.Decimal,
                NumberStyle.UppercaseRoman => PageNumberStyles.RomanUpper,
                NumberStyle.LowercaseRoman => PageNumberStyles.RomanLower,
                NumberStyle.UppercaseLetter => PageNumberStyles.LettersUpper,
                NumberStyle.LowercaseLetter => PageNumberStyles.LettersLower,
                NumberStyle.None => PageNumberStyles.None,
                _ => null,
            },
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
    internal static WordsListData? List(Paragraph paragraph) =>
        paragraph.ListFormat.IsListItem
            ? new WordsListData
            {
                Kind = paragraph.ListFormat.ListLevel.NumberStyle == NumberStyle.Bullet ? "bullet" : "number",
                Level = paragraph.ListFormat.ListLevelNumber + 1,
            }
            : null;

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

    /// <summary>A color as #RRGGBB, or null for an automatic or empty one.</summary>
    internal static string? Hex(System.Drawing.Color color) =>
        color.IsEmpty || color.A == 0 ? null : $"#{color.ToArgb() & 0xffffff:X6}";
}
