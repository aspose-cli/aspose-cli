using System.Text.Json;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Tables;
using ContractCommentData = Aspose.Cli.Product.Words.Contracts.CommentData;
using static Aspose.Cli.Product.Words.Engine.WordsEngineSupport;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Owns atomic document splitting and bounded artifact extraction.</summary>
internal sealed class WordsExtractionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SafeFileWriter _writer;
    private readonly WordsDocumentLoader _loader;
    private readonly ResourceBudgetLedger _resourceBudgets;

    internal WordsExtractionService(
        ILicenseGate licenseGate,
        SafeFileWriter writer,
        WordsDocumentLoader loader,
        ResourceBudgetLedger resourceBudgets)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
    }

    /// <summary>Splits a document and commits all outputs atomically.</summary>
    internal WordsSplitResult Split(string filePath, WordsSplitRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        using var writer = new WordsSplitWriter(_writer, request.OutputDirectory, request.Overwrite);
        if (request.By == "section")
        {
            for (int index = 0; index < loaded.Document.Sections.Count; index++)
            {
                Document part = loaded.Document.Clone();
                for (int remove = part.Sections.Count - 1; remove >= 0; remove--)
                {
                    if (remove != index)
                    {
                        part.Sections.RemoveAt(remove);
                    }
                }

                writer.Stage(part, index + 1, $"section-{index + 1}");
            }
        }
        else if (request.By == "pages")
        {
            IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.PageCount)
                ?? Enumerable.Range(1, loaded.Document.PageCount).ToArray();
            int outputIndex = 0;
            foreach (int page in pages)
            {
                Document part = loaded.Document.ExtractPages(page - 1, 1);
                writer.Stage(part, ++outputIndex, $"page-{page}");
            }
        }
        else if (request.By == "heading1")
        {
            SplitByHeading(loaded, writer);
        }
        else
        {
            throw CliErrors.OptionInvalid("--by", $"unknown split mode '{request.By}'", "Use section, heading1 or pages.");
        }

        IReadOnlyList<SplitOutput> outputs = writer.Commit();
        return new WordsSplitResult
        {
            Input = InfoProjection.Source(filePath, loaded),
            Outputs = outputs,
            License = EnvelopeParts.License(state),
            Warnings = request.By == "pages"
                ? EnvelopeParts.CombineWarnings(OutputWarnings(state, loaded, "docx"), [new Warning { Code = WordsDiagnostics.LayoutMayDiffer, Message = "Page extraction can slightly reflow complex layouts.", Hint = "Visually inspect the split pages." }])
                : OutputWarnings(state, loaded, "docx"),
        };
    }

    /// <summary>Extracts bounded document artifacts into a guarded directory.</summary>
    internal WordsExtractResult Extract(string filePath, WordsExtractRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        using var guard = new ExtractionGuard(_resourceBudgets, request.OutputDirectory, request.Overwrite);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        var items = new List<ExtractedItem>();
        var warnings = new List<Warning>();
        if (request.What == "images")
        {
            int number = 0;
            int linked = 0;
            foreach (Shape shape in loaded.Document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static s => s.HasImage))
            {
                // A link-only image has no stored bytes; reading them would fetch the link.
                if (shape.ImageData.IsLinkOnly)
                {
                    linked++;
                    continue;
                }

                byte[] bytes = shape.ImageData.ImageBytes;
                string extension = FileFormatUtil.ImageTypeToExtension(shape.ImageData.ImageType);
                string path = guard.WriteAllBytes($"image-{++number:000}{extension}", bytes);
                items.Add(new ExtractedItem { Path = path, Kind = "image", SizeBytes = bytes.LongLength, Block = index.FindBlock(shape) });
            }

            if (linked > 0)
            {
                warnings.Add(new Warning
                {
                    Code = WordsDiagnostics.LinkedImagesSkipped,
                    Message = $"{linked} linked image(s) store no bytes in the document and were not extracted.",
                    Hint = "Embed linked images in the source document to extract them.",
                    AffectsCompleteness = true,
                });
            }
        }
        else if (request.What == "comments")
        {
            IReadOnlyList<ContractCommentData> comments = loaded.Document.GetChildNodes(NodeType.Comment, true).Cast<Comment>()
                .Select(comment => new ContractCommentData { Author = comment.Author, Text = WordsText.Of(comment), Block = index.FindBlock(comment) }).ToArray();
            string json = JsonSerializer.Serialize(
                comments,
                ProductJsonContext.Definition.LocalOptions);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            string path = guard.WriteAllBytes("comments.json", bytes);
            items.Add(new ExtractedItem { Path = path, Kind = "comments", SizeBytes = bytes.LongLength });
        }
        else if (request.What == "text")
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(WordsText.Lines(index.Entries.Select(static entry => entry.Node)));
            string path = guard.WriteAllBytes("document.txt", bytes);
            items.Add(new ExtractedItem { Path = path, Kind = "text", SizeBytes = bytes.LongLength });
        }
        else if (request.What == "tables")
        {
            int number = 0;
            foreach (BlockEntry entry in index.Entries.Where(static entry => entry.Node is Table))
            {
                byte[] bytes = Csv((Table)entry.Node);
                string path = guard.WriteAllBytes($"table-{++number:000}.csv", bytes);
                items.Add(new ExtractedItem { Path = path, Kind = "table", SizeBytes = bytes.LongLength, Block = entry.Index });
            }
        }
        else
        {
            throw CliErrors.OptionInvalid("--what", $"unknown extraction kind '{request.What}'", "Use images, comments, text or tables.");
        }

        guard.Commit();
        return new WordsExtractResult
        {
            Input = InfoProjection.Source(filePath, loaded),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(EnvelopeParts.CombineWarnings(EnvelopeParts.OutputWarnings(state), InputWarnings(loaded)), warnings),
        };
    }

    /// <summary>
    /// A table as RFC 4180 CSV in UTF-8 with a byte order mark, which spreadsheet programs need
    /// to read it as UTF-8: one record per row and one field per cell, the cell's visible text
    /// with a line break between its paragraphs and for each manual line break. Merged cells stay as the document stores them:
    /// a cell spanning columns can be one field, so rows can have different field counts, and a
    /// cell a merge covers is an empty field.
    /// </summary>
    private static byte[] Csv(Table table)
    {
        var csv = new System.Text.StringBuilder();
        foreach (Row row in table.Rows)
        {
            csv.AppendJoin(',', row.Cells.Select(static cell => Field(WordsText.Of(cell).TrimEnd(ControlChar.ParagraphBreakChar).Replace(ControlChar.ParagraphBreakChar, '\n').Replace(ControlChar.LineBreakChar, '\n'))));
            csv.Append("\r\n");
        }

        return [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(csv.ToString())];

        static string Field(string text) =>
            text.AsSpan().IndexOfAny(",\"\r\n") < 0 ? text : $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    /// <summary>
    /// Writes one part per Heading 1, and a leading part for the blocks before the first one
    /// (a title page or table of contents), so no block is left out. Each part is a copy of
    /// the whole document with the other blocks removed, so it keeps its sections' page
    /// setup, headers, footers and styles.
    /// </summary>
    private static void SplitByHeading(LoadedDocument loaded, WordsSplitWriter writer)
    {
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        List<int> starts = index.Entries.Where(static entry => entry.Node is Paragraph p && InfoProjection.HeadingLevel(p) == 1)
            .Select(static entry => entry.Index).ToList();
        if (starts.Count == 0)
        {
            throw CliErrors.NotFoundAt(WordsDiagnostics.AnchorNotFound, "Heading 1 paragraph", "1", 0,
                "Use --by section or --by pages, or apply Heading 1 styles first.");
        }

        if (starts[0] > 1)
        {
            starts.Insert(0, 1);
        }

        for (int group = 0; group < starts.Count; group++)
        {
            int start = starts[group];
            int end = group + 1 < starts.Count ? starts[group + 1] - 1 : index.Count;
            Document part = loaded.Document.Clone();
            var partIndex = new DocumentBlockIndex(part, loaded.Evaluation);
            foreach (BlockEntry entry in partIndex.Entries.Where(entry => entry.Index < start || entry.Index > end))
            {
                DocumentBlockIndex.Remove(entry.Node);
            }

            foreach (Section section in part.Sections.Cast<Section>().ToArray())
            {
                if (part.Sections.Count > 1 && !section.Body.HasChildNodes)
                {
                    section.Remove();
                }
            }

            part.EnsureMinimum();
            writer.Stage(part, group + 1, $"blocks-{start}-{end}");
            part.Cleanup();
        }
    }
}
