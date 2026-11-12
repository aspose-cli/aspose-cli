using System.Globalization;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>
/// The page numbers set_header and set_footer replace in the primary header or footer, which
/// numbers a section's pages. An operation records each section whose replaced primary header or
/// footer held a page number or page count; once the batch has run, the sections whose primary
/// header and footer hold none any more are reported as <c>PAGE_NUMBERS_REMOVED</c>, which leaves
/// the output incomplete. A set_page_numbers later in the batch numbers the pages again, so it
/// reports nothing. A first-page or even-page header or footer is not checked: no operation
/// numbers those pages again, and a cover page without a number is often intended.
/// </summary>
internal sealed class WordsPageNumberLosses
{
    private readonly List<Replacement> _replaced = [];
    private readonly List<(string Op, int Section, string Text)> _moved = [];

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

    /// <summary>Records that an operation replaced a section's primary header or footer, which held page numbers.</summary>
    internal void Replaced(HeaderFooterOp operation, Section section, bool isHeader) =>
        _replaced.Add(new Replacement(operation, section, isHeader));

    /// <summary>
    /// One warning for each operation whose replaced header or footer left pages without a number,
    /// in batch order. <paramref name="input"/> lists the sections before the batch, by whose
    /// numbers the operations of a batch address them.
    /// </summary>
    internal IReadOnlyList<Warning> Warnings(Document document, IReadOnlyList<Section> input) =>
        [.. _replaced
            .Where(item => ReferenceEquals(item.Section.Document, document) && item.Section.ParentNode is not null
                && !Numbered(item.Section))
            .GroupBy(static item => (item.Operation, item.IsHeader))
            .Select(group => Warning(document, input, group.Key.Operation, group.Key.IsHeader, [.. group.Select(static item => item.Section).Distinct()]))];

    /// <summary>Whether a section's primary header or footer, its own or the one it continues, holds a page number.</summary>
    private static bool Numbered(Section section) =>
        WordsHeadersFooters.HoldsPageNumbers(WordsHeadersFooters.Continued(section, HeaderFooterType.HeaderPrimary))
        || WordsHeadersFooters.HoldsPageNumbers(WordsHeadersFooters.Continued(section, HeaderFooterType.FooterPrimary));

    /// <summary>
    /// The warning for the sections an operation left without page numbers. Its hint gives the
    /// set_page_numbers to add after the operation: one without a section when every section of
    /// the input lost its numbers, otherwise one for each such section, numbered as in the input,
    /// so that it numbers no other section and moves no page number another section shows.
    /// </summary>
    private static Warning Warning(Document document, IReadOnlyList<Section> input, HeaderFooterOp operation, bool isHeader, IReadOnlyList<Section> sections)
    {
        string name = WordsOp.Catalog.NameOf(operation);
        int[] numbers = [.. sections.Select(section => document.Sections.IndexOf(section) + 1).Order()];
        string list = string.Join(", ", numbers.Select(static number => number.ToString(CultureInfo.InvariantCulture)));
        string location = isHeader ? WordsHeaderFooterLocations.Header : WordsHeaderFooterLocations.Footer;
        string numbering = "{\"op\":\"set_page_numbers\",\"location\":\"" + location + "\"";
        string[] renumbering = input.All(sections.Contains)
            ? [numbering + "}"]
            : [.. sections.Select(section => IndexOf(input, section) + 1).Order()
                .Select(number => numbering + ",\"section\":" + number.ToString(CultureInfo.InvariantCulture) + "}")];
        return new Warning(WordsDiagnostics.PageNumbersRemoved,
            $"{name} replaced the {location} of section(s) {list}, which held the page numbers, and the new {location} has none, so those pages are no longer numbered.")
        {
            Hint = $"To number the pages again, add after {name} in the batch: {string.Join(", ", renumbering)}.",
            Location = numbers.Length == 1 ? string.Create(CultureInfo.InvariantCulture, $"section/{numbers[0]}") : null,
            Details = new JsonObject { ["id"] = operation.Id, ["op"] = name },
            AffectsCompleteness = true,
        };
    }

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

    private sealed record Replacement(HeaderFooterOp Operation, Section Section, bool IsHeader);
}
