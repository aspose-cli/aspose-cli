using System.Globalization;
using System.Net;
using System.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using DrawingImageFormat = Aspose.Pdf.Drawing.ImageFormat;


namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns transactional PDF splitting and bounded artifact extraction.</summary>
internal sealed class PdfExtractionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly SafeFileWriter _writer;
    private readonly PdfDocumentLoader _loader;

    internal PdfExtractionService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _resourceBudgets = resourceBudgets;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _loader = loader;
    }

    internal PdfSplitResult Split(string filePath, PdfSplitRequest request)
    {
        int modes = request.PageGroups is { Count: > 0 } ? 1 : 0;
        modes += request.Every.HasValue ? 1 : 0;
        modes += request.ByBookmarks ? 1 : 0;
        if (modes != 1)
        {
            throw CliErrors.Usage(["Choose exactly one of --pages, --every or --by-bookmarks."]);
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<SplitPart> parts = SplitParts(loaded.Document, request);
        string root = Path.GetFullPath(request.OutputDirectory);
        using var writer = new AtomicOutputSetWriter(_writer, root, "pdf-split");
        string stem = Path.GetFileNameWithoutExtension(filePath);
        var targets = new List<(SplitPart Part, string Path)>();
        var names = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (SplitPart part in parts)
        {
            string name = SplitName(request.NameTemplate, stem, part);
            if (!names.Add(name))
            {
                throw CliErrors.OptionInvalid(
                    "--name-template",
                    $"produces duplicate output '{name}'",
                    "Include {n}, {pages} or {bookmark} so every output name is unique.");
            }
            string target = Path.Combine(root, name);
            writer.Stage(target, request.Overwrite, staged =>
            {
                using Document selected = Select(loaded.Document, part.Pages);
                selected.Save(staged);
            });
            targets.Add((part, target));
        }

        IReadOnlyList<long> sizes = writer.Commit();
        return new PdfSplitResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = targets.Select((item, index) => new PdfSplitOutput
            {
                Index = item.Part.Index,
                Pages = PageRangeText(item.Part.Pages),
                Bookmark = item.Part.Bookmark,
                Output = BuildOutput(item.Path, "pdf", sizes[index]),
            }).ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.OutputWarnings(state),
        };
    }

    internal PdfExtractResult Extract(string filePath, PdfExtractRequest request)
    {
        if (!PdfExtractKinds.All.Contains(request.What, StringComparer.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "--what",
                $"unknown extraction kind '{request.What}'",
                "Use images, attachments, text or tables.");
        }

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        using var guard = new ExtractionGuard(
            _resourceBudgets,
            request.OutputDirectory,
            request.Overwrite);
        IReadOnlyList<PdfExtractedItem> items = request.What switch
        {
            "images" => ExtractImages(loaded.Document, pages, guard, _resourceBudgets),
            "attachments" => ExtractAttachments(loaded.Document, guard),
            "text" => ExtractTextArtifact(loaded.Document, pages, guard),
            "tables" => ExtractTables(loaded.Document, pages, guard),
            _ => throw new InvalidOperationException("The extraction registry and handler are out of sync."),
        };
        guard.Commit();
        return new PdfExtractResult
        {
            Input = PdfInfoProjection.Source(filePath),
            What = request.What,
            Items = items,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.OutputWarnings(state),
        };
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractImages(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard,
        ResourceBudgetLedger budgets)
    {
        var items = new List<PdfExtractedItem>();
        int number = 0;
        foreach (int pageNumber in pages)
        {
            var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
            document.Pages[pageNumber].Accept(absorber);
            foreach (ImagePlacement placement in absorber.ImagePlacements)
            {
                // The image's own dimensions bound the decode, so check them before decoding.
                // The SDK's PNG encoder resizes its target stream, which the budgeted
                // extraction stream does not allow, so the bounded image is encoded in memory
                // and then written through the extraction budget.
                XImage image = placement.Image;
                RenderPixelGuard.EnsureFits(budgets, image.Width, image.Height, dpi: null,
                    hint: $"The image on page {pageNumber} is too large to decode; extract the other pages with --pages.");

                using var encoded = new MemoryStream();
                placement.Save(encoded, DrawingImageFormat.Png);
                byte[] bytes = encoded.ToArray();
                items.Add(new PdfExtractedItem
                {
                    Path = guard.WriteAllBytes($"image-{++number:000}.png", bytes),
                    Kind = "image",
                    SizeBytes = bytes.LongLength,
                    Page = pageNumber,
                });
            }
        }

        return items;
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractAttachments(
        Document document,
        ExtractionGuard guard)
    {
        var items = new List<PdfExtractedItem>();
        foreach (FileSpecification file in document.EmbeddedFiles.OrderBy(
                     static item => item.UnicodeName ?? item.Name,
                     StringComparer.Ordinal))
        {
            string name = file.UnicodeName ?? file.Name ?? "attachment.bin";
            long declaredLength = file.Contents.CanSeek
                ? file.Contents.Length
                : 0;
            long writtenLength = 0;
            string path = guard.Write(
                name,
                declaredLength,
                output =>
                {
                    // budget-allow: ExtractionGuard supplies a BudgetWriteStream
                    // bounded by the remaining extraction byte budget.
                    file.Contents.CopyTo(output);
                    writtenLength = output.Length;
                },
                flatten: true);
            items.Add(new PdfExtractedItem
            {
                Path = path,
                Kind = "attachment",
                SizeBytes = writtenLength,
                Name = name,
            });
        }

        return items;
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractTextArtifact(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(DocumentText(document, pages));
        string path = guard.WriteAllBytes("document.txt", bytes);
        return
        [
            new PdfExtractedItem
            {
                Path = path,
                Kind = "text",
                SizeBytes = bytes.LongLength,
            },
        ];
    }

    private static IReadOnlyList<PdfExtractedItem> ExtractTables(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard)
    {
        var items = new List<PdfExtractedItem>();
        int number = 0;
        foreach (int pageNumber in pages)
        {
            Page page = document.Pages[pageNumber];
            var absorber = new TableAbsorber();
            absorber.Visit(page);
            foreach (AbsorbedTable table in absorber.TableList)
            {
                var csv = new StringBuilder();
                foreach (AbsorbedRow row in table.RowList)
                {
                    csv.AppendLine(string.Join(
                        ",",
                        row.CellList.Select(static cell => Csv(string.Concat(
                            cell.TextFragments.Select(static fragment => fragment.Text))))));
                }

                byte[] bytes = Encoding.UTF8.GetBytes(csv.ToString());
                string path = guard.WriteAllBytes($"table-{++number:000}.csv", bytes);
                items.Add(new PdfExtractedItem
                {
                    Path = path,
                    Kind = "table",
                    SizeBytes = bytes.LongLength,
                    Page = pageNumber,
                    Rect = ToContractRect(page, table.Rectangle),
                    Confidence = 0.5,
                });
            }
        }

        return items;
    }

    private static string Csv(string value)
    {
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.IndexOfAny([',', '"', '\n']) >= 0
            ? "\"" + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : normalized;
    }

    private static IReadOnlyList<SplitPart> SplitParts(Document document, PdfSplitRequest request)
    {
        if (request.PageGroups is { Count: > 0 } groups)
        {
            return groups.Select((group, index) =>
                new SplitPart(index + 1, group.Resolve(document.Pages.Count), null)).ToArray();
        }

        if (request.Every is int every)
        {
            if (every < 1)
            {
                throw CliErrors.OptionInvalid("--every", "must be at least 1", "Pass a positive page count.");
            }

            return Enumerable.Range(1, document.Pages.Count)
                .Chunk(every)
                .Select((pages, index) => new SplitPart(index + 1, pages, null))
                .ToArray();
        }

        var bookmarks = document.Outlines
            .Select(item => (Title: item.Title ?? string.Empty, Page: PdfNavigationCensus.DestinationPage(item)))
            .Where(static item => item.Page > 0)
            .OrderBy(static item => item.Page)
            .GroupBy(static item => item.Page)
            .Select(static group => group.First())
            .ToArray();
        if (bookmarks.Length == 0)
        {
            throw CliErrors.OptionInvalid(
                "--by-bookmarks",
                "the document has no top-level bookmarks with page destinations",
                "Use --pages or --every, or add bookmarks first.");
        }

        var parts = new List<SplitPart>();
        if (bookmarks[0].Page > 1)
        {
            parts.Add(new SplitPart(
                parts.Count + 1,
                Enumerable.Range(1, bookmarks[0].Page - 1).ToArray(),
                null));
        }

        int bookmarkIndexOffset = parts.Count;
        parts.AddRange(bookmarks.Select((bookmark, index) =>
        {
            int end = index + 1 < bookmarks.Length ? bookmarks[index + 1].Page - 1 : document.Pages.Count;
            int[] pages = Enumerable.Range(bookmark.Page, end - bookmark.Page + 1).ToArray();
            return new SplitPart(bookmarkIndexOffset + index + 1, pages, bookmark.Title);
        }));
        return parts;
    }

    private static string SplitName(string template, string stem, SplitPart part)
    {
        string bookmark = SafeName(part.Bookmark ?? string.Empty);
        string expanded = template
            .Replace("{stem}", SafeName(stem), StringComparison.Ordinal)
            .Replace("{n}", part.Index.ToString("000", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{pages}", PageRangeText(part.Pages).Replace(',', '_'), StringComparison.Ordinal)
            .Replace("{bookmark}", bookmark, StringComparison.Ordinal);
        if (expanded.Contains('{', StringComparison.Ordinal) || expanded.Contains('}', StringComparison.Ordinal)
            || !string.Equals(expanded, Path.GetFileName(expanded), StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(expanded), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                "--name-template",
                $"produces unsafe PDF name '{expanded}'",
                "Use a simple .pdf file name with {stem}, {n}, {pages} or {bookmark}.");
        }

        return expanded;
    }

    private static string SafeName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(character =>
            invalid.Contains(character) || char.IsControl(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized.Trim().TrimEnd('.');
    }

    private sealed record SplitPart(int Index, IReadOnlyList<int> Pages, string? Bookmark);
}
