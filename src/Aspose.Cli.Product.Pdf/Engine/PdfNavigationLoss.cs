using System.Globalization;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// The in-document navigation an edit left leading to no page, by why. An entry whose page the
/// edit deleted is the result the caller asked for. An entry whose page is still in the output
/// but which no longer leads to it, such as a destination with a coordinate of 0 on a moved page
/// (PDF-MOVE-BOOKMARK), is damage beside what the caller asked for and leaves the output
/// incomplete. Each entry is matched before and after the edit by what the edit keeps of it: a
/// bookmark by its title and destination, a link by its area and destination, a named
/// destination by its name; the page it led to before the edit says which loss it is.
/// </summary>
/// <param name="Stranded">The entries whose page is still in the output.</param>
/// <param name="Deleted">The entries whose page the edit deleted.</param>
internal sealed record PdfNavigationLoss(PdfNavigationCensus Stranded, PdfNavigationCensus Deleted)
{
    /// <summary>What a navigation entry is.</summary>
    internal enum Kind
    {
        Bookmark,
        Link,
        NamedDestination,
    }

    /// <summary>
    /// The bookmarks, links and named destinations of <paramref name="document"/> that lead
    /// within it, each with the page it leads to, or none when it no longer leads to a page.
    /// </summary>
    internal static IReadOnlyList<Entry> Capture(Document document)
    {
        var entries = new List<Entry>();
        CaptureOutline(document, document.Outlines, entries);
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

        int[] stranded = new int[3], deleted = new int[3];
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
            (gone >= 0 || pages.Count == 0 ? deleted : stranded)[(int)entry.Kind]++;
            if (pages.Count > 0)
            {
                pages.RemoveAt(Math.Max(gone, 0));
            }
        }

        return new PdfNavigationLoss(Census(stranded), Census(deleted));
    }

    /// <summary>
    /// The warnings: one, which leaves the output incomplete, for the entries whose page is still
    /// in the output, and one for those whose page the edit deleted.
    /// </summary>
    /// <param name="outlineInspection">The quoted command line that reads the bookmarks of the input as they were, or null when none can.</param>
    internal IEnumerable<Warning> Warnings(string? outlineInspection)
    {
        if (Stranded.ToWarning(
                "no longer lead to a page, although the page they led to is still in the output: the edit could not point them at it, "
                    + "as with a destination with a coordinate of 0 on a moved page, which the SDK cannot tell apart from an omitted one",
                StrandedHint(outlineInspection)) is { } stranded)
        {
            yield return stranded;
        }

        if (Deleted.ToWarning("lead to pages the edit deleted", DeletedHint(), affectsCompleteness: false) is { } deleted)
        {
            yield return deleted;
        }
    }

    private string StrandedHint(string? outlineInspection) => Sentences(
        Stranded.Bookmarks == 0
            ? null
            : $"Read the page each bookmark of the input opened with {outlineInspection ?? "'pdf inspect --detail outline' on a copy of the original"}; "
                + "in the outline of the output the ones that lost it have no page. "
                + "In the same batch, after the page operations, delete those bookmarks with delete_bookmarks "
                + "and add them again with add_bookmark at the numbers their pages have in the output.",
        Stranded.Links + Stranded.NamedDestinations == 0
            ? null
            : "No operation re-creates links or named destinations: tell the user which no longer lead to their page.");

    private string DeletedHint() => Sentences(
        "The pages they led to were deleted as the batch asked.",
        Deleted.Bookmarks == 0 ? null : "Bookmarks you no longer need can be removed with delete_bookmarks.",
        Deleted.Links + Deleted.NamedDestinations == 0 ? null : "No operation deletes links or named destinations: tell the user they lead nowhere.");

    private static string Sentences(params string?[] sentences) => string.Join(' ', sentences.OfType<string>());

    private static PdfNavigationCensus Census(int[] counts) =>
        new(counts[(int)Kind.Bookmark], counts[(int)Kind.Link], counts[(int)Kind.NamedDestination]);

    private static void CaptureOutline(Document document, IEnumerable<OutlineItemCollection> items, List<Entry> entries)
    {
        foreach (OutlineItemCollection item in items)
        {
            if (PdfNavigationCensus.Target(item.Destination, item.Action) is { } target)
            {
                entries.Add(new Entry(Kind.Bookmark, $"{item.Title}\n{Describe(target)}", PageOf(document, target)));
            }

            CaptureOutline(document, item, entries);
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
    internal sealed record Entry(Kind Kind, string Key, Page? Target);
}
