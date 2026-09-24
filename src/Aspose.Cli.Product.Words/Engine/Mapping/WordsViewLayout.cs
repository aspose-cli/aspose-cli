using System.Drawing;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Layout;
using Aspose.Words.Tables;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Places the body blocks of a document on its rendered pages. Every
/// paragraph and table gets the bounds of its lines or rows on each page it
/// spans, plus a digest of its content and formatting, so a viewer can point
/// at exactly the block that changed. The fixed-page layout is aligned with
/// the body in reading order; a document whose layout cannot be aligned
/// exactly (for example because of hidden paragraphs) gets no elements rather
/// than misplaced ones.
/// </summary>
internal static class WordsViewLayout
{
    private const double CssPerPoint = 96d / 72d;
    private const int LabelLength = 80;

    /// <summary>Returns the elements of each of the first <paramref name="pageCount"/> pages.</summary>
    internal static IReadOnlyList<IReadOnlyList<ViewElement>> Collect(Document document, bool evaluation, int pageCount)
    {
        List<LayoutBlock>? blocks = WalkLayout(document, pageCount);
        List<Node> body = BodyBlocks(document);
        if (blocks is null || blocks.Count > body.Count)
        {
            return Empty(pageCount);
        }

        var index = new DocumentBlockIndex(document, evaluation);
        var pages = Enumerable.Range(0, pageCount).Select(static _ => new List<ViewElement>()).ToArray();
        for (int position = 0; position < blocks.Count; position++)
        {
            Node node = body[position];
            LayoutBlock block = blocks[position];
            if (block.IsTable != node is Table)
            {
                return Empty(pageCount);
            }
            if (index.FindBlock(node) is null)
            {
                // The evaluation banner is laid out but is not a document block.
                continue;
            }

            (string kind, int? level) = Classify(node);
            string digest = Digest(node);
            string? label = Label(node);
            foreach ((int page, RectangleF bounds) in block.Fragments)
            {
                pages[page - 1].Add(new ViewElement
                {
                    Kind = kind,
                    Level = level,
                    Label = label,
                    Digest = digest,
                    Box = new ViewBox(
                        Math.Round(bounds.X * CssPerPoint, 2),
                        Math.Round(bounds.Y * CssPerPoint, 2),
                        Math.Round(bounds.Width * CssPerPoint, 2),
                        Math.Round(bounds.Height * CssPerPoint, 2)),
                });
            }
        }
        return pages;
    }

    private static IReadOnlyList<IReadOnlyList<ViewElement>> Empty(int pageCount) =>
        Enumerable.Range(0, pageCount).Select(static _ => (IReadOnlyList<ViewElement>)[]).ToArray();

    private static List<Node> BodyBlocks(Document document)
    {
        var blocks = new List<Node>();
        foreach (Section section in document.Sections)
        {
            blocks.AddRange(DocumentBlockIndex.BodyBlocks(section.Body));
        }
        return blocks;
    }

    /// <summary>
    /// Walks the column content of the first pages in reading order. Lines
    /// accumulate into a paragraph until its paragraph or section mark; rows
    /// accumulate into a table until the next line. Headers, footers and notes
    /// live outside the column content and are ignored.
    /// </summary>
    private static List<LayoutBlock>? WalkLayout(Document document, int pageCount)
    {
        var blocks = new List<LayoutBlock>();
        if (pageCount == 0)
        {
            return blocks;
        }

        LayoutBlock? open = null;
        var enumerator = new LayoutEnumerator(document);
        enumerator.Reset();
        do
        {
            int page = enumerator.PageIndex;
            if (enumerator.Type != LayoutEntityType.Page || page > pageCount)
            {
                break;
            }
            if (!enumerator.MoveFirstChild())
            {
                continue;
            }
            do
            {
                if (enumerator.Type == LayoutEntityType.Column
                    && !VisitColumn(enumerator, page, blocks, ref open))
                {
                    return null;
                }
            }
            while (enumerator.MoveNext());
            _ = enumerator.MoveParent();
        }
        while (enumerator.MoveNext());

        if (open is not null)
        {
            // A block that continues beyond the last rendered page.
            blocks.Add(open);
        }
        return blocks;
    }

    private static bool VisitColumn(
        LayoutEnumerator enumerator,
        int page,
        List<LayoutBlock> blocks,
        ref LayoutBlock? open)
    {
        if (!enumerator.MoveFirstChild())
        {
            return true;
        }
        do
        {
            if (enumerator.Type == LayoutEntityType.Line)
            {
                if (open is { IsTable: true })
                {
                    blocks.Add(open);
                    open = null;
                }
                open ??= new LayoutBlock(isTable: false);
                open.Include(page, enumerator.Rectangle);
                if (EndsParagraph(enumerator))
                {
                    blocks.Add(open);
                    open = null;
                }
            }
            else if (enumerator.Type == LayoutEntityType.Row)
            {
                if (open is { IsTable: false })
                {
                    _ = enumerator.MoveParent();
                    return false;
                }
                open ??= new LayoutBlock(isTable: true);
                open.Include(page, enumerator.Rectangle);
            }
        }
        while (enumerator.MoveNext());
        _ = enumerator.MoveParent();
        return true;
    }

    private static bool EndsParagraph(LayoutEnumerator enumerator)
    {
        bool ends = false;
        if (enumerator.MoveFirstChild())
        {
            do
            {
                ends |= enumerator.Kind is "PARAGRAPH" or "SECTION";
            }
            while (enumerator.MoveNext());
            _ = enumerator.MoveParent();
        }
        return ends;
    }

    private static (string Kind, int? Level) Classify(Node node) => node switch
    {
        Paragraph paragraph when InfoProjection.HeadingLevel(paragraph) is int level => ("heading", level),
        Paragraph { IsListItem: true } => ("list-item", null),
        Paragraph => ("paragraph", null),
        _ => ("table", null),
    };

    private static string? Label(Node node)
    {
        string text = string.Join(
            ' ',
            WordsText.Of(node)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length switch
        {
            0 => null,
            <= LabelLength => text,
            _ => text[..LabelLength],
        };
    }

    /// <summary>Digest of a block's text, paragraph and character formatting and inline images.</summary>
    private static string Digest(Node node)
    {
        var canonical = new StringBuilder();
        Append(canonical, node);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..16]
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder canonical, Node node)
    {
        switch (node)
        {
            case Paragraph paragraph:
                ParagraphFormat format = paragraph.ParagraphFormat;
                canonical.Append("P|").Append(format.StyleName)
                    .Append('|').Append((int)format.Alignment)
                    .Append('|').Append(Number(format.LeftIndent))
                    .Append('|').Append(Number(format.FirstLineIndent))
                    .Append('|').Append(Number(format.SpaceBefore))
                    .Append('|').Append(Number(format.SpaceAfter))
                    .Append('|').Append(Number(format.LineSpacing))
                    .Append('|').Append(format.Shading.BackgroundPatternColor.ToArgb());
                if (paragraph.IsListItem)
                {
                    canonical.Append("|L").Append(paragraph.ListFormat.ListLevelNumber)
                        .Append('|').Append(paragraph.ListLabel?.LabelString);
                }
                foreach (Node child in paragraph.GetChildNodes(NodeType.Any, false))
                {
                    AppendInline(canonical, child);
                }
                break;
            case Table table:
                canonical.Append('T');
                foreach (Row row in table.Rows)
                {
                    canonical.Append("|row");
                    foreach (Cell cell in row.Cells)
                    {
                        canonical.Append("|cell|")
                            .Append(cell.CellFormat.Shading.BackgroundPatternColor.ToArgb())
                            .Append('|').Append(Number(cell.CellFormat.Width));
                        foreach (Node child in cell.GetChildNodes(NodeType.Any, false))
                        {
                            Append(canonical, child);
                        }
                    }
                }
                break;
        }
    }

    private static void AppendInline(StringBuilder canonical, Node node)
    {
        switch (node)
        {
            case Run run:
                Aspose.Words.Font font = run.Font;
                canonical.Append("|R|").Append(run.Text)
                    .Append('|').Append(font.Name)
                    .Append('|').Append(Number(font.Size))
                    .Append('|').Append(font.Bold ? 'b' : '-')
                    .Append(font.Italic ? 'i' : '-')
                    .Append(font.StrikeThrough ? 's' : '-')
                    .Append((int)font.Underline)
                    .Append('|').Append(font.Color.ToArgb())
                    .Append('|').Append(font.HighlightColor.ToArgb());
                break;
            case Shape shape:
                canonical.Append("|S|").Append((int)shape.ShapeType)
                    .Append('|').Append(Number(shape.Width))
                    .Append('|').Append(Number(shape.Height));
                if (shape.HasImage)
                {
                    canonical.Append('|').Append(
                        Convert.ToHexString(SHA256.HashData(shape.ImageData.ImageBytes)));
                }
                break;
            default:
                canonical.Append('|').Append((int)node.NodeType).Append('|').Append(node.GetText());
                break;
        }
    }

    private static string Number(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed class LayoutBlock(bool isTable)
    {
        private readonly SortedDictionary<int, RectangleF> _pages = [];

        public bool IsTable { get; } = isTable;

        public IEnumerable<(int Page, RectangleF Bounds)> Fragments =>
            _pages.Select(static pair => (pair.Key, pair.Value));

        public void Include(int page, RectangleF bounds) =>
            _pages[page] = _pages.TryGetValue(page, out RectangleF existing)
                ? RectangleF.Union(existing, bounds)
                : bounds;
    }
}
