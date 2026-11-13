using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// Plans a page move that keeps the form. The SDK has no page move, so a move copies pages and
/// deletes the originals, and a copied page's fields come back under a new top-level name beside
/// the originals, which keep their values without a widget (PDF-PAGE-COPY-FIELDS). Moving a run of
/// pages S to a position orders the pages as moving the pages T between them the other way does,
/// so the plan copies the pages that hold no widget, or else those whose every field it can name
/// back: one with no parent in its name, whose widgets all lie on the copied pages. After the copy
/// each such field's copy takes its name and the original is deleted. Any other move is refused
/// before it changes anything.
/// </summary>
internal sealed class PdfMovedFields(HashSet<string> names, List<(string Name, string[] Widgets)> copied)
{
    /// <summary>The pages to copy, the position to insert the copies at and their fields, for moving <paramref name="pages"/> to <paramref name="at"/>.</summary>
    internal static (int[] Pages, int At, PdfMovedFields Fields) Plan(Document document, int[] pages, int at)
    {
        List<(int[] Pages, int At)> plans = [(pages, at)];
        if (at != pages[0] && pages.Zip(pages.Skip(1)).All(static pair => pair.Second == pair.First + 1))
        {
            plans.Add(at < pages[0]
                ? ([.. Enumerable.Range(at, pages[0] - at)], at + pages.Length)
                : ([.. Enumerable.Range(pages[^1] + 1, at - pages[0])], pages[0]));
        }

        ILookup<int, string> widgets = Enumerable.Range(1, document.Pages.Count).SelectMany(number =>
                document.Pages[number].Annotations.OfType<WidgetAnnotation>().Select(widget => (Page: number, widget.FullName)))
            .ToLookup(static widget => widget.Page, static widget => widget.FullName);
        string? Unnamed(int[] plan) => plan.SelectMany(page => widgets[page]).FirstOrDefault(name =>
            name.Contains('.', StringComparison.Ordinal) || widgets.Any(page => !plan.Contains(page.Key) && page.Contains(name)));
        (int[] Pages, int At) chosen = plans.OrderBy(plan => plan.Pages.Any(page => widgets[page].Any())).FirstOrDefault(plan => Unnamed(plan.Pages) is null);
        if (chosen.Pages is null)
        {
            string field = Unnamed(pages)!;
            string holding = string.Join(", ", widgets.Where(page => page.Contains(field)).Select(static page => page.Key));
            throw new OperationInvalidException(field.Contains('.', StringComparison.Ordinal)
                    ? $"form field '{field}' on page {holding} has a parent in its name, and this move copies that page, which leaves a second copy of the field beside it"
                    : $"form field '{field}' has widgets on pages {holding}, and this move copies some of them but not all, which leaves a second copy of the field",
                (field.Contains('.', StringComparison.Ordinal) ? $"Move the pages without form fields around page {holding} instead" : $"Move pages {holding} together, or move the pages without form fields around them instead")
                    + ", which can give the same order without copying the field; if no such move does, tell the user.");
        }

        HashSet<string> moved = [.. chosen.Pages.SelectMany(page => widgets[page])];
        return (chosen.Pages, chosen.At, new PdfMovedFields(
            [.. document.Form.Fields.Select(static field => field.FullName)],
            Shapes(document, moved.Contains, page => Array.IndexOf(chosen.Pages, page))));
    }

    /// <summary>
    /// Gives each copy the name of the field it copies, whose widgets the move placed from page
    /// <paramref name="at"/> on, and deletes the original. A field without such a copy fails the move.
    /// </summary>
    internal void Restore(Document document, int at)
    {
        List<(string Name, string[] Widgets)> copies = Shapes(document, name => !names.Contains(name), page => page - at);
        foreach ((string name, string[] widgets) in copied)
        {
            int copy = copies.FindIndex(candidate => candidate.Widgets.SequenceEqual(widgets));
            if (copy < 0)
            {
                throw new EngineOpException($"the engine did not copy form field '{name}' as a page move expects, so the field would be left twice",
                    new InvalidOperationException(name));
            }

            document.Form.Delete(name);
            ((Field)document.Form[copies[copy].Name]).PartialName = name;
            copies.RemoveAt(copy);
        }
    }

    // Each field the predicate selects, with each of its widgets as its kind, its place among the copied pages and its area.
    private static List<(string Name, string[] Widgets)> Shapes(Document document, Func<string, bool> selected, Func<int, int> place) =>
        [.. document.Form.Fields.Where(field => selected(field.FullName)).GroupBy(static field => field.FullName, StringComparer.Ordinal)
            .Select(group => (group.Key, group.Select(field => $"{field.GetType().Name} {place(PdfForms.PageOf(field) ?? 0)} {field.Rect}").ToArray()))];
}
