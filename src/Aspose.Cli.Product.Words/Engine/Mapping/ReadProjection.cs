using Aspose.Cli.Product.Words.Contracts;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Tables;
using ContractImageData = Aspose.Cli.Product.Words.Contracts.ImageData;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static class ReadProjection
{
    public static DocumentReadResult Project(LoadedDocument loaded, string path, DocumentReadRequest request)
    {
        var index = new DocumentBlockIndex(loaded.Document);
        IReadOnlyList<BlockEntry> candidates;
        if (request.Section is { } section)
        {
            if (section < 1 || section > loaded.Document.Sections.Count)
            {
                throw WordsErrors.SectionNotFound(section, loaded.Document.Sections.Count);
            }

            candidates = index.Entries.Where(entry => entry.Section == section).ToArray();
        }
        else if (request.Blocks is { } range)
        {
            candidates = range.Resolve(index.Count).Select(index.Get).ToArray();
        }
        else
        {
            candidates = index.Entries;
        }

        if (request.Scope == "outline")
        {
            candidates = candidates.Where(static entry => entry.Node is Paragraph p && InfoProjection.HeadingLevel(p) is not null).ToArray();
        }

        int take = TakeCount(
            candidates.Select(CharacterCost),
            request.MaxBlocks,
            request.MaxCharacters);
        IReadOnlyList<BlockEntry> selected = candidates.Take(take).ToArray();
        int remaining = candidates.Count - selected.Count;
        int used = 0;
        var blocks = new List<BlockData>(selected.Count);
        foreach (BlockEntry entry in selected)
        {
            int available = Math.Max(1, request.MaxCharacters - used);
            BlockData block = ProjectBlock(entry, request.Scope, available);
            used += Math.Min(available, CharacterCost(entry));
            blocks.Add(block);
        }

        int first = selected.Count == 0 ? 0 : selected[0].Index;
        int last = selected.Count == 0 ? 0 : selected[^1].Index;
        string window = selected.Count == 0 ? "empty" : first == last ? first.ToString() : $"{first}-{last}";
        string? next = remaining <= 0
            ? null
            : $"aspose-cli words query blocks \"{path}\" --blocks {FormatBlocks(candidates.Skip(take).Select(static entry => entry.Index))} --scope {request.Scope} --max-chars {request.MaxCharacters} --max-blocks {request.MaxBlocks} --output json";

        return new DocumentReadResult
        {
            Source = InfoProjection.Source(path, loaded),
            Scope = request.Scope,
            Window = new BlockWindow { Blocks = window, Of = index.Count, Truncated = remaining > 0 || blocks.Any(static b => b.ContentTruncated) },
            Blocks = blocks,
            Next = next,
        };
    }

    private static int CharacterCost(BlockEntry entry) => Math.Max(1, InfoProjection.Clean(entry.Node.GetText()).Length);

    private static int TakeCount(
        IEnumerable<int> characterCosts,
        int maxItems,
        int maxCharacters)
    {
        int count = 0;
        long characters = 0;
        foreach (int cost in characterCosts)
        {
            if (count > 0
                && (count >= maxItems
                    || characters + Math.Max(0, cost) > maxCharacters))
            {
                break;
            }
            count++;
            characters += Math.Max(0, cost);
        }
        return count;
    }

    private static string FormatBlocks(IEnumerable<int> indices)
    {
        int[] values = indices.ToArray();
        var parts = new List<string>();
        int start = values[0];
        int previous = start;
        for (int index = 1; index < values.Length; index++)
        {
            int current = values[index];
            if (current == previous + 1)
            {
                previous = current;
                continue;
            }

            parts.Add(start == previous ? start.ToString() : $"{start}-{previous}");
            start = previous = current;
        }

        parts.Add(start == previous ? start.ToString() : $"{start}-{previous}");
        return string.Join(',', parts);
    }

    private static BlockData ProjectBlock(BlockEntry entry, string scope, int available)
    {
        if (entry.Node is Paragraph paragraph)
        {
            string text = InfoProjection.Clean(paragraph.GetText());
            bool truncated = text.Length > available;
            string projected = truncated ? text[..available] : text;
            return new BlockData
            {
                I = entry.Index,
                Type = "paragraph",
                Section = entry.Section,
                Text = projected,
                Style = paragraph.ParagraphFormat.StyleName,
                HeadingLevel = InfoProjection.HeadingLevel(paragraph),
                Runs = scope == "full" ? paragraph.Runs.Cast<Run>().Select(run => new RunData
                {
                    Text = run.Text,
                    Font = run.Font.Name,
                    Size = run.Font.Size,
                    Bold = run.Font.Bold,
                    Italic = run.Font.Italic,
                    Color = $"#{run.Font.Color.ToArgb() & 0xffffff:X6}",
                }).Take(500).ToArray() : null,
                Images = paragraph.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage)
                    .Select(shape => new ContractImageData
                    {
                        Block = entry.Index,
                        Name = shape.Name,
                        WidthPoints = shape.Width,
                        HeightPoints = shape.Height,
                    }).ToArray(),
                BreakAfter = paragraph.ParagraphFormat.PageBreakBefore
                    || paragraph.Runs.Cast<Run>().Any(static run => run.Text.Contains(ControlChar.PageBreak, StringComparison.Ordinal))
                        ? "page"
                        : null,
                ContentTruncated = truncated,
            };
        }

        var table = (Table)entry.Node;
        int used = 0;
        bool truncatedCells = false;
        var rows = new List<IReadOnlyList<string>>();
        foreach (Row row in table.Rows)
        {
            var cells = new List<string>();
            foreach (Cell cell in row.Cells)
            {
                string value = InfoProjection.Clean(cell.GetText());
                int remaining = Math.Max(0, available - used);
                if (value.Length > remaining)
                {
                    value = value[..remaining];
                    truncatedCells = true;
                }

                used += value.Length;
                cells.Add(value);
                if (used >= available)
                {
                    truncatedCells = true;
                    break;
                }
            }

            rows.Add(cells);
            if (used >= available)
            {
                break;
            }
        }

        return new BlockData
        {
            I = entry.Index,
            Type = "table",
            Section = entry.Section,
            Rows = table.Rows.Count,
            Columns = table.Rows.Count == 0 ? 0 : table.Rows.Cast<Row>().Max(static row => row.Cells.Count),
            Cells = rows,
            ContentTruncated = truncatedCells || rows.Count < table.Rows.Count,
        };
    }
}
