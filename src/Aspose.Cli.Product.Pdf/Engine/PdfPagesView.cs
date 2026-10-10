using System.Globalization;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Views;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Renders the parts of the pages view for review and live display.</summary>
internal static class PdfPagesView
{
    /// <summary>Renders the pages of one view, opening the document once.</summary>
    internal static ViewManifest Render(
        PdfSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts)
    {
        const int evidenceDpi = 150;
        const double cssDpi = 96;
        ArgumentNullException.ThrowIfNull(artifacts);
        _ = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        bool evidence = request.Purpose != ViewPurpose.Display;
        int dpi = evidence ? evidenceDpi : RenderPixelGuard.DefaultDpi;
        int total = loaded.Document.Pages.Count;
        int count = Math.Min(total, request.MaxPartCount);
        var parts = new List<ViewPart>(count);
        for (int pageNumber = 1; pageNumber <= count; pageNumber++)
        {
            Page page = loaded.Document.Pages[pageNumber];
            string file = string.Create(CultureInfo.InvariantCulture, $"page-{pageNumber:0000}.png");
            int number = pageNumber;
            artifacts.Write(
                file,
                stream =>
                {
                    if (evidence)
                    {
                        PdfRaster.RenderEvidencePage(session.Budgets, loaded.Document, number, dpi, stream);
                    }
                    else
                    {
                        PdfRaster.RenderPage(session.Budgets, loaded.Document, number, "png", dpi, stream);
                    }
                });
            parts.Add(new ViewPart
            {
                Id = PdfViews.PagePart(pageNumber),
                Label = string.Create(CultureInfo.InvariantCulture, $"Page {pageNumber}"),
                File = file,
                Kind = ViewPartKinds.Image,
                Width = Math.Max(1, (int)Math.Ceiling(page.Rect.Width / 72d * cssDpi)),
                Height = Math.Max(1, (int)Math.Ceiling(page.Rect.Height / 72d * cssDpi)),
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["size"] = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{page.Rect.Width:0.##} × {page.Rect.Height:0.##} pt"),
                },
            });
        }

        return new ViewManifest
        {
            View = PdfViews.Pages,
            SourceFormat = "pdf",
            SourceSizeBytes = new FileInfo(filePath).Length,
            SourceEncrypted = loaded.Document.IsEncrypted,
            TotalPartCount = total,
            Parts = parts,
        };
    }
}
