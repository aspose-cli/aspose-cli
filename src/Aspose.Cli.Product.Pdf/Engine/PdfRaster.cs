using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Pdf;
using Aspose.Pdf.Devices;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Writes PDF pages as images for render, convert and the pages view.</summary>
internal static class PdfRaster
{
    /// <summary>
    /// Writes one page as an image: the engine's page device for a bitmap, or the page alone saved
    /// as SVG. Every page raster passes the shared pixel guard here, before the engine allocates the bitmap.
    /// </summary>
    internal static void RenderPage(
        ResourceBudgetLedger budgets,
        Document document,
        int pageNumber,
        string format,
        int dpi,
        Stream stream)
    {
        Page page = document.Pages[pageNumber];
        PdfEngineFormat image = PdfEngineFormats.Of(format);
        if (image.Write != PdfEngineWrite.PageImage)
        {
            throw new InvalidOperationException($"'{format}' is not a PDF render format.");
        }

        if (image.Device is not null)
        {
            EnsurePageFits(budgets, page, dpi);
        }

        if (image.Device is { } device)
        {
            device(new Resolution(dpi)).Process(page, stream);
            return;
        }

        using var selected = new Document();
        selected.Pages.Add(page);
        selected.Save(stream, PdfEngineFormats.Save(format));
    }

    internal static void EnsurePageFits(ResourceBudgetLedger budgets, Page page, int dpi)
    {
        (long width, long height) = PagePixels(page, dpi);
        RenderPixelGuard.EnsureFits(budgets, width, height, dpi);
    }

    /// <summary>The pixel size of <paramref name="page"/> rendered at <paramref name="dpi"/>.</summary>
    private static (long Width, long Height) PagePixels(Page page, int dpi) =>
        ((long)Math.Ceiling(page.Rect.Width / 72d * dpi), (long)Math.Ceiling(page.Rect.Height / 72d * dpi));
}
