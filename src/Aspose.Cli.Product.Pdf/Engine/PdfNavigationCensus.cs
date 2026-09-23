using Aspose.Cli.Sdk.Contracts;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Counts a document's in-document navigation — bookmarks, link annotations and named
/// destinations — whose target does not resolve to one of its pages. Page moves and merges
/// rebuild page objects, and the SDK exposes no public way to retarget an existing
/// destination losslessly, so the difference between two censuses is what an operation
/// broke; it is disclosed rather than repaired with an invented destination.
/// </summary>
internal readonly record struct PdfNavigationCensus(int Bookmarks, int Links, int NamedDestinations)
{
    internal int Total => Bookmarks + Links + NamedDestinations;

    /// <summary>Navigation entries whose destination no longer leads to a page.</summary>
    internal static PdfNavigationCensus Unresolved(Document document)
    {
        int bookmarks = CountUnresolved(document, document.Outlines);
        int links = 0;
        foreach (Page page in document.Pages)
        {
            foreach (Annotation annotation in page.Annotations)
            {
                if (annotation is LinkAnnotation link
                    && Target(link.Destination, link.Action) is { } target
                    && !Resolves(document, target))
                {
                    links++;
                }
            }
        }

        int named = document.NamedDestinations.Names
            .Count(name => !Resolves(document, document.NamedDestinations[name]));
        return new PdfNavigationCensus(bookmarks, links, named);
    }

    /// <summary>What <paramref name="after"/> has broken that <paramref name="before"/> had not.</summary>
    internal static PdfNavigationCensus Degraded(PdfNavigationCensus before, PdfNavigationCensus after) => new(
        Math.Max(0, after.Bookmarks - before.Bookmarks),
        Math.Max(0, after.Links - before.Links),
        Math.Max(0, after.NamedDestinations - before.NamedDestinations));

    /// <summary>The completeness warning for degraded navigation, or null when nothing degraded.</summary>
    internal Warning? ToWarning(string cause, string hint) => Total == 0 ? null : new Warning
    {
        Code = PdfDiagnostics.NavigationDegraded,
        AffectsCompleteness = true,
        Message = $"{Bookmarks} bookmark(s), {Links} link(s) and {NamedDestinations} named destination(s) {cause}.",
        Hint = hint,
    };

    /// <summary>The destination an outline item or link points at, or null when it points outside the document.</summary>
    internal static IAppointment? Target(IAppointment? destination, PdfAction? action) =>
        destination ?? (action as GoToAction)?.Destination;

    /// <summary>True when the destination, followed through its name, lands on an existing page.</summary>
    internal static bool Resolves(Document document, IAppointment? destination) => destination switch
    {
        ExplicitDestination explicitDestination =>
            explicitDestination.PageNumber >= 1 && explicitDestination.PageNumber <= document.Pages.Count,
        NamedDestination named => document.NamedDestinations[named.Name] is ExplicitDestination resolved
            && Resolves(document, resolved),
        _ => false,
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
}
