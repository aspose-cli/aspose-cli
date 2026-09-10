using System.Text.Json;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Contracts.Serialization;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Product.Words.Engine.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Drawing;
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
    internal WordsSplitResult Split(string filePath, WordsSplitRequest request) =>
        WordsErrorTranslator.Execute("split", () => SplitCore(filePath, request));

    /// <summary>Extracts bounded document artifacts into a guarded directory.</summary>
    internal WordsExtractResult Extract(string filePath, WordsExtractRequest request) =>
        WordsErrorTranslator.Execute("extract", () => ExtractCore(filePath, request));

    private WordsSplitResult SplitCore(string filePath, WordsSplitRequest request)
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
            SplitByHeading(loaded.Document, writer);
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
                ? Combine(OutputWarnings(state, loaded, "docx"), [new Warning { Code = WordsDiagnostics.LayoutMayDiffer, Message = "Page extraction can slightly reflow complex layouts.", Hint = "Visually inspect the split pages." }])
                : OutputWarnings(state, loaded, "docx"),
        };
    }

    private WordsExtractResult ExtractCore(string filePath, WordsExtractRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);
        using var guard = new ExtractionGuard(_resourceBudgets, request.OutputDirectory);
        var index = new DocumentBlockIndex(loaded.Document);
        var items = new List<ExtractedItem>();
        if (request.What == "images")
        {
            int number = 0;
            foreach (Shape shape in loaded.Document.GetChildNodes(NodeType.Shape, true).Cast<Shape>().Where(static s => s.HasImage))
            {
                byte[] bytes = shape.ImageData.ImageBytes;
                string extension = FileFormatUtil.ImageTypeToExtension(shape.ImageData.ImageType);
                string path = guard.WriteAllBytes($"image-{++number:000}{extension}", bytes);
                items.Add(new ExtractedItem { Path = path, Kind = "image", SizeBytes = bytes.LongLength, Block = index.FindBlock(shape) });
            }
        }
        else if (request.What == "comments")
        {
            ContractCommentData[] comments = loaded.Document.GetChildNodes(NodeType.Comment, true).Cast<Comment>()
                .Select(comment => new ContractCommentData { Author = comment.Author, Text = InfoProjection.Clean(comment.GetText()), Block = index.FindBlock(comment) }).ToArray();
            string json = JsonSerializer.Serialize(
                comments,
                ProductJsonContext.Definition.LocalOptions);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            string path = guard.WriteAllBytes("comments.json", bytes);
            items.Add(new ExtractedItem { Path = path, Kind = "comments", SizeBytes = bytes.LongLength });
        }
        else if (request.What == "text")
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(InfoProjection.Clean(loaded.Document.GetText()));
            string path = guard.WriteAllBytes("document.txt", bytes);
            items.Add(new ExtractedItem { Path = path, Kind = "text", SizeBytes = bytes.LongLength });
        }
        else
        {
            throw CliErrors.OptionInvalid("--what", $"unknown extraction kind '{request.What}'", "Use images, comments or text.");
        }

        guard.Commit();
        return new WordsExtractResult
        {
            Input = InfoProjection.Source(filePath, loaded),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
            Warnings = Combine(EnvelopeParts.OutputWarnings(state), InputWarnings(loaded)),
        };
    }

    private static void SplitByHeading(Document document, WordsSplitWriter writer)
    {
        var index = new DocumentBlockIndex(document);
        List<int> starts = index.Entries.Where(static entry => entry.Node is Paragraph p && InfoProjection.HeadingLevel(p) == 1)
            .Select(static entry => entry.Index).ToList();
        if (starts.Count == 0)
        {
            throw new CliException(WordsDiagnostics.AnchorNotFound, "No Heading 1 paragraph was found.", hint: "Use --by section/pages, or apply Heading 1 styles first.");
        }

        for (int group = 0; group < starts.Count; group++)
        {
            int start = starts[group];
            int end = group + 1 < starts.Count ? starts[group + 1] - 1 : index.Count;
            var part = new Document();
            part.FirstSection.Body.RemoveAllChildren();
            for (int block = start; block <= end; block++)
            {
                Node imported = part.ImportNode(index.Get(block).Node, true, ImportFormatMode.KeepSourceFormatting);
                part.FirstSection.Body.AppendChild(imported);
            }

            part.EnsureMinimum();
            writer.Stage(part, group + 1, $"blocks-{start}-{end}");
        }
    }
}
