using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Tables;
using ContractImageData = Aspose.Cli.Product.Words.Contracts.ImageData;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class ReadProjection
{
    public static DocumentReadResult Project(LoadedDocument loaded, string path, DocumentReadRequest request)
    {
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        IReadOnlyList<BlockEntry> candidates = request.Blocks is { } range
            ? index.Select(range)
            : index.Entries;
        if (request.Section is { } section)
        {
            if (section < 1 || section > loaded.Document.Sections.Count)
            {
                throw WordsErrors.SectionNotFound(section, loaded.Document.Sections.Count);
            }

            candidates = candidates.Where(entry => entry.Section == section).ToArray();
        }

        if (request.Scope == "outline")
        {
            candidates = candidates.Where(static entry => entry.Node is Paragraph p && InfoProjection.HeadingLevel(p) is not null).ToArray();
        }

        int remainingCharacters = request.MaxCharacters;
        var blocks = new List<BlockData>();
        foreach (BlockEntry entry in candidates)
        {
            if (blocks.Count >= request.MaxBlocks || blocks.Count > 0 && remainingCharacters == 0)
            {
                break;
            }
            int available = remainingCharacters;
            BlockData block = ProjectBlock(entry, request.Scope, ref available);
            if (blocks.Count > 0 && block.ContentTruncated)
            {
                break;
            }
            blocks.Add(block);
            remainingCharacters = available;
        }

        int first = blocks.Count == 0 ? 0 : blocks[0].I;
        int last = blocks.Count == 0 ? 0 : blocks[^1].I;
        int remaining = candidates.Count - blocks.Count;
        string window = blocks.Count == 0 ? "empty" : first == last ? first.ToString() : $"{first}-{last}";

        return new DocumentReadResult
        {
            Source = InfoProjection.Source(path, loaded),
            Scope = request.Scope,
            Window = new BlockWindow { Blocks = window, Of = index.Count, Truncated = remaining > 0 || blocks.Any(static b => b.ContentTruncated) },
            Blocks = blocks,
        };
    }

    private static BlockData ProjectBlock(BlockEntry entry, string scope, ref int remaining)
    {
        bool truncated = false;
        if (entry.Node is Paragraph paragraph)
        {
            string text = Take(WordsText.Of(paragraph), ref remaining, ref truncated);
            var runs = scope == "full" ? new List<RunData>() : null;
            if (runs is not null)
            {
                foreach (Run run in paragraph.Runs)
                {
                    if (runs.Count == 500 || remaining == 0 && run.Text.Length > 0)
                    {
                        truncated = true;
                        break;
                    }
                    runs.Add(new RunData
                    {
                        Text = Take(run.Text, ref remaining, ref truncated),
                        Font = run.Font.Name,
                        Size = run.Font.Size,
                        Bold = run.Font.Bold,
                        Italic = run.Font.Italic,
                        Color = $"#{run.Font.Color.ToArgb() & 0xffffff:X6}",
                    });
                }
            }
            return new BlockData
            {
                I = entry.Index,
                Type = "paragraph",
                Section = entry.Section,
                Text = text,
                Style = paragraph.ParagraphFormat.StyleName,
                HeadingLevel = InfoProjection.HeadingLevel(paragraph),
                Runs = runs,
                Images = paragraph.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage)
                    .Select(shape => new ContractImageData
                    {
                        Block = entry.Index,
                        Name = shape.Name,
                        WidthPoints = shape.Width,
                        HeightPoints = shape.Height,
                    }).ToArray(),
                BreakAfter = BreakAfter(paragraph),
                ContentTruncated = truncated,
            };
        }

        var table = (Table)entry.Node;
        var rows = new List<IReadOnlyList<string>>();
        foreach (Row row in table.Rows)
        {
            if (remaining == 0)
            {
                truncated = true;
                break;
            }
            var cells = new List<string>();
            foreach (Cell cell in row.Cells)
            {
                if (remaining == 0)
                {
                    truncated = true;
                    break;
                }
                cells.Add(Take(WordsText.Of(cell), ref remaining, ref truncated));
            }
            rows.Add(cells);
        }
        return new BlockData
        {
            I = entry.Index,
            Type = "table",
            Section = entry.Section,
            Rows = table.Rows.Count,
            Columns = table.Rows.Count == 0 ? 0 : table.Rows.Cast<Row>().Max(static row => row.Cells.Count),
            Cells = rows,
            ContentTruncated = truncated,
        };
    }

    private static string Take(string value, ref int remaining, ref bool truncated)
    {
        int length = Math.Min(value.Length, remaining);
        truncated |= length < value.Length;
        remaining -= length;
        return value[..length];
    }

    /// <summary>
    /// The break that ends a paragraph block: a page break inside it or on the next paragraph
    /// (page break before), or a section break when it is the last block of a section.
    /// </summary>
    private static string? BreakAfter(Paragraph paragraph)
    {
        if (paragraph.Runs.Cast<Run>().Any(static run => run.Text.Contains(ControlChar.PageBreak, StringComparison.Ordinal))
            || paragraph.NextSibling is Paragraph { ParagraphFormat.PageBreakBefore: true })
        {
            return "page";
        }

        return paragraph.IsEndOfSection && paragraph.ParentSection?.NextSibling is Section ? "section" : null;
    }
}
