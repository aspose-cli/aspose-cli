using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Tables;

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
            WordsSections.Get(loaded.Document, section);
            candidates = candidates.Where(entry => entry.Section == section).ToArray();
            // Block numbers run through the whole document, so a range can miss the section.
            if (request.Blocks is not null && candidates.Count == 0)
            {
                int[] held = index.Entries.Where(entry => entry.Section == section).Select(static entry => entry.Index).ToArray();
                throw CliErrors.NotFoundAt(
                    WordsDiagnostics.BlockNotFound,
                    "block",
                    request.Blocks.Text,
                    index.Count,
                    $"Section {section} holds block{(held.Length == 1 ? "" : "s")} {PageRange.Describe(held)}; "
                    + "use a range inside it, or drop --blocks to read the whole section.");
            }
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

        return new DocumentReadResult
        {
            Source = InfoProjection.Source(path, loaded),
            Scope = request.Scope,
            Blocks = blocks,
            BlockCount = index.Count,
            Window = new ResultWindow
            {
                Unit = "block",
                Returned = blocks.Count,
                Total = candidates.Count,
                Truncated = blocks.Count < candidates.Count || blocks.Any(static block => block.ContentTruncated),
            },
        };
    }

    private static BlockData ProjectBlock(BlockEntry entry, string scope, ref int remaining)
    {
        bool truncated = false;
        if (entry.Node is Paragraph paragraph)
        {
            string visible = WordsText.Of(paragraph, listNumbers: false);
            string text = AfterLeadingBreaks(visible);
            // The runs leave out the leading page breaks the text leaves out.
            int skipped = visible.Length - text.Length;
            text = Take(text, ref remaining, ref truncated);
            var runs = scope == "full" ? new List<RunData>() : null;
            if (runs is not null)
            {
                foreach (Run run in WordsText.VisibleRuns(paragraph))
                {
                    string runText = run.Text[Math.Min(skipped, run.Text.Length)..];
                    skipped -= run.Text.Length - runText.Length;
                    if (run.Text.Length > 0 && runText.Length == 0)
                    {
                        continue;
                    }
                    if (runs.Count == 500 || remaining == 0 && runText.Length > 0)
                    {
                        truncated = true;
                        break;
                    }
                    runs.Add(new RunData
                    {
                        Text = Take(runText, ref remaining, ref truncated),
                        Font = run.Font.Name,
                        LatinFont = run.Font.NameAscii,
                        EastAsianFont = run.Font.NameFarEast,
                        Size = run.Font.Size,
                        Bold = run.Font.Bold,
                        Italic = run.Font.Italic,
                        Color = $"#{run.Font.Color.ToArgb() & 0xffffff:X6}",
                    });
                }
            }
            return new BlockData
            {
                Block = entry.Index,
                Type = "paragraph",
                Section = entry.Section,
                ListLabel = WordsText.ListNumber(paragraph),
                Text = text,
                Style = paragraph.ParagraphFormat.StyleName,
                HeadingLevel = InfoProjection.HeadingLevel(paragraph),
                Runs = runs,
                ParagraphFormat = runs is null ? null : FormatOf(paragraph.ParagraphFormat),
                Images = paragraph.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static shape => shape.HasImage)
                    .Select(shape => new BlockImageData
                    {
                        Block = entry.Index,
                        Name = shape.Name,
                        Width = shape.Width,
                        Height = shape.Height,
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
            Block = entry.Index,
            Type = "table",
            Section = entry.Section,
            RowCount = table.Rows.Count,
            ColumnCount = table.Rows.Count == 0 ? 0 : table.Rows.Cast<Row>().Max(static row => row.Cells.Count),
            Cells = rows,
            ContentTruncated = truncated,
        };
    }

    private static ParagraphFormatData FormatOf(ParagraphFormat format) => new()
    {
        Alignment = JsonNamingPolicy.CamelCase.ConvertName(format.Alignment.ToString()),
        LeftIndent = format.LeftIndent,
        RightIndent = format.RightIndent,
        FirstLineIndent = format.FirstLineIndent,
        SpaceBefore = format.SpaceBefore,
        SpaceAfter = format.SpaceAfter,
        LineSpacingRule = JsonNamingPolicy.CamelCase.ConvertName(format.LineSpacingRule.ToString()),
        // The SDK states a multiple in points of 12 per line.
        LineSpacing = format.LineSpacingRule == LineSpacingRule.Multiple ? format.LineSpacing / 12 : format.LineSpacing,
    };

    private static string Take(string value, ref int remaining, ref bool truncated)
    {
        int length = Math.Min(value.Length, remaining);
        truncated |= length < value.Length;
        remaining -= length;
        return value[..length];
    }

    /// <summary>
    /// The break that ends a paragraph block: a page break inside it, or one that starts the next
    /// paragraph (page break before, or a page break before its text), or a section break when
    /// it is the last block of a section.
    /// </summary>
    private static string? BreakAfter(Paragraph paragraph)
    {
        if (AfterLeadingBreaks(RunText(paragraph)).Contains(ControlChar.PageBreakChar, StringComparison.Ordinal)
            || paragraph.NextSibling is Paragraph next && StartsPage(next))
        {
            return WordsBreakKinds.Page;
        }

        return paragraph.IsEndOfSection && paragraph.ParentSection?.NextSibling is Section ? WordsBreakKinds.Section : null;
    }

    private static bool StartsPage(Paragraph paragraph)
    {
        string text = RunText(paragraph);
        return paragraph.ParagraphFormat.PageBreakBefore || AfterLeadingBreaks(text).Length < text.Length;
    }

    private static string RunText(Paragraph paragraph) => string.Concat(paragraph.Runs.Cast<Run>().Select(static run => run.Text));

    // Page breaks before a paragraph's text, which documents converted from PDF have, start its
    // page like page break before does, so they end the block before it. A paragraph of page
    // breaks alone keeps them.
    private static string AfterLeadingBreaks(string text) =>
        text.TrimStart(ControlChar.PageBreakChar) is { Length: > 0 } rest ? rest : text;
}
