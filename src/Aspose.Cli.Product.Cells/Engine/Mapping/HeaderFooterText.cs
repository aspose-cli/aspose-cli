using System.Text.RegularExpressions;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The text the header and footer of a sheet print: the one place that reads it past its
/// formatting codes, so what a save says it leaves out (<see cref="UnwrittenContent"/>) and
/// what a replacement says it leaves in place (<c>TextReplaceOps</c>) are judged on the same
/// text.
/// </summary>
internal static partial class HeaderFooterText
{
    private static readonly string[] SectionNames = ["left", "center", "right"];

    /// <summary>
    /// The text a header or footer section prints of its own: a doubled ampersand prints one, and
    /// the page number and page count (the codes P and N, with an offset such as <c>&amp;P+1</c>)
    /// and the codes that only format what follows them (the font, the size, the color and the
    /// styles such as bold) print no text, whatever the case of their letter. A size code takes
    /// every digit after it, as the engine reads it, so <c>&amp;122024</c> prints nothing. The
    /// other codes, such as the date and the sheet name, stay as they are.
    /// </summary>
    internal static string Visible(string? section) =>
        string.IsNullOrEmpty(section) ? string.Empty : Codes().Replace(section, static code => code.Value == "&&" ? "&" : string.Empty);

    /// <summary>Whether a section prints text of its own (<see cref="Visible"/>), more than the page number and formatting.</summary>
    internal static bool PrintsText(string? section) => !string.IsNullOrWhiteSpace(Visible(section));

    /// <summary>
    /// The sections that print on some page, each with its raw text: the three sections of the
    /// header and of the footer, then those of the first page when the sheet gives it its own
    /// header and footer (<see cref="PageSetup.IsHFDiffFirst"/>), then those of the even pages when
    /// they differ from the odd ones (<see cref="PageSetup.IsHFDiffOddEven"/>). The first-page and
    /// even-page sections of a sheet that does not use them print nowhere and are left out.
    /// </summary>
    internal static IEnumerable<HeaderFooterSection> InEffect(PageSetup page)
    {
        IEnumerable<HeaderFooterSection> sections = Of(HeaderFooterPages.Default, page.GetHeader, page.GetFooter);
        if (page.IsHFDiffFirst)
        {
            sections = sections.Concat(Of(HeaderFooterPages.FirstPage, page.GetFirstPageHeader, page.GetFirstPageFooter));
        }

        if (page.IsHFDiffOddEven)
        {
            sections = sections.Concat(Of(HeaderFooterPages.EvenPages, page.GetEvenHeader, page.GetEvenFooter));
        }

        return sections;
    }

    /// <summary>The name of a section by its index, such as <c>center</c>.</summary>
    internal static string SectionName(int section) => SectionNames[section];

    private static IEnumerable<HeaderFooterSection> Of(HeaderFooterPages pages, Func<int, string?> header, Func<int, string?> footer) =>
        Enumerable.Range(0, SectionNames.Length).Select(section => new HeaderFooterSection(pages, Footer: false, section, header(section)))
            .Concat(Enumerable.Range(0, SectionNames.Length).Select(section => new HeaderFooterSection(pages, Footer: true, section, footer(section))));

    // A doubled ampersand, or a font, size, color, style, page number or page count code.
    [GeneratedRegex(@"&&|&(?:""[^""]*""|\d+|K(?:[0-9A-F]{6}|\d{2}[+-]\d{3})|[PN](?:[+-]\d+)?|[BIUESXYOH])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Codes();
}

/// <summary>The pages a header or footer prints on.</summary>
internal enum HeaderFooterPages
{
    /// <summary>Every page without one of its own: the header and footer a read reports and set_page_setup writes.</summary>
    Default,

    /// <summary>The first page, when the sheet gives it its own.</summary>
    FirstPage,

    /// <summary>The even pages, when they differ from the odd ones.</summary>
    EvenPages,
}

/// <summary>One section of a header or footer that prints on some page.</summary>
/// <param name="Pages">The pages it prints on.</param>
/// <param name="Footer">Whether it is a section of the footer rather than of the header.</param>
/// <param name="Section">Its index: 0 left, 1 center, 2 right.</param>
/// <param name="Text">Its raw text, formatting codes included, or null when it has none.</param>
internal readonly record struct HeaderFooterSection(HeaderFooterPages Pages, bool Footer, int Section, string? Text)
{
    /// <summary>The header or footer it belongs to, such as <c>header</c> or <c>first-page footer</c>.</summary>
    internal string Part => (Pages switch
    {
        HeaderFooterPages.FirstPage => "first-page ",
        HeaderFooterPages.EvenPages => "even-page ",
        _ => string.Empty,
    }) + (Footer ? "footer" : "header");
}
