using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// A document's pages by number, and the text each one shows. A command that returns or searches
/// page text reads its pages through a <see cref="TextReading"/>, which says what that text leaves out.
/// </summary>
internal static class PdfPages
{
    internal static Page PageAt(Document document, int page) =>
        page > 0 && page <= document.Pages.Count
            ? document.Pages[page]
            : throw CliErrors.NotFoundAt(
                ErrorCodes.PageNotFound,
                "page",
                page.ToString(CultureInfo.InvariantCulture),
                document.Pages.Count);

    internal static string ExtractText(Page page, string content)
    {
        TextExtractionOptions.TextFormattingMode formatting = content == PdfReadContents.Layout
            ? TextExtractionOptions.TextFormattingMode.Pure
            : TextExtractionOptions.TextFormattingMode.Flatten;
        var absorber = new TextAbsorber
        {
            ExtractionOptions = new TextExtractionOptions(formatting),
        };
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }

    /// <summary>
    /// The plain text of the pages, each page separated from the next by a form feed and a
    /// line break, so page boundaries survive even when a page has no text.
    /// </summary>
    internal static string DocumentText(IEnumerable<Page> pages) =>
        string.Join("\f" + Environment.NewLine, pages.Select(static page => ExtractText(page, PdfReadContents.Text)));

    /// <summary>
    /// The text a command returns or searches, page by page. A field's value is not page text, so
    /// what the command read leaves out the values of the filled fields on the pages it read, which
    /// <see cref="FormValuesNotInText"/> names.
    /// </summary>
    /// <param name="document">The document read.</param>
    /// <param name="inspection">The command line that reads the input's field values, or null for a command made without one.</param>
    internal sealed class TextReading(Document document, string? inspection)
    {
        private readonly List<int> _read = [];

        /// <summary>The text of a page, as <see cref="ExtractText"/> reads it.</summary>
        internal string Text(int number, string content) => ExtractText(Page(number), content);

        /// <summary>The plain text of the pages, as <see cref="DocumentText"/> joins it.</summary>
        internal string PlainText(IEnumerable<int> numbers) => DocumentText(numbers.Select(Page));

        /// <summary>The matches of a pattern on a page, as <see cref="PdfTextMatch.MatchText"/> finds them.</summary>
        internal TextFragmentCollection Match(int number, string pattern, bool regex, bool caseSensitive, Func<string, Exception> invalidPattern) =>
            PdfTextMatch.MatchText(Page(number), pattern, regex, caseSensitive, invalidPattern);

        private Page Page(int number)
        {
            _read.Add(number);
            return document.Pages[number];
        }

        /// <summary>
        /// The warning that the text read leaves out the values of the filled fields on the pages
        /// read, with the command that reads them, or null when none of them holds one.
        /// </summary>
        internal Warning? FormValuesNotInText()
        {
            IReadOnlyList<string> filled = PdfForms.Filled(document, _read);
            return filled.Count == 0
                ? null
                : new Warning(PdfDiagnostics.FormValuesNotInText,
                    $"The page text leaves out the values of {filled.Count} filled form field{(filled.Count == 1 ? "" : "s")} on the pages read: {PdfForms.Listed(filled)}.")
                {
                    Hint = $"A field's value is not page text: read the values with {PdfForms.Inspection(inspection)}, which gives each field's name, value, page and rect.",
                    Location = filled.Count == 1 ? PdfInfoProjection.Address("field", filled[0]) : null,
                };
        }
    }
}
