using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// The in-document navigation an output lost, by why, and the one place that decides whether a
/// loss leaves the output incomplete. An entry that led to a page the caller left out of the
/// output, which an edit deleted, <c>--pages</c> did not select or a split left out of a part,
/// is the result the caller asked for. An entry whose page is still in the output but which no
/// longer leads to it, such as a destination whose top reads 0 on a moved page
/// (PDF-MOVE-BOOKMARK) or one through a named destination the output does not carry, is damage
/// beside what the caller asked for and leaves the output incomplete. A bookmark that still
/// opens its page, only not at the place in it, is neither (<see cref="Repositioned"/>). An edit
/// matches each entry before and after it by what it keeps of it: a bookmark by its title and
/// destination, a link by its area and destination, a named destination by its name; the page
/// it led to before the edit says which loss it is.
/// </summary>
/// <param name="Stranded">The entries whose page is still in the output.</param>
/// <param name="Removed">The entries whose page the caller left out of the output.</param>
internal sealed record PdfNavigationLoss(PdfNavigationCensus Stranded, PdfNavigationCensus Removed)
{
    /// <summary>The addresses in the output's outline of the bookmarks <see cref="Stranded"/> counts, when an edit found them.</summary>
    internal IReadOnlyList<string> StrandedBookmarks { get; init; } = [];

    /// <summary>The addresses in the output's outline of the bookmarks <see cref="Removed"/> counts, when an edit found them.</summary>
    internal IReadOnlyList<string> RemovedBookmarks { get; init; } = [];

    /// <summary>What a navigation entry is.</summary>
    internal enum Kind
    {
        Bookmark,
        Link,
        NamedDestination,
    }

    /// <summary>
    /// The bookmarks of <paramref name="document"/>, and its links and named destinations that
    /// lead within it, each with the page it leads to, or none when it leads to no page. A link's
    /// page is read through <paramref name="links"/>: the capture of a document before and after it
    /// changed in memory reads both with the same.
    /// </summary>
    internal static IReadOnlyList<Entry> Capture(Document document, PdfLinkPages? links = null)
    {
        links ??= new PdfLinkPages();
        var entries = new List<Entry>();
        CaptureOutline(document, document.Outlines, null, entries);
        for (int number = 1; number <= document.Pages.Count; number++)
        {
            foreach (Annotation annotation in document.Pages[number].Annotations)
            {
                if (annotation is LinkAnnotation link && PdfNavigationCensus.Target(link.Destination, link.Action) is { } target)
                {
                    Rectangle area = link.Rect;
                    entries.Add(new Entry(Kind.Link, string.Create(CultureInfo.InvariantCulture,
                        $"{area.LLX} {area.LLY} {area.URX} {area.URY} {Describe(target)}"), links.Number(document, target) is > 0 and int page ? document.Pages[page] : null));
                }
            }
        }

        foreach (string name in PdfNavigationCensus.NamedDestinationNames(document))
        {
            entries.Add(new Entry(Kind.NamedDestination, name, PageOf(document, document.NamedDestinations[name])));
        }

        return entries;
    }

    /// <summary>
    /// What the edit broke: each entry of <paramref name="after"/> that leads to no page, and did
    /// before, by whether the page it led to is still in the output.
    /// </summary>
    /// <param name="before">The entries before the edit (<see cref="Capture"/>).</param>
    /// <param name="after">The entries after it.</param>
    /// <param name="kept">Whether a page of the input is still in the output.</param>
    internal static PdfNavigationLoss Between(IReadOnlyList<Entry> before, IReadOnlyList<Entry> after, Func<Page, bool> kept)
    {
        Dictionary<(Kind, string), int> broken = [];
        Dictionary<(Kind, string), List<Page>> led = [];
        foreach (Entry entry in before)
        {
            (Kind, string) key = (entry.Kind, entry.Key);
            if (entry.Target is { } page)
            {
                if (!led.TryGetValue(key, out List<Page>? pages))
                {
                    led[key] = pages = [];
                }

                pages.Add(page);
            }
            else
            {
                broken[key] = broken.GetValueOrDefault(key) + 1;
            }
        }

        int[] stranded = new int[3], removed = new int[3];
        List<string> strandedBookmarks = [], removedBookmarks = [];
        foreach (Entry entry in after.Where(static entry => entry.Target is null))
        {
            (Kind, string) key = (entry.Kind, entry.Key);
            if (broken.GetValueOrDefault(key) > 0)
            {
                broken[key]--;
                continue;
            }

            // Of the pages entries like it led to, one the edit deleted explains the loss first;
            // an entry the edit made and then lost led to no page of the input.
            List<Page> pages = led.GetValueOrDefault(key) ?? [];
            int gone = pages.FindIndex(page => !kept(page));
            bool left = gone >= 0 || pages.Count == 0;
            (left ? removed : stranded)[(int)entry.Kind]++;
            if (entry.Address is { } address)
            {
                (left ? removedBookmarks : strandedBookmarks).Add(address);
            }

            if (pages.Count > 0)
            {
                pages.RemoveAt(Math.Max(gone, 0));
            }
        }

        return new PdfNavigationLoss(Census(stranded), Census(removed))
        {
            StrandedBookmarks = strandedBookmarks,
            RemovedBookmarks = removedBookmarks,
        };
    }

    /// <summary>
    /// The warnings of an edit: one, which leaves the output incomplete, for the entries whose
    /// page is still in the output, and one for those whose page the edit deleted.
    /// </summary>
    internal IEnumerable<Warning> Warnings() => new[]
    {
        StrandedWarning(
            "no longer lead to a page, although the page they led to is still in the output: the edit could not point them at it, "
                + "as with a destination whose top reads 0 on a moved page, which the SDK cannot tell apart from an omitted top"),
        RemovedWarning("lead to pages the edit deleted"),
    }.OfType<Warning>();

    /// <summary>The warning for <see cref="Stranded"/>, which leaves the output incomplete, or null when it counts nothing.</summary>
    internal Warning? StrandedWarning(string cause) => ToWarning(Stranded, StrandedBookmarks, cause, affectsCompleteness: true);

    /// <summary>The warning for <see cref="Removed"/>, which leaves the output as the caller asked, or null when it counts nothing.</summary>
    internal Warning? RemovedWarning(string cause) => ToWarning(Removed, RemovedBookmarks, cause, affectsCompleteness: false);

    /// <summary>
    /// The warning for bookmarks a copy opens at their whole page rather than at the place in it
    /// they opened at, or null when there are none: they still lead to their page.
    /// </summary>
    /// <param name="bookmarks">How many bookmarks lost their place.</param>
    /// <param name="output">What holds them, such as "parts".</param>
    internal static Warning? Repositioned(int bookmarks, string output) => bookmarks == 0 ? null : new Warning(PdfDiagnostics.NavigationDegraded,
        $"{bookmarks} bookmark(s) of the {output} open their page as a whole, not at the place in it they opened in the input.");

    private static Warning? ToWarning(PdfNavigationCensus lost, IReadOnlyList<string> bookmarks, string cause, bool affectsCompleteness) => lost.Total == 0 ? null
        : new Warning(PdfDiagnostics.NavigationDegraded,
            $"{lost.Bookmarks} bookmark(s), {lost.Links} link(s) and {lost.NamedDestinations} named destination(s) {cause}.")
        {
            AffectsCompleteness = affectsCompleteness,
            Details = bookmarks.Count == 0 ? null
                : new JsonObject { ["bookmarks"] = new JsonArray([.. bookmarks.Take(CliErrors.MaximumAvailableNames).Select(static address => (JsonNode)address)]) },
        };

    private static PdfNavigationCensus Census(int[] counts) =>
        new(counts[(int)Kind.Bookmark], counts[(int)Kind.Link], counts[(int)Kind.NamedDestination]);

    private static void CaptureOutline(Document document, IEnumerable<OutlineItemCollection> items, string? parent, List<Entry> entries)
    {
        int position = 0;
        foreach (OutlineItemCollection item in items)
        {
            string address = PdfOutlineAddresses.OutlineIndex(parent, ++position);
            IAppointment? target = PdfNavigationCensus.Target(item.Destination, item.Action);
            entries.Add(new Entry(Kind.Bookmark, $"{item.Title}\n{(target is null ? "none" : Describe(target))}", PageOf(document, target)) { Address = address });

            CaptureOutline(document, item, address, entries);
        }
    }

    // The page a destination leads to, followed through its name, as PdfNavigationCensus.Resolves reads it.
    private static Page? PageOf(Document document, IAppointment? destination) => destination switch
    {
        ExplicitDestination target when target.PageNumber >= 1 && target.PageNumber <= document.Pages.Count => document.Pages[target.PageNumber],
        NamedDestination named => PageOf(document, PdfNavigationCensus.NamedTarget(document, named.Name) as ExplicitDestination),
        _ => null,
    };

    // A destination without its page, which an edit keeps when the page goes.
    private static string Describe(IAppointment destination) => destination switch
    {
        XYZExplicitDestination target => string.Create(CultureInfo.InvariantCulture, $"XYZ {target.Left} {target.Top} {target.Zoom}"),
        FitHExplicitDestination target => string.Create(CultureInfo.InvariantCulture, $"FitH {target.Top}"),
        FitBHExplicitDestination target => string.Create(CultureInfo.InvariantCulture, $"FitBH {target.Top}"),
        FitVExplicitDestination target => string.Create(CultureInfo.InvariantCulture, $"FitV {target.Left}"),
        FitBVExplicitDestination target => string.Create(CultureInfo.InvariantCulture, $"FitBV {target.Left}"),
        FitRExplicitDestination target => string.Create(CultureInfo.InvariantCulture, $"FitR {target.Left} {target.Bottom} {target.Right} {target.Top}"),
        NamedDestination named => "named " + named.Name,
        _ => destination.GetType().Name,
    };

    /// <summary>One navigation entry: its kind, what tells it from the others across an edit, and the page it leads to, or null.</summary>
    internal sealed record Entry(Kind Kind, string Key, Page? Target)
    {
        /// <summary>A bookmark's address in the outline it was read from, as <c>pdf inspect</c> reports it.</summary>
        internal string? Address { get; init; }
    }
}
