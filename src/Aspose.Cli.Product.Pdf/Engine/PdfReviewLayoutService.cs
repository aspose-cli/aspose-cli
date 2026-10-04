using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Projects the displayed page sizes and conservative text-boundary facts from the real PDF layout.</summary>
internal sealed class PdfReviewLayoutService
{
    private const double PageBoundaryTolerance = 0.5;
    private readonly ILicenseGate _licenseGate;
    private readonly PdfDocumentLoader _loader;

    internal PdfReviewLayoutService(
        ILicenseGate licenseGate,
        PdfDocumentLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    internal PdfReviewLayout Inspect(
        string filePath,
        string? password,
        int maxPages)
    {
        _ = _licenseGate.EnsureApplied();
        using LoadedPdf loaded = _loader.Open(filePath, password);
        int inspectedPages = Math.Min(loaded.Document.Pages.Count, maxPages);
        var pages = new List<PdfReviewPageLayout>(inspectedPages);
        for (int pageNumber = 1; pageNumber <= inspectedPages; pageNumber++)
        {
            pages.Add(InspectPage(loaded.Document.Pages[pageNumber], pageNumber));
        }
        return new PdfReviewLayout(pages);
    }

    private static PdfReviewPageLayout InspectPage(Page page, int pageNumber)
    {
        var absorber = new TextFragmentAbsorber();
        page.Accept(absorber);
        int fragments = 0;
        int outsideFragments = 0;
        foreach (TextFragment fragment in absorber.TextFragments)
        {
            if (string.IsNullOrWhiteSpace(fragment.Text))
            {
                continue;
            }
            fragments++;
            if (IsOutsidePage(fragment.Rectangle, page.Rect))
            {
                outsideFragments++;
            }
        }
        Rectangle displayed = page.GetPageRect(considerRotation: true);
        return new PdfReviewPageLayout(
            pageNumber, displayed.Width, displayed.Height, fragments, outsideFragments, ImageCoverage(page));
    }

    /// <summary>The share of the page its image placements cover, overlaps counted twice, at most 1.</summary>
    private static double ImageCoverage(Page page)
    {
        var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
        page.Accept(absorber);
        double area = absorber.ImagePlacements.Sum(static placement =>
            placement.Rectangle.Width * placement.Rectangle.Height);
        return Math.Min(1d, area / Math.Max(1d, page.Rect.Width * page.Rect.Height));
    }

    private static bool IsOutsidePage(Rectangle text, Rectangle page) =>
        text.LLX < page.LLX - PageBoundaryTolerance
        || text.LLY < page.LLY - PageBoundaryTolerance
        || text.URX > page.URX + PageBoundaryTolerance
        || text.URY > page.URY + PageBoundaryTolerance;
}
