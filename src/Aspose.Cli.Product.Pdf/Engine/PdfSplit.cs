using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;


namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Splits a PDF transactionally by ranges, fixed groups or bookmarks: <c>pdf split</c>.</summary>
internal static class PdfSplit
{
    internal static PdfSplitResult Run(PdfSession session, PdfSplitRequest request)
    {
        string filePath = request.Input;
        int modes = request.PageGroups is { Count: > 0 } ? 1 : 0;
        modes += request.Every.HasValue ? 1 : 0;
        modes += request.ByBookmarks ? 1 : 0;
        if (modes != 1)
        {
            throw CliErrors.Usage(["Choose exactly one of --pages, --every or --by-bookmarks."]);
        }

        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<SplitPart> parts = SplitParts(loaded.Document, request);
        string root = request.Output.Path;
        using OutputSet<Document> writer = session.Outputs.BeginSet([root], "pdf-split");
        string stem = SafeName(Path.GetFileNameWithoutExtension(filePath));
        var targets = new List<(SplitPart Part, string Path)>();
        int bookmarks = 0;
        int links = 0;
        int namedDestinations = 0;
        foreach (SplitPart part in parts)
        {
            string target = Path.Combine(
                root,
                string.Create(CultureInfo.InvariantCulture, $"{stem}.{part.Index:000}.pdf"));
            Document source = loaded.Document;
            int[] pages = [.. part.Pages];
            // The part is the document the write pipeline inspects, so it lives until it is staged.
            using Document selected = Select(source, pages);
            writer.Stage(target, request.Output.Overwrite, selected, staged =>
            {
                CopyPageLabels(source, selected, pages);
                // A part keeps the bookmarks of its pages; what leads elsewhere is counted.
                bookmarks += CopyOutline(source, source.Outlines, selected.Outlines, selected,
                    page => Array.IndexOf(pages, page) + 1);
                links += Math.Max(0, PdfNavigationCensus.UnresolvedLinks(selected, Enumerable.Range(1, pages.Length))
                    - PdfNavigationCensus.UnresolvedLinks(source, pages));
                namedDestinations += PdfNavigationCensus.NamedDestinationNames(source).Count(name =>
                    source.NamedDestinations[name] is ExplicitDestination destination
                    && pages.Contains(destination.PageNumber));
                selected.Save(staged);
            });
            targets.Add((part, target));
        }

        IReadOnlyList<long> sizes = writer.Commit();
        List<Warning> warnings = [];
        if (new PdfNavigationCensus(bookmarks, links, namedDestinations).ToWarning(
                "lost their exact target in the parts: bookmarks open their page at Fit zoom, links to a page of another part lead nowhere, and named destinations are not carried into the parts",
                "Re-create location-sensitive bookmarks (add_bookmark) and links (add_link) on the parts that need them.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }
        return new PdfSplitResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Outputs = targets.Select((item, index) => new PdfSplitOutput
            {
                Index = item.Part.Index,
                Pages = Aspose.Cli.Sdk.Addressing.PageRange.Describe(item.Part.Pages) ?? string.Empty,
                Bookmark = item.Part.Bookmark,
                Output = BuildOutput(item.Path, "pdf", sizes[index]),
            }).ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
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

    /// <summary>
    /// Labels each page of a part as it was labelled in the source. A label range starts at the
    /// part's first page and wherever a page does not follow the previous one in the same source
    /// range; a source page before the first range is numbered as the reader shows it.
    /// </summary>
    private static void CopyPageLabels(Document source, Document part, IReadOnlyList<int> pages)
    {
        int[] starts = [.. source.PageLabels.GetPages().Order()];
        if (starts.Length == 0)
        {
            return;
        }

        int previous = -2;
        int previousStart = -1;
        for (int index = 0; index < pages.Count; index++)
        {
            int page = pages[index] - 1;
            int start = starts.LastOrDefault(candidate => candidate <= page, -1);
            if (start != previousStart || page != previous + 1)
            {
                PageLabel label = start < 0
                    ? new PageLabel { NumberingStyle = NumberingStyle.NumeralsArabic }
                    : source.PageLabels.GetLabel(start);
                part.PageLabels.UpdateLabel(index, new PageLabel
                {
                    Prefix = label.Prefix,
                    NumberingStyle = label.NumberingStyle,
                    StartingValue = label.StartingValue + page - Math.Max(start, 0),
                });
            }

            previous = page;
            previousStart = start;
        }
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

