using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>A document's pages by number, and the text each one shows.</summary>
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
    internal static string DocumentText(Document document, IReadOnlyList<int> pages) =>
        string.Join(
            "\f" + Environment.NewLine,
            pages.Select(pageNumber => ExtractText(document.Pages[pageNumber], PdfReadContents.Text)));
}
