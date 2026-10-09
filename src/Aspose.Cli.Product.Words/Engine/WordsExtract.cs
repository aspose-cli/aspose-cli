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

/// <summary>Serves <c>words extract</c>: bounded document assets written into a guarded directory.</summary>
internal static class WordsExtract
{
    /// <summary>Extracts bounded document artifacts into a guarded directory.</summary>
    internal static WordsExtractResult Run(WordsSession session, WordsExtractRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedDocument loaded = session.Loader.Open(request.Input, request.Password);
        using ExtractionGuard guard = session.Outputs.BeginExtraction(request.Output.Path, request.Output.Overwrite);
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
            Input = InfoProjection.Source(request.Input, loaded),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(InputWarnings(loaded), warnings),
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
            csv.AppendJoin(',', row.Cells.Select(static cell => Field(WordsText.LineBreaks(WordsText.Of(cell), "\n"))));
            csv.Append("\r\n");
        }

        return [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(csv.ToString())];

        static string Field(string text) =>
            text.AsSpan().IndexOfAny(",\"\r\n") < 0 ? text : $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
