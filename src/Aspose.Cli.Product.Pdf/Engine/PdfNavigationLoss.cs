using System.Globalization;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// The in-document navigation an output lost, by why, and the one place that decides whether a
/// loss leaves the output incomplete. An entry that led to a page the caller left out of the
/// output, which an edit deleted, <c>--pages</c> did not select or a split put in another part,
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
    // The most bookmark addresses a hint names.
    private const int NamedBookmarks = 20;

    /// <summary>The addresses in the output's outline of the bookmarks <see cref="Stranded"/> counts, when an edit found them.</summary>
    internal IReadOnlyList<string> StrandedBookmarks { get; init; } = [];

    /// <summary>The addresses in the output's outline of the bookmarks <see cref="Removed"/> counts, when an edit found them.</summary>
    internal IReadOnlyList<string> RemovedBookmarks { get; init; } = [];

    /// <summary>The addresses in the output's outline of every other bookmark, which a hint's deletion must not reach.</summary>
    internal IReadOnlyList<string> KeptBookmarks { get; init; } = [];

    /// <summary>What a navigation entry is.</summary>
    internal enum Kind
    {
        Bookmark,
        Link,
        NamedDestination,
    }

    /// <summary>
    /// The bookmarks of <paramref name="document"/>, and its links and named destinations that
    /// lead within it, each with the page it leads to, or none when it leads to no page.
    /// </summary>
    internal static IReadOnlyList<Entry> Capture(Document document)
    {
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
                        $"{area.LLX} {area.LLY} {area.URX} {area.URY} {Describe(target)}"), PageOf(document, target)));
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
            KeptBookmarks = [.. after.Select(static entry => entry.Address).OfType<string>().Except(strandedBookmarks).Except(removedBookmarks)],
        };
    }

    /// <summary>
    /// The warnings of an edit: one, which leaves the output incomplete, for the entries whose
    /// page is still in the output, and one for those whose page the edit deleted.
    /// </summary>
    /// <param name="outlineInspection">The quoted command line that reads the bookmarks of the input as they were, or null when none can.</param>
    internal IEnumerable<Warning> Warnings(string? outlineInspection) => new[]
    {
        StrandedWarning(
            "no longer lead to a page, although the page they led to is still in the output: the edit could not point them at it, "
                + "as with a destination whose top reads 0 on a moved page, which the SDK cannot tell apart from an omitted top",
            StrandedHint(outlineInspection)),
        RemovedWarning("lead to pages the edit deleted", DeletedHint()),
    }.OfType<Warning>();

    /// <summary>The warning for <see cref="Stranded"/>, which leaves the output incomplete, or null when it counts nothing.</summary>
    internal Warning? StrandedWarning(string cause, string hint) => ToWarning(Stranded, cause, hint, affectsCompleteness: true);

    /// <summary>The warning for <see cref="Removed"/>, which leaves the output as the caller asked, or null when it counts nothing.</summary>
    internal Warning? RemovedWarning(string cause, string hint) => ToWarning(Removed, cause, hint, affectsCompleteness: false);

    /// <summary>
    /// The warning for bookmarks a copy opens at their whole page rather than at the place in it
    /// they opened at, or null when there are none: they still lead to their page.
    /// </summary>
    /// <param name="bookmarks">How many bookmarks lost their place.</param>
    /// <param name="output">What holds them, such as "parts".</param>
    internal static Warning? Repositioned(int bookmarks, string output) => bookmarks == 0 ? null : new Warning(PdfDiagnostics.NavigationDegraded,
        $"{bookmarks} bookmark(s) of the {output} open their page as a whole, not at the place in it they opened in the input.")
    {
        Hint = "They still open their page. No operation sets the place in a page a bookmark opens at: tell the user if that place matters.",
    };

    private static Warning? ToWarning(PdfNavigationCensus lost, string cause, string hint, bool affectsCompleteness) => lost.Total == 0 ? null
        : new Warning(PdfDiagnostics.NavigationDegraded,
            $"{lost.Bookmarks} bookmark(s), {lost.Links} link(s) and {lost.NamedDestinations} named destination(s) {cause}.")
        {
            AffectsCompleteness = affectsCompleteness,
            Hint = hint,
        };

    private string StrandedHint(string? outlineInspection) => Sentences(
        Stranded.Bookmarks == 0
            ? null
            : $"Read the page each bookmark of the input opened with {outlineInspection ?? "'pdf inspect --detail outline' on a copy of the original"}.",
        Deletion(StrandedBookmarks, "the bookmarks the outline of the output shows without a page with delete_bookmarks") is { } deletion
            ? $"In the same batch, after the page operations, delete {deletion} and add them again with add_bookmark at the numbers their pages have in the output."
            : null,
        Held(StrandedBookmarks),
        Stranded.Links + Stranded.NamedDestinations == 0
            ? null
            : "No operation re-creates links or named destinations: tell the user which no longer lead to their page.");

    private string DeletedHint() => Sentences(
        "The pages they led to were deleted as the batch asked.",
        Deletion(RemovedBookmarks, "delete_bookmarks") is { } deletion ? $"Bookmarks you no longer need can be removed with {deletion}." : null,
        Held(RemovedBookmarks),
        Removed.Links + Removed.NamedDestinations == 0 ? null : "No operation deletes links or named destinations: tell the user they lead nowhere.");

    // The delete_bookmarks operation of the bookmarks at the addresses that hold no kept bookmark, without those another of
    // them holds, which it deletes with it; tooMany when there are more than a hint names, null when there are none.
    private string? Deletion(IReadOnlyList<string> bookmarks, string tooMany)
    {
        string[] free = [.. bookmarks.Where(address => !KeptBookmarks.Any(kept => Under(kept, address)))];
        string[] deleted = [.. free.Where(address => !free.Any(parent => Under(address, parent)))];
        return deleted.Length is 0 ? null : deleted.Length > NamedBookmarks ? tooMany
            : $"{{\"op\":\"delete_bookmarks\",\"bookmarks\":[{string.Join(",", deleted.Select(static address => $"\"{address}\""))}]}}";
    }

    // The bookmarks at the addresses that hold a kept bookmark, which deleting them would delete too.
    private string? Held(IReadOnlyList<string> bookmarks) =>
        bookmarks.Where(address => KeptBookmarks.Any(kept => Under(kept, address))).ToArray() is { Length: > 0 } held
            ? $"Bookmark(s) {string.Join(", ", held)} of the output lost their page but hold bookmarks that still lead to theirs: deleting one deletes those too, so keep it or tell the user."
            : null;

    private static bool Under(string address, string parent) => address.StartsWith(parent + "/", StringComparison.Ordinal);

    private static string Sentences(params string?[] sentences) => string.Join(' ', sentences.OfType<string>());

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
