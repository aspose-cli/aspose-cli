using System.Text.Json;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Tables;
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
        // Images are copied as the document stores them, once the evaluation watermark is skipped.
        using ExtractionGuard guard = session.Outputs.BeginExtraction(
            request.Output.Path,
            request.Output.Overwrite,
            request.What == WordsExtractTargets.Images ? ExtractionContent.EmbeddedFiles : ExtractionContent.Derived);
        var index = new DocumentBlockIndex(loaded.Document, loaded.Evaluation);
        var outputs = new List<WordsExtractedOutput>();
        var warnings = new List<Warning>();
        if (request.What == WordsExtractTargets.Images)
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

                // The watermark picture an evaluation save draws is no image of the document.
                if (loaded.Evaluation && WordsEvaluation.IsWatermark(shape))
                {
                    continue;
                }

                byte[] bytes = shape.ImageData.ImageBytes;
                string extension = FileFormatUtil.ImageTypeToExtension(shape.ImageData.ImageType);
                PublishedFile file = guard.StageAllBytes($"image-{++number:000}{extension}", bytes);
                outputs.Add(new WordsExtractedOutput(FileObjects.Companion(file)) { Block = index.FindBlock(shape) });
            }

            if (linked > 0)
            {
                warnings.Add(new Warning(WordsDiagnostics.LinkedImagesSkipped, $"{linked} linked image(s) store no bytes in the document and were not extracted.")
                {
                    Hint = "Embed linked images in the source document to extract them.",
                    AffectsCompleteness = true,
                });
            }
        }
        else if (request.What == WordsExtractTargets.Comments)
        {
            IReadOnlyList<WordsCommentData> comments = loaded.Document.GetChildNodes(NodeType.Comment, true).Cast<Comment>()
                .Select(comment => new WordsCommentData { Author = comment.Author, Text = WordsText.Of(comment), Block = index.FindBlock(comment) }).ToArray();
            string json = JsonSerializer.Serialize(
                comments,
                ProductJsonContext.Definition.LocalOptions);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            PublishedFile file = guard.StageAllBytes("comments.json", bytes);
            outputs.Add(new WordsExtractedOutput(FileObjects.Companion(file)));
        }
        else if (request.What == WordsExtractTargets.Text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(WordsText.Lines(index.Entries.Select(static entry => entry.Node)));
            PublishedFile file = guard.StageAllBytes("document.txt", bytes);
            outputs.Add(new WordsExtractedOutput(FileObjects.Companion(file)));
        }
        else if (request.What == WordsExtractTargets.Tables)
        {
            int number = 0;
            foreach (BlockEntry entry in index.Entries.Where(static entry => entry.Node is Table))
            {
                byte[] bytes = Csv((Table)entry.Node, request.ByteOrderMark);
                PublishedFile file = guard.StageAllBytes($"table-{++number:000}.csv", bytes);
                outputs.Add(new WordsExtractedOutput(FileObjects.Companion(file)) { Block = entry.Index });
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
            Outputs = outputs,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(InputWarnings(loaded), warnings),
        };
    }

    /// <summary>
    /// A table as RFC 4180 CSV in UTF-8, with a byte order mark when asked, which spreadsheet
    /// programs need to read it as UTF-8: one record per row and one field per cell, the cell's visible text
    /// with a line break between its paragraphs and for each manual line break. Merged cells stay as the document stores them:
    /// a cell spanning columns can be one field, so rows can have different field counts, and a
    /// cell a merge covers is an empty field.
    /// </summary>
    private static byte[] Csv(Table table, bool byteOrderMark) => CsvText.Encode(
        table.Rows.Cast<Row>().Select(static row => row.Cells.Select(static cell => WordsText.LineBreaks(WordsText.Of(cell), "\n"))),
        byteOrderMark);
}
