using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Keeps navigation to and from pages that an edit replaces with copies. The SDK has no page
/// move, so a move copies the pages and deletes the originals. Copying pages together keeps
/// the links between them, but every other destination that names an original is left
/// without a page: bookmarks, named destinations and links on other pages that target a
/// copied page, and links on a copied page that target a page that was not copied.
/// <see cref="Capture"/> records those whose destination can be rebuilt exactly, before the
/// deletion; <see cref="Retarget"/> points them at the pages' new numbers afterwards.
/// </summary>
/// <remarks>
/// The SDK reads an omitted (null) destination coordinate as 0 and cannot write one, so a
/// destination with a coordinate that reads 0 could only be rebuilt with an invented
/// position. It is left untouched and <see cref="PdfNavigationCensus"/> discloses it. A zoom
/// of 0 means the same as an omitted zoom, and FitR coordinates are never omitted, so neither
/// is ambiguous. Destinations are built with <see cref="ExplicitDestination.CreateDestination(Page, ExplicitDestinationType, double[])"/>:
/// one constructed from a page reports page number 0 until the document is saved, which the
/// census would count as lost.
/// </remarks>
internal sealed class PdfNavigationRetarget
{
    private readonly List<Entry> _entries = [];
    private readonly List<(int HostPage, int Annotation, Entry Entry)> _linksOnReplacedPages = [];

    private PdfNavigationRetarget()
    {
    }

    /// <summary>
    /// Records the navigation that <paramref name="replaced"/> (page numbers) would lose when
    /// they are copied together and the originals deleted.
    /// </summary>
    internal static PdfNavigationRetarget Capture(Document document, IReadOnlyCollection<int> replaced)
    {
        var retarget = new PdfNavigationRetarget();
        var pages = new HashSet<int>(replaced);
        retarget.CaptureOutlines(document.Outlines, pages);

        foreach (string name in document.NamedDestinations.Names)
        {
            if (Exact(document.NamedDestinations[name]) is { } named && pages.Contains(named.Page))
            {
                retarget._entries.Add(named with { Set = destination => document.NamedDestinations[name] = destination });
            }
        }

        for (int number = 1; number <= document.Pages.Count; number++)
        {
            bool hostReplaced = pages.Contains(number);
            int index = 0;
            foreach (Annotation annotation in document.Pages[number].Annotations)
            {
                index++;
                if (annotation is not LinkAnnotation link
                    || Exact(PdfNavigationCensus.Target(link.Destination, link.Action)) is not { } target
                    || target.Page < 1
                    || target.Page > document.Pages.Count
                    || pages.Contains(target.Page) == hostReplaced)
                {
                    continue;
                }

                // A replaced page's links are replaced with it, in the same order.
                if (hostReplaced)
                {
                    retarget._linksOnReplacedPages.Add((number, index, target));
                }
                else
                {
                    retarget._entries.Add(target with { Set = Setter(link) });
                }
            }
        }

        return retarget;
    }

    /// <summary>Points the captured navigation at the pages' new numbers.</summary>
    /// <param name="document">The edited document.</param>
    /// <param name="renumber">The new number of the page an original page number named.</param>
    internal void Retarget(Document document, Func<int, int> renumber)
    {
        foreach (Entry entry in _entries)
        {
            entry.Set(entry.Rebuild(document, renumber));
        }

        foreach ((int hostPage, int annotation, Entry entry) in _linksOnReplacedPages)
        {
            AnnotationCollection annotations = document.Pages[renumber(hostPage)].Annotations;
            if (annotation <= annotations.Count && annotations[annotation] is LinkAnnotation link)
            {
                Setter(link)(entry.Rebuild(document, renumber));
            }
        }
    }

    private void CaptureOutlines(IEnumerable<OutlineItemCollection> items, HashSet<int> pages)
    {
        foreach (OutlineItemCollection item in items)
        {
            if (Exact(PdfNavigationCensus.Target(item.Destination, item.Action)) is { } target && pages.Contains(target.Page))
            {
                Action<IAppointment> set = item.Destination is not null
                    ? destination => item.Destination = destination
                    : destination => ((GoToAction)item.Action).Destination = destination;
                _entries.Add(target with { Set = set });
            }

            CaptureOutlines(item, pages);
        }
    }

    private static Action<IAppointment> Setter(LinkAnnotation link) => link.Destination is not null
        ? destination => link.Destination = destination
        : destination => ((GoToAction)link.Action).Destination = destination;

    /// <summary>
    /// The page, type and coordinates of an explicit destination, or null when it is not
    /// explicit or a coordinate reads 0 and may stand for an omitted one.
    /// </summary>
    private static Entry? Exact(IAppointment? appointment) => appointment switch
    {
        FitExplicitDestination fit => new(fit.PageNumber, ExplicitDestinationType.Fit, []),
        FitBExplicitDestination fit => new(fit.PageNumber, ExplicitDestinationType.FitB, []),
        FitRExplicitDestination fit => new(fit.PageNumber, ExplicitDestinationType.FitR, [fit.Left, fit.Bottom, fit.Right, fit.Top]),
        XYZExplicitDestination { Left: not 0, Top: not 0 } xyz => new(xyz.PageNumber, ExplicitDestinationType.XYZ, [xyz.Left, xyz.Top, xyz.Zoom]),
        FitHExplicitDestination { Top: not 0 } fit => new(fit.PageNumber, ExplicitDestinationType.FitH, [fit.Top]),
        FitBHExplicitDestination { Top: not 0 } fit => new(fit.PageNumber, ExplicitDestinationType.FitBH, [fit.Top]),
        FitVExplicitDestination { Left: not 0 } fit => new(fit.PageNumber, ExplicitDestinationType.FitV, [fit.Left]),
        FitBVExplicitDestination { Left: not 0 } fit => new(fit.PageNumber, ExplicitDestinationType.FitBV, [fit.Left]),
        _ => null,
    };

    /// <summary>An exact destination and where it is written back.</summary>
    private sealed record Entry(int Page, ExplicitDestinationType Type, double[] Values)
    {
        public Action<IAppointment> Set { get; init; } = static _ => { };

        public ExplicitDestination Rebuild(Document document, Func<int, int> renumber) =>
            ExplicitDestination.CreateDestination(document.Pages[renumber(Page)], Type, Values);
    }
}
