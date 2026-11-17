using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Words;
using Aspose.Words.Fields;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The page numbers set_header and set_footer replace in the primary header or footer, which
/// numbers a section's pages. An operation records each section of the input that shows the
/// primary header or footer it replaced, its own or the one it continues, when that held a page
/// number or page count before the batch or the operation; once the batch has run, the sections
/// whose primary header and footer hold none any more are reported as <c>PAGE_NUMBERS_REMOVED</c>,
/// which leaves the output incomplete. A set_page_numbers later in the batch numbers the pages
/// again, so it reports nothing. A first-page or even-page header or footer is not checked: no
/// operation numbers those pages again, and a cover page without a number is often intended.
/// </summary>
internal sealed class WordsPageNumberLosses(IReadOnlyList<Section> input)
{
    private readonly List<Replacement> _replaced = [];
    private readonly List<(string Op, int Section, string Text)> _moved = [];
    private readonly List<(SetPageNumbersOp Operation, Section Section, HeaderFooterType Left)> _numbered = [];

    // Before the batch: whether each section's primary header and footer, its own or continued,
    // held page numbers, how they read, and the field beside the number that no text writes.
    private readonly Dictionary<Section, Before> _before = input.ToDictionary(static section => section, static section => new Before(
        Holds(section, HeaderFooterType.HeaderPrimary), Holds(section, HeaderFooterType.FooterPrimary), WordsReadBack.PageNumbers(section, out string? unwritten), unwritten));

    /// <summary>
    /// Records that a set_page_numbers numbered a section in one location, so that the other,
    /// <paramref name="left"/>, shows none after the batch; a later one for the same section decides instead.
    /// </summary>
    internal void Numbered(SetPageNumbersOp operation, Section section, HeaderFooterType left)
    {
        _numbered.RemoveAll(item => item.Section == section);
        _numbered.Add((operation, section, left));
    }

    /// <summary>An issue for each section whose output still shows a page number where the batch's last set_page_numbers for it left none.</summary>
    internal IEnumerable<VerificationIssue> Uncleared(Document reopened) => _numbered
        .Select(item => (item.Operation, item.Left, Number: item.Section.ParentNode is null ? 0 : ((Document)item.Section.Document).Sections.IndexOf(item.Section) + 1))
        .Where(item => item.Number > 0 && item.Number <= reopened.Sections.Count
            && WordsHeadersFooters.Continued(reopened.Sections[item.Number - 1], item.Left)?.Range.Fields.Cast<Field>().Any(static field => field.Type == FieldType.FieldPage) == true)
        .Select(static item => VerificationIssues.Of(WordsDiagnostics.PageNumbersMismatch, string.Create(CultureInfo.InvariantCulture,
            $"set_page_numbers numbered section {item.Number} in the {item.Operation.Location}, and the output's {(item.Left == HeaderFooterType.HeaderPrimary ? "header" : "footer")} there still shows a page number."),
            string.Create(CultureInfo.InvariantCulture, $"section/{item.Number}")));

    /// <summary>Records the words a set_page_numbers without text moved with the number of a section, by its index, read before it ran.</summary>
    internal void Moved(SetPageNumbersOp operation, int section, string text) => _moved.Add((operation.Id!, section, text));

    /// <summary>The operations other than <paramref name="lastOp"/> that moved words, which <see cref="Unmoved"/> does not read back.</summary>
    internal IReadOnlySet<string> Unread(string lastOp) => _moved.Select(static item => item.Op).Where(op => op != lastOp).ToHashSet(StringComparer.Ordinal);

    /// <summary>An issue for each section whose page numbers in <paramref name="reopened"/> do not read as the words <paramref name="lastOp"/>, the batch's last operation, moved; a later operation may change an earlier one's.</summary>
    internal IEnumerable<VerificationIssue> Unmoved(Document reopened, string lastOp) => _moved
        .Where(item => item.Op == lastOp && item.Section < reopened.Sections.Count && WordsReadBack.PageNumbers(reopened.Sections[item.Section]).Text != item.Text)
        .Select(static item => VerificationIssues.Of(WordsDiagnostics.PageNumbersMismatch, string.Create(CultureInfo.InvariantCulture,
            $"set_page_numbers moved the page number of section {item.Section + 1} with the words '{item.Text}', and the output's page numbers do not read so."),
            string.Create(CultureInfo.InvariantCulture, $"section/{item.Section + 1}")));

    /// <summary>
    /// Records that an operation replaced the primary header or footer a section of the input
    /// shows, which held page numbers when it did so (<paramref name="numbered"/>) or before the
    /// batch; an <paramref name="owner"/> is one the operation wrote the story of. A later
    /// replacement of the same takes over the record, as only page numbers given after it stay.
    /// </summary>
    internal void Replaced(HeaderFooterOp operation, Section section, bool isHeader, bool numbered, bool owner)
    {
        bool earlier = _replaced.RemoveAll(item => item.Section == section && item.IsHeader == isHeader) > 0;
        if (_before.TryGetValue(section, out Before? before) && (earlier || numbered || (isHeader ? before.Header : before.Footer)))
        {
            _replaced.Add(new Replacement(operation, section, isHeader, owner));
        }
    }

    /// <summary>One warning for each operation whose replaced header or footer left pages without a number, in batch order.</summary>
    internal IReadOnlyList<Warning> Warnings(Document document) =>
        [.. _replaced
            .Where(item => ReferenceEquals(item.Section.Document, document) && item.Section.ParentNode is not null
                && !Holds(item.Section, HeaderFooterType.HeaderPrimary) && !Holds(item.Section, HeaderFooterType.FooterPrimary))
            .GroupBy(static item => (item.Operation, item.IsHeader))
            .Select(group => Warning(document, group.Key.Operation, group.Key.IsHeader, [.. group]))];

    /// <summary>Whether a section's primary header or footer of a type, its own or the one it continues, holds a page number.</summary>
    private static bool Holds(Section section, HeaderFooterType type) =>
        WordsHeadersFooters.HoldsPageNumbers(WordsHeadersFooters.Continued(section, type));

    /// <summary>
    /// The warning for the sections an operation left without page numbers.
    /// </summary>
    private static Warning Warning(Document document, HeaderFooterOp operation, bool isHeader, IReadOnlyList<Replacement> items)
    {
        string name = WordsOp.Catalog.NameOf(operation);
        int[] numbers = [.. items.Select(item => document.Sections.IndexOf(item.Section) + 1).Order()];
        string list = string.Join(", ", numbers.Select(static number => number.ToString(CultureInfo.InvariantCulture)));
        string location = isHeader ? WordsHeaderFooterLocations.Header : WordsHeaderFooterLocations.Footer;
        return new Warning(WordsDiagnostics.PageNumbersRemoved,
            $"{name} replaced the {location} that section(s) {list} show, which held the page numbers, and the new {location} has none, so those pages are no longer numbered.")
        {
            Location = numbers.Length == 1 ? string.Create(CultureInfo.InvariantCulture, $"section/{numbers[0]}") : null,
            Details = new JsonObject { ["id"] = operation.Id, ["op"] = name },
            AffectsCompleteness = true,
        };
    }

    private sealed record Replacement(HeaderFooterOp Operation, Section Section, bool IsHeader, bool Owner);

    private sealed record Before(bool Header, bool Footer, WordsPageNumbersData Read, string? Unwritten);
}
