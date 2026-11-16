using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// The page each link of a document in memory leads to, the one judgment of it while an edit
/// changes the document. A link keeps the destination it read first, page number included, so after
/// a page deletion it can name the number another page took (PDF-LINK-STALE-PAGE). A link's
/// explicit destination is therefore told by the page it led to when it was first read here, and
/// leads to no page once the document no longer holds that page; any other destination is read as
/// it is.
/// </summary>
internal sealed class PdfLinkPages
{
    private readonly Dictionary<IAppointment, Page?> _pages = new(ReferenceEqualityComparer.Instance);

    /// <summary>The number of the page of <paramref name="document"/> a link's destination leads to, followed through its name, or 0 when it leads to none.</summary>
    internal int Number(Document document, IAppointment destination)
    {
        if (destination is not ExplicitDestination)
        {
            return PdfNavigationCensus.PageNumber(document, destination);
        }

        if (!_pages.TryGetValue(destination, out Page? page))
        {
            _pages[destination] = page = PdfNavigationCensus.PageNumber(document, destination) is > 0 and int number ? document.Pages[number] : null;
        }

        return page is null ? 0 : document.Pages.IndexOf(page);
    }
}
