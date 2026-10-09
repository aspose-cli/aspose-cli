using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Pdf;
using Aspose.Pdf.Devices;
using SkiaSharp;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Writes PDF pages as images for render, convert and the pages view.</summary>
internal static class PdfRaster
{
    /// <summary>Marks the page number in a multi-page output name: <c>report.p3.png</c>.</summary>
    internal const string PagePart = "p";

    /// <summary>
    /// Writes one page as an image. Every page raster passes the shared pixel guard here,
    /// before the engine allocates the bitmap. A <paramref name="grid"/> is drawn on the raster,
    /// never into the document.
    /// </summary>
    internal static void RenderPage(
        ResourceBudgetLedger budgets,
        Document document,
        int pageNumber,
        string format,
        int dpi,
        Stream stream,
        PdfRenderGrid? grid = null)
    {
        Page page = document.Pages[pageNumber];
        if (format != "svg")
        {
            EnsurePageFits(budgets, page, dpi);
        }

        if (grid is not null)
        {
            using var raster = new MemoryStream();
            new PngDevice(new Resolution(dpi)).Process(page, raster);
            raster.Position = 0;
            Rectangle box = page.GetPageRect(considerRotation: true);
            PdfGridOverlay.Draw(raster, grid, box.Width, box.Height, dpi, format, stream);
            return;
        }

        switch (format)
        {
            case "png":
                new PngDevice(new Resolution(dpi)).Process(page, stream);
                break;
            case "jpeg":
                new JpegDevice(new Resolution(dpi), 95).Process(page, stream);
                break;
            case "svg":
                using (Document selected = Select(document, [pageNumber]))
                {
                    selected.Save(stream, SaveFormat.Svg);
                }

                break;
            default:
                throw new InvalidOperationException($"'{format}' is not a PDF render format.");
        }
    }

    /// <summary>
    /// PDF-RENDER-THIN-GLYPH: the engine drops thin glyph strokes, such as the underscores of a
    /// signature line, at some positions below about 300 DPI. Review evidence is rendered at twice
    /// its resolution and scaled down by averaging, so such a stroke shows as grey at the
    /// evidence size. A page whose double-size raster exceeds the pixel budget renders directly.
    /// </summary>
    internal static void RenderEvidencePage(ResourceBudgetLedger budgets, Document document, int pageNumber, int dpi, Stream stream)
    {
        Page page = document.Pages[pageNumber];
        int sampled = dpi * 2;
        (long width, long height) = PagePixels(page, sampled);
        long maxPixels = budgets.Limit(ResourceBudgetKinds.RasterPixels);
        if (width <= 0 || height <= 0 || width > maxPixels / height)
        {
            RenderPage(budgets, document, pageNumber, "png", dpi, stream);
            return;
        }

        using var raster = new MemoryStream();
        new PngDevice(new Resolution(sampled)).Process(page, raster);
        raster.Position = 0;
        using SKBitmap full = SKBitmap.Decode(raster)
            ?? throw new InvalidOperationException("The rendered page image could not be decoded.");
        // At exactly half the size, linear filtering averages each 2 x 2 block of pixels.
        var info = new SKImageInfo((full.Width + 1) / 2, (full.Height + 1) / 2, full.ColorType, full.AlphaType);
        using SKBitmap scaled = full.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            ?? throw new InvalidOperationException("The rendered page image could not be scaled.");
        using SKData encoded = scaled.Encode(SKEncodedImageFormat.Png, 100);
        encoded.SaveTo(stream);
    }

    internal static void EnsurePageFits(ResourceBudgetLedger budgets, Page page, int dpi)
    {
        (long width, long height) = PagePixels(page, dpi);
        RenderPixelGuard.EnsureFits(budgets, width, height, dpi, "Render fewer or smaller pages, or lower --dpi.");
    }

    /// <summary>The pixel size of <paramref name="page"/> rendered at <paramref name="dpi"/>.</summary>
    private static (long Width, long Height) PagePixels(Page page, int dpi) =>
        ((long)Math.Ceiling(page.Rect.Width / 72d * dpi), (long)Math.Ceiling(page.Rect.Height / 72d * dpi));
}
