using Aspose.Words;
using Aspose.Words.Fields;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// Which header or footer the pages of a section show. A section that defines no header or
/// footer of a type continues the one of the nearest earlier section that does; the pages show
/// the primary ones, the first-page ones only when the section shows a different first page, and
/// the even-page ones only when it shows different odd and even pages.
/// </summary>
internal static class WordsHeadersFooters
{
    /// <summary>The header or footer of a type that a section has or continues, or null when no section up to it has one.</summary>
    internal static HeaderFooter? Continued(Section section, HeaderFooterType type)
    {
        for (Section? current = section; current is not null; current = current.PreviousSibling as Section)
        {
            if (current.HeadersFooters[type] is { } story)
            {
                return story;
            }
        }

        return null;
    }

    /// <summary>Whether some page of a section shows its header or footer of a type.</summary>
    internal static bool Shown(Section section, HeaderFooterType type) => type switch
    {
        HeaderFooterType.HeaderFirst or HeaderFooterType.FooterFirst => section.PageSetup.DifferentFirstPageHeaderFooter,
        HeaderFooterType.HeaderEven or HeaderFooterType.FooterEven => section.PageSetup.OddAndEvenPagesHeaderFooter,
        _ => true,
    };

    /// <summary>The headers and footers some page of the document shows, each once.</summary>
    internal static IReadOnlySet<HeaderFooter> ShownStories(Document document) =>
        document.Sections.Cast<Section>()
            .SelectMany(static section => Enum.GetValues<HeaderFooterType>()
                .Where(type => Shown(section, type))
                .Select(type => Continued(section, type)))
            .OfType<HeaderFooter>()
            .ToHashSet();

    /// <summary>Whether a header or footer holds a page number or page count, such as a PAGE field.</summary>
    internal static bool HoldsPageNumbers(HeaderFooter? story) =>
        story is not null && story.Range.Fields.Cast<Field>().Any(WordsText.VariesByPage);
}
