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

/// <summary>Shared PDF split, extraction, creation, and output artifact helpers.</summary>
internal static class PdfArtifactSupport
{
    internal static IReadOnlyList<PdfExtractedItem> ExtractImages(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard)
    {
        var items = new List<PdfExtractedItem>();
        int number = 0;
        foreach (int pageNumber in pages)
        {
            var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
            document.Pages[pageNumber].Accept(absorber);
            foreach (ImagePlacement placement in absorber.ImagePlacements)
            {
                using var stream = new MemoryStream();
                placement.Save(stream, DrawingImageFormat.Png);
                byte[] bytes = stream.ToArray();
                string path = guard.WriteAllBytes($"image-{++number:000}.png", bytes);
                items.Add(new PdfExtractedItem
                {
                    Path = path,
                    Kind = "image",
                    SizeBytes = bytes.LongLength,
                    Page = pageNumber,
                });
            }
        }

        return items;
    }

    internal static IReadOnlyList<PdfExtractedItem> ExtractAttachments(
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

    internal static IReadOnlyList<PdfExtractedItem> ExtractTextArtifact(
        Document document,
        IReadOnlyList<int> pages,
        ExtractionGuard guard)
    {
        var text = new StringBuilder();
        foreach (int pageNumber in pages)
        {
            if (text.Length > 0)
            {
                text.Append('\f').AppendLine();
            }

            text.Append(ExtractText(document.Pages[pageNumber], PdfReadModes.Plain));
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text.ToString());
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

    internal static IReadOnlyList<PdfExtractedItem> ExtractTables(
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

    internal static string Csv(string value)
    {
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.IndexOfAny([',', '"', '\n']) >= 0
            ? "\"" + normalized.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : normalized;
    }

    internal static IReadOnlyList<SplitPart> SplitParts(Document document, PdfSplitRequest request)
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
            .Select(item => (Title: item.Title ?? string.Empty, Page: DestinationPage(item)))
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

    internal static int DestinationPage(OutlineItemCollection item)
    {
        if (item.Destination is ExplicitDestination direct)
        {
            return direct.PageNumber;
        }

        return item.Action is GoToAction { Destination: ExplicitDestination action }
            ? action.PageNumber
            : 0;
    }

    internal static string SplitName(string template, string stem, SplitPart part)
    {
        string bookmark = SafeName(part.Bookmark ?? string.Empty);
        string expanded = template
            .Replace("{stem}", SafeName(stem), StringComparison.Ordinal)
            .Replace("{n}", part.Index.ToString("000", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{pages}", SafeName(PageRangeText(part.Pages)), StringComparison.Ordinal)
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

    internal static string SafeName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(character =>
            invalid.Contains(character) || char.IsControl(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized.Trim().TrimEnd('.');
    }

    internal static (double Width, double Height) PageDimensions(string id) => id.ToUpperInvariant() switch
    {
        "A3" => (841.89, 1190.55),
        "A4" => (595.28, 841.89),
        "LETTER" => (612, 792),
        "LEGAL" => (612, 1008),
        _ => throw CliErrors.OptionInvalid("--page-size", $"unknown size '{id}'", "Use A3, A4, Letter or Legal."),
    };

    internal static MarginInfo Margin(PdfMargins margins) => new()
    {
        Top = margins.Top,
        Right = margins.Right,
        Bottom = margins.Bottom,
        Left = margins.Left,
    };

    internal static void ValidateMargins(PdfMargins margins, double width, double height)
    {
        if (margins.Top < 0 || margins.Right < 0 || margins.Bottom < 0 || margins.Left < 0
            || margins.Left + margins.Right >= width
            || margins.Top + margins.Bottom >= height)
        {
            throw CliErrors.OptionInvalid(
                "--margins",
                "values must be non-negative and leave a positive content area",
                "Use smaller top,right,bottom,left values in points.");
        }
    }

    internal static IReadOnlyList<SourceInfo> CreationInputs(NewPdfRequest request)
    {
        IEnumerable<string> paths = request.ImagePaths
            ?? (request.HtmlPath is not null ? [request.HtmlPath] : [request.TextPath!]);
        return paths.Select(path => new SourceInfo
        {
            Path = Path.GetFullPath(path),
            Format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
            SizeBytes = new FileInfo(path).Length,
        }).ToArray();
    }

    internal static void EnsureCreationInputs(
        ResourceBudgetLedger resourceBudgets,
        NewPdfRequest request)
    {
        IReadOnlyList<string> paths = request.ImagePaths
            ?? (request.HtmlPath is not null ? [request.HtmlPath] : [request.TextPath!]);
        if (paths.Count > 1000)
        {
            throw CliErrors.OptionInvalid(
                "--from-images",
                "more than 1000 inputs were requested",
                "Create smaller PDFs and merge them in bounded batches.");
        }

        foreach (string path in paths)
        {
            InputSizeGuard.Ensure(resourceBudgets, path);
        }
    }

    internal static void EnsurePdfOutput(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                "--out",
                $"PDF output must use the .pdf extension: '{path}'",
                "Choose a path ending in .pdf.");
        }
    }

    internal static void CopyOutline(
        IEnumerable<OutlineItemCollection> source,
        OutlineCollection target,
        Document document,
        int pageOffset)
    {
        foreach (OutlineItemCollection item in source)
        {
            var copied = CopyOutlineItem(item, document, pageOffset);
            target.Add(copied);
        }
    }

    internal static OutlineItemCollection CopyOutlineItem(
        OutlineItemCollection source,
        Document document,
        int pageOffset)
    {
        var copied = new OutlineItemCollection(document.Outlines)
        {
            Title = source.Title,
            Bold = source.Bold,
            Italic = source.Italic,
            Color = source.Color,
        };
        int page = DestinationPage(source);
        if (page > 0)
        {
            copied.Destination = new FitExplicitDestination(document.Pages[pageOffset + page]);
        }

        foreach (OutlineItemCollection child in source)
        {
            copied.Add(CopyOutlineItem(child, document, pageOffset));
        }

        return copied;
    }

    internal static IReadOnlyList<Warning>? OutputWarnings(LicenseState state) =>
        state == LicenseState.Evaluation ? [EnvelopeParts.EvaluationWatermark] : null;

    internal sealed record SplitPart(int Index, IReadOnlyList<int> Pages, string? Bookmark);

    internal sealed class HtmlResourceLoader(LocalDocumentResourceLoader resources)
    {
        public LoadOptions.ResourceLoadingResult Load(string resourceUri)
        {
            if (resources.TryRead(resourceUri, out byte[] data))
            {
                return new LoadOptions.ResourceLoadingResult(data);
            }

            return new LoadOptions.ResourceLoadingResult(Array.Empty<byte>())
            {
                // Cancelling the custom loader would enable the SDK default loader.
                LoadingCancelled = false,
            };
        }
    }
}

