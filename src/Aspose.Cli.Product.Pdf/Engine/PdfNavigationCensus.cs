using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Counts a document's in-document navigation — bookmarks, link annotations and named
/// destinations — whose target does not resolve to one of its pages. Deleting a page leaves
/// the navigation to it without a target, and a page move retargets only the destinations it
/// can rebuild exactly (<see cref="PdfNavigationRetarget"/>), so the difference between two
/// censuses is what an operation broke; it is disclosed rather than repaired with an invented
/// destination.
/// </summary>
internal readonly record struct PdfNavigationCensus(int Bookmarks, int Links, int NamedDestinations)
{
    internal int Total => Bookmarks + Links + NamedDestinations;

    /// <summary>Navigation entries whose destination no longer leads to a page.</summary>
    internal static PdfNavigationCensus Unresolved(Document document)
    {
        int bookmarks = CountUnresolved(document, document.Outlines);
        int links = UnresolvedLinks(document, Enumerable.Range(1, document.Pages.Count));
        int named = NamedDestinationNames(document)
            .Count(name => !Resolves(document, document.NamedDestinations[name]));
        return new PdfNavigationCensus(bookmarks, links, named);
    }

    /// <summary>The link annotations on the pages whose destination does not lead to a page.</summary>
    internal static int UnresolvedLinks(Document document, IEnumerable<int> pages) =>
        LinkTargets(document, pages).Count(static page => page == 0);

    /// <summary>
    /// The page each link annotation on the pages that leads within the document leads to
    /// (<see cref="PageNumber"/>), or 0 when it leads to none.
    /// </summary>
    internal static IEnumerable<int> LinkTargets(Document document, IEnumerable<int> pages) =>
        pages.SelectMany(page => document.Pages[page].Annotations.OfType<LinkAnnotation>())
            .Select(static link => Target(link.Destination, link.Action)).OfType<IAppointment>()
            .Select(target => PageNumber(document, target));

    /// <summary>
    /// The names of the document's named destinations. The engine throws
    /// <see cref="NullReferenceException"/> for a name tree without a Dests entry, such as one
    /// that holds only attachments (PDF-NAMES-WITHOUT-DESTS); that document has none. The engine
    /// call is the only code in the try, so no filter is needed, and none could tell its frames
    /// apart: optimized code inlines its getters into this method.
    /// </summary>
    internal static string[] NamedDestinationNames(Document document)
    {
        try
        {
            return document.NamedDestinations.Names;
        }
        catch (NullReferenceException)
        {
            return [];
        }
    }

    /// <summary>
    /// The destination a name leads to, or null when the document has none of that name. The
    /// engine's lookup throws for a name tree without a Dests entry (PDF-NAMES-WITHOUT-DESTS),
    /// such as the one a document assembled from copied pages gets with its attachments.
    /// </summary>
    internal static IAppointment? NamedTarget(Document document, string name) =>
        NamedDestinationNames(document).Contains(name, StringComparer.Ordinal)
            ? document.NamedDestinations[name]
            : null;

    /// <summary>What <paramref name="after"/> has broken that <paramref name="before"/> had not.</summary>
    internal static PdfNavigationCensus Degraded(PdfNavigationCensus before, PdfNavigationCensus after) => new(
        Math.Max(0, after.Bookmarks - before.Bookmarks),
        Math.Max(0, after.Links - before.Links),
        Math.Max(0, after.NamedDestinations - before.NamedDestinations));

    /// <summary>
    /// The destination an outline item or link points at, or null when it points outside the
    /// document: a web address, another file (the SDK's remote GoTo is a GoTo), any other action.
    /// </summary>
    internal static IAppointment? Target(IAppointment? destination, PdfAction? action) =>
        action is null or GoToAction and not GoToRemoteAction ? destination ?? (action as GoToAction)?.Destination : null;

    /// <summary>True when the destination, followed through its name, lands on an existing page.</summary>
    internal static bool Resolves(Document document, IAppointment? destination) => PageNumber(document, destination) > 0;

    /// <summary>The existing page the destination, followed through its name, lands on, or 0 when it lands on none.</summary>
    internal static int PageNumber(Document document, IAppointment? destination) => destination switch
    {
        ExplicitDestination target when target.PageNumber >= 1 && target.PageNumber <= document.Pages.Count => target.PageNumber,
        NamedDestination named => NamedTarget(document, named.Name) is ExplicitDestination resolved ? PageNumber(document, resolved) : 0,
        _ => 0,
    };

    private static int CountUnresolved(Document document, IEnumerable<OutlineItemCollection> items)
    {
        int count = 0;
        foreach (OutlineItemCollection item in items)
        {
            if (Target(item.Destination, item.Action) is { } target && !Resolves(document, target))
            {
                count++;
            }

            count += CountUnresolved(document, item);
        }

        return count;
    }

    /// <summary>The page an outline item leads to, followed through its name (<see cref="PageNumber"/>), or 0 when it leads to none.</summary>
    internal static int DestinationPage(Document document, OutlineItemCollection item) =>
        PageNumber(document, Target(item.Destination, item.Action));
}
