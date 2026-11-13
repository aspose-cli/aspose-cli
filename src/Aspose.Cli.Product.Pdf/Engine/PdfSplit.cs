using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using static Aspose.Cli.Product.Pdf.Engine.PdfAssembly;


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
        int repositioned = 0;
        (int Stranded, int Removed) links = default;
        int namedDestinations = 0;
        foreach ((SplitPart part, string target) in parts.Zip(paths))
        {
            Document source = loaded.Document;
            int[] pages = [.. part.Pages];
            // The part is the document the write pipeline inspects, so it lives until it is staged.
            using Document selected = Select(source, pages);
            bool encrypted = PdfOutputEncryption.Encrypted(selected, passwords);
            writer.Stage(target, PdfFormats.Document[0], request.Output.Overwrite, selected, staged =>
            {
                CopyInformation(source, selected);
                CopyPageLabels(source, selected, pages, offset: 0);
                // Each part carries every attachment of the source; a part starts with none, so none is left out.
                CopyAttachments(source, selected);
                // A part keeps the bookmarks of its pages; what leads elsewhere is counted.
                repositioned += CopyOutline(source, source.Outlines, selected.Outlines, selected,
                    page => Array.IndexOf(pages, page) + 1);
                // A link the part broke led to a page of another part, or else to one of its own.
                int broken = Math.Max(0, PdfNavigationCensus.UnresolvedLinks(selected, Enumerable.Range(1, pages.Length))
                    - PdfNavigationCensus.UnresolvedLinks(source, pages));
                int elsewhere = Math.Min(broken, PdfNavigationCensus.LinkTargets(source, pages).Count(page => page > 0 && !pages.Contains(page)));
                links = (links.Stranded + broken - elsewhere, links.Removed + elsewhere);
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

        var navigation = new PdfNavigationLoss(
            new PdfNavigationCensus(0, links.Stranded, namedDestinations), new PdfNavigationCensus(0, links.Removed, 0));
        warnings.AddRange(new[]
        {
            navigation.StrandedWarning(
                "no longer lead to their page, although their part holds it: the parts carry no named destinations, so what led there through one leads nowhere",
                "No operation re-creates links or named destinations: tell the user which no longer lead to their page."),
            navigation.RemovedWarning(
                "lead to a page of another part, so they lead nowhere in their own",
                "The pages they led to are in other parts, as the split asked; tell the user they lead nowhere in the parts."),
            PdfNavigationLoss.Repositioned(repositioned, "parts"),
        }.OfType<Warning>());
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
            .Select(item => (Title: item.Title ?? string.Empty, Page: PdfNavigationCensus.DestinationPage(document, item)))
            .Where(static item => item.Page > 0)
            .OrderBy(static item => item.Page)
            .GroupBy(static item => item.Page)
            .Select(static group => group.First())
            .ToArray();
        if (bookmarks.Length == 0)
        {
            throw CliErrors.OptionInvalid(
                SplitOptions.ByOption,
                "the document has no top-level bookmarks with page destinations",
                "Use --by pages, or first bookmark the first page of each part with a 'pdf edit' batch of add_bookmark operations, "
                + "such as {\"op\":\"add_bookmark\",\"title\":\"Part 1\",\"page\":1}; headings, such as those of Markdown, are not bookmarks.");
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

    private sealed record SplitPart(int Index, IReadOnlyList<int> Pages);
}

