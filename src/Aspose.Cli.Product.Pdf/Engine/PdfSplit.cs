using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
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
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<SplitPart> parts = SplitParts(loaded.Document, request);
        (PdfOutputPasswords? passwords, Warning? kept) =
            PdfOutputEncryption.Kept(request.EncryptPassword, request.Password, [PdfInputSecurity.Of(loaded)]);
        string root = request.Output.Path;
        using OutputSet<Document> writer = session.Outputs.BeginSet([root], "pdf-split");
        IReadOnlyList<int> numbers = request.Plan.By == SplitBy.Pages
            ? request.Plan.PartNumbers([.. parts.Select(static part => part.Pages)])
            : [.. parts.Select(static part => part.Index)];
        IReadOnlyList<string> paths = request.Output.Parts(Path.GetFileNameWithoutExtension(filePath), "pdf", numbers);
        var targets = new List<(SplitPart Part, string Path, bool Encrypted)>();
        int bookmarks = 0;
        int links = 0;
        int namedDestinations = 0;
        foreach ((SplitPart part, string target) in parts.Zip(paths))
        {
            Document source = loaded.Document;
            int[] pages = [.. part.Pages];
            // The part is the document the write pipeline inspects, so it lives until it is staged.
            using Document selected = Select(source, pages);
            bool encrypted = PdfOutputEncryption.Encrypted(selected, passwords);
            writer.Stage(target, request.Output.Overwrite, selected, staged =>
            {
                CopyPageLabels(source, selected, pages);
                // Each part carries every attachment of the source; a part starts with none, so none is left out.
                CopyAttachments(source, selected);
                // A part keeps the bookmarks of its pages; what leads elsewhere is counted.
                bookmarks += CopyOutline(source, source.Outlines, selected.Outlines, selected,
                    page => Array.IndexOf(pages, page) + 1);
                links += Math.Max(0, PdfNavigationCensus.UnresolvedLinks(selected, Enumerable.Range(1, pages.Length))
                    - PdfNavigationCensus.UnresolvedLinks(source, pages));
                namedDestinations += PdfNavigationCensus.NamedDestinationNames(source).Count(name =>
                    source.NamedDestinations[name] is ExplicitDestination destination
                    && pages.Contains(destination.PageNumber));
                PdfOutputEncryption.Save(selected, staged, passwords);
            }, encrypted: encrypted);
            targets.Add((part, target, encrypted));
        }

        IReadOnlyList<PublishedFile> files = writer.Commit();
        List<Warning> warnings = [];
        if (PortfolioNotKept(loaded.Document, input: null) is { } portfolio)
        {
            warnings.Add(portfolio);
        }

        if (kept is not null)
        {
            warnings.Add(kept);
        }

        if (new PdfNavigationCensus(bookmarks, links, namedDestinations).ToWarning(
                "lost their exact target in the parts: bookmarks open their page at Fit zoom, links to a page of another part lead nowhere, and named destinations are not carried into the parts",
                "Re-create location-sensitive bookmarks (add_bookmark) and links (add_hyperlink) on the parts that need them.")
            is { } degraded)
        {
            warnings.Add(degraded);
        }
        return new PdfSplitResult
        {
            Input = PdfInfoProjection.Source(filePath, loaded),
            Outputs = targets.Select((item, index) => new PdfPartOutput(FileObjects.Output(files[index], PdfFormats.Document[0], item.Encrypted))
            {
                Pages = Aspose.Cli.Sdk.Addressing.PageRange.Describe(item.Part.Pages) ?? string.Empty,
            }).ToArray(),
            License = EnvelopeParts.License(state),
            Warnings = warnings.Count == 0 ? null : warnings,
        };
    }


    private static IReadOnlyList<SplitPart> SplitParts(Document document, PdfSplitRequest request)
    {
        if (request.Plan.By == SplitBy.Pages)
        {
            return [.. request.Plan.PageGroups(document.Pages.Count).Select(static (pages, index) => new SplitPart(index + 1, pages))];
        }

        var bookmarks = document.Outlines
            .Select(static item => (Title: item.Title ?? string.Empty, Page: PdfNavigationCensus.DestinationPage(item)))
            .Where(static item => item.Page > 0)
            .OrderBy(static item => item.Page)
            .GroupBy(static item => item.Page)
            .Select(static group => group.First())
            .ToArray();
        if (bookmarks.Length == 0)
        {
            throw CliErrors.OptionInvalid(
                "--by",
                "the document has no top-level bookmarks with page destinations",
                "Use --by pages, or add bookmarks first.");
        }

        var parts = new List<SplitPart>();
        if (bookmarks[0].Page > 1)
        {
            parts.Add(new SplitPart(
                parts.Count + 1,
                Enumerable.Range(1, bookmarks[0].Page - 1).ToArray()));
        }

        int bookmarkIndexOffset = parts.Count;
        parts.AddRange(bookmarks.Select((bookmark, index) =>
        {
            int end = index + 1 < bookmarks.Length ? bookmarks[index + 1].Page - 1 : document.Pages.Count;
            int[] pages = Enumerable.Range(bookmark.Page, end - bookmark.Page + 1).ToArray();
            return new SplitPart(bookmarkIndexOffset + index + 1, pages);
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

    private sealed record SplitPart(int Index, IReadOnlyList<int> Pages);
}

