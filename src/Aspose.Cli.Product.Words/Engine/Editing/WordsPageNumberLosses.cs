using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Words;

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

    // Before the batch: whether each section's primary header and footer, its own or continued,
    // held page numbers, how they read, and the field beside the number that no text writes.
    private readonly Dictionary<Section, Before> _before = input.ToDictionary(static section => section, static section => new Before(
        Holds(section, HeaderFooterType.HeaderPrimary), Holds(section, HeaderFooterType.FooterPrimary), WordsReadBack.PageNumbers(section, out string? unwritten), unwritten));

    /// <summary>Records the words a set_page_numbers without text moved with the number of a section, by its index, read before it ran.</summary>
    internal void Moved(SetPageNumbersOp operation, int section, string text) => _moved.Add((operation.Id!, section, text));

    /// <summary>The operations other than <paramref name="lastOp"/> that moved words, which <see cref="Unmoved"/> does not read back.</summary>
    internal IReadOnlySet<string> Unread(string lastOp) => _moved.Select(static item => item.Op).Where(op => op != lastOp).ToHashSet(StringComparer.Ordinal);

    /// <summary>An issue for each section whose page numbers in <paramref name="reopened"/> do not read as the words <paramref name="lastOp"/>, the batch's last operation, moved; a later operation may change an earlier one's.</summary>
    internal IEnumerable<VerificationIssue> Unmoved(Document reopened, string lastOp) => _moved
        .Where(item => item.Op == lastOp && item.Section < reopened.Sections.Count && WordsReadBack.PageNumbers(reopened.Sections[item.Section]).Text != item.Text)
        .Select(static item => VerificationIssues.Of(WordsDiagnostics.PageNumbersMismatch, string.Create(CultureInfo.InvariantCulture,
            $"set_page_numbers moved the page number of section {item.Section + 1} with the words '{item.Text}', and the output's page numbers do not read so."),
            string.Create(CultureInfo.InvariantCulture, $"section/{item.Section + 1}"), "Read the headers and footers with 'aspose-cli words inspect <file> --detail sections'."));

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
    /// The warning for the sections an operation left without page numbers. Its hint gives the
    /// set_page_numbers to add after the operation for each section whose story it wrote,
    /// numbered as in the input, by which the operations of a batch address sections: a section
    /// that continues that story numbers its pages with it and keeps what it shows, and no other
    /// section is numbered. It is one without a section when every section of the input had its
    /// story written and they read alike. Each writes the number as it read there before the batch.
    /// </summary>
    private Warning Warning(Document document, HeaderFooterOp operation, bool isHeader, IReadOnlyList<Replacement> items)
    {
        string name = WordsOp.Catalog.NameOf(operation);
        int[] numbers = [.. items.Select(item => document.Sections.IndexOf(item.Section) + 1).Order()];
        string list = string.Join(", ", numbers.Select(static number => number.ToString(CultureInfo.InvariantCulture)));
        string location = isHeader ? WordsHeaderFooterLocations.Header : WordsHeaderFooterLocations.Footer;
        (int Number, Before Before)[] owners = [.. items.Where(static item => item.Owner)
            .Select(item => (IndexOf(input, item.Section) + 1, _before[item.Section])).OrderBy(static owner => owner.Item1)];
        string[] renumbering = owners.Length == input.Count && owners.Select(owner => Numbering(location, null, owner.Before)).Distinct().Count() == 1
            ? [Numbering(location, null, owners[0].Before)]
            : [.. owners.Select(owner => Numbering(location, owner.Number, owner.Before))];
        string[] unwritten = [.. owners.Where(owner => owner.Before.Read.Location == location).Select(static owner => owner.Before.Unwritten).OfType<string>().Distinct()];
        return new Warning(WordsDiagnostics.PageNumbersRemoved,
            $"{name} replaced the {location} that section(s) {list} show, which held the page numbers, and the new {location} has none, so those pages are no longer numbered.")
        {
            Hint = $"To number the pages again, add after {name} in the batch: {string.Join(", ", renumbering)}." + (unwritten.Length == 0 ? string.Empty
                : $" The number's paragraph also held the {string.Join(" and ", unwritten)} field{(unwritten.Length == 1 ? string.Empty : "s")}, which no set_page_numbers text writes: tell the user, or write what it showed with {name}."),
            Location = numbers.Length == 1 ? string.Create(CultureInfo.InvariantCulture, $"section/{numbers[0]}") : null,
            Details = new JsonObject { ["id"] = operation.Id, ["op"] = name },
            AffectsCompleteness = true,
        };
    }

    // A set_page_numbers that writes the number as it read there before the batch: its alignment
    // where the default would change it, and its words when a text writes them all, none beside a
    // field no text writes, such as DATE, whose result they would fix.
    private static string Numbering(string location, int? section, Before before)
    {
        var operation = new JsonObject { ["op"] = "set_page_numbers", ["location"] = location };
        if (section is int number)
        {
            operation["section"] = number;
        }

        bool there = before.Read.Location == location;
        if (there && before.Read.Alignment is { } alignment && alignment != new SetPageNumbersOp().Alignment)
        {
            operation["alignment"] = alignment;
        }

        if (there && before.Unwritten is null && before.Read.Text is { Length: > 0 } text)
        {
            operation["text"] = text;
        }

        return operation.ToJsonString(Readable);
    }

    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The position of a section in a list, by identity; every section an operation replaced is one of the input.</summary>
    private static int IndexOf(IReadOnlyList<Section> sections, Section section)
    {
        for (int index = 0; index < sections.Count; index++)
        {
            if (ReferenceEquals(sections[index], section))
            {
                return index;
            }
        }

        throw new InvalidOperationException("A section whose header or footer an operation replaced is not one of the input's.");
    }

    private sealed record Replacement(HeaderFooterOp Operation, Section Section, bool IsHeader, bool Owner);

    private sealed record Before(bool Header, bool Footer, WordsPageNumbersData Read, string? Unwritten);
}
