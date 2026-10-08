using System.Globalization;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Page views and the conservative PDF quality findings of their review.</summary>
internal sealed class PdfViewAdapter : IProductViewAdapter<IPdfEngine>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(PdfViews.Pages, "Pages", ViewPartKinds.Image)];

    public string ReviewView => PdfViews.Pages;

    public string LiveView => PdfViews.Pages;

    private const string Hint =
        "Correct the affected final PDF page, save, and run review again in a new directory.";

    public bool VisualInspectionRequired => true;

    public IReadOnlyList<ReviewCheck> Checks => PdfReviewChecks.All;

    public ViewManifest Render(
        IPdfEngine port,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        port.RenderView(filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        IPdfEngine port,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        PdfInfoResult info = port.GetInfo(filePath, new PdfInfoRequest
        {
            Details = ["forms"],
            Password = request.Password,
        });
        int inspected = rendered.Parts.Count;
        if (port is not IPdfReviewLayoutPort layoutPort)
        {
            throw new InvalidOperationException(
                "The active PDF engine does not expose the product-owned review layout port.");
        }
        PdfReviewLayout layout = layoutPort.InspectReviewLayout(
            filePath,
            request.Password,
            inspected);
        PdfReadResult read = port.Read(filePath, new PdfReadRequest
        {
            Pages = inspected == 0 ? null : PageRange.Parse($"1-{inspected}"),
            Mode = PdfReadModes.Plain,
            MaxCharacters = 1_000_000,
            Password = request.Password,
        });
        var findings = new List<ReviewFinding>();
        AnalyzeTextBounds(layout, findings);
        AnalyzeCoveredText(layout, findings);
        AnalyzeWatermarks(layout, findings);
        int unusualPages = AnalyzePageSizes(layout, findings);
        TextAnalysis text = AnalyzeText(
            read,
            layout,
            info.Forms?.FieldCount ?? 0,
            findings);
        IReadOnlyList<int> scannedPages = AnalyzeScannedPages(read, findings);
        FormAnalysis forms = AnalyzeForms(
            port,
            filePath,
            request.Password,
            info.Pdf.PageCount,
            info.Forms?.FieldCount ?? 0,
            findings);
        int unembeddedFonts = AnalyzeFonts(
            port,
            filePath,
            request.Password,
            read.Pages,
            findings);
        return new ProductReviewAssessment
        {
            Findings = findings,
            Coverage =
            [
                Metric("pages", info.Pdf.PageCount, "pages"),
                Metric("inspectedPages", inspected, "pages"),
                Metric("emptyTextPages", text.EmptyPages, "pages"),
                Metric("scannedPages", scannedPages.Count, "pages"),
                Metric("lowUtilizationPages", text.LowUtilizationPages, "pages"),
                Metric("outsideTextFragments", layout.Pages.Sum(static page => page.OutsideTextFragments), "fragments"),
                Metric("coveredTextFragments", layout.Pages.Sum(static page => page.CoveredTextFragments), "fragments"),
                Metric("unusualPageSizes", unusualPages, "pages"),
                Metric("unembeddedFonts", unembeddedFonts, "fonts"),
                Metric("formFields", forms.Fields, "fields"),
                Metric("formFieldsWithoutPage", forms.FieldsWithoutPage, "fields"),
            ],
        };
    }

    private static void AnalyzeTextBounds(
        PdfReviewLayout layout,
        ICollection<ReviewFinding> findings)
    {
        foreach (PdfReviewPageLayout page in layout.Pages)
        {
            if (page.OutsideTextFragments == 0)
            {
                continue;
            }
            findings.Add(PdfReviewChecks.TextOutsidePage.Finding(
                $"{page.OutsideTextFragments} text fragment(s) extend beyond the page rectangle and may be clipped.",
                $"page {page.Page}",
                Hint,
                PdfViews.PagePart(page.Page)));
        }
    }

    /// <summary>
    /// The pages where text lies under an opaque box. A redaction leaves such a box, and the
    /// engine moves the text after a removed value under it (PDF-REDACT-TEXT-SHIFT).
    /// </summary>
    private static void AnalyzeCoveredText(
        PdfReviewLayout layout,
        ICollection<ReviewFinding> findings)
    {
        foreach (PdfReviewPageLayout page in layout.Pages.Where(static page => page.CoveredTextFragments > 0))
        {
            findings.Add(PdfReviewChecks.TextCovered.Finding(
                $"{page.CoveredTextFragments} text fragment(s) lie under an opaque box, such as a redaction cover, so the page does not show them although the file still contains them.",
                $"page {page.Page}",
                "Compare the page image with the text 'pdf query pages' reads. When a redaction moved the text beside a removed value under its cover, redact the source document and create the PDF again; that text cannot be moved back in the PDF.",
                PdfViews.PagePart(page.Page)));
        }
    }

    /// <summary>The pages whose text holds the evaluation notice of any Aspose product.</summary>
    private static void AnalyzeWatermarks(
        PdfReviewLayout layout,
        ICollection<ReviewFinding> findings)
    {
        foreach (PdfReviewPageLayout page in layout.Pages)
        {
            if (page.EvaluationProduct is not { } product)
            {
                continue;
            }
            findings.Add(PdfReviewChecks.EvaluationWatermark.Finding(
                $"The page carries the evaluation watermark of {product}, saved into the file by a run without a license.",
                $"page {page.Page}",
                $"Regenerate the file from its original inputs with a license for {product}; editing this file keeps the watermark.",
                PdfViews.PagePart(page.Page)));
        }
    }

    private static int AnalyzePageSizes(
        PdfReviewLayout layout,
        ICollection<ReviewFinding> findings)
    {
        int unusualPages = 0;
        foreach (PdfReviewPageLayout page in layout.Pages)
        {
            if (IsUnusualPageSize(page))
            {
                unusualPages++;
                findings.Add(PdfReviewChecks.PageSizeUnusual.Finding(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Page size {page.WidthPoints:0.##} x {page.HeightPoints:0.##} pt is unusual and may preview poorly."),
                    $"page {page.Page}",
                    Hint,
                    PdfViews.PagePart(page.Page)));
            }
        }
        return unusualPages;
    }

    private static bool IsUnusualPageSize(PdfReviewPageLayout page) =>
        page.WidthPoints < 72
        || page.HeightPoints < 72
        || page.WidthPoints > 2_880
        || page.HeightPoints > 2_880;

    /// <summary>
    /// Pages whose images cover at least this share of the page carry their content as images,
    /// like a slide with a picture; the text checks treat them, and suspected scans, as image pages.
    /// </summary>
    private const double PicturedPageCoverage = 0.25;

    private static TextAnalysis AnalyzeText(
        PdfReadResult read,
        PdfReviewLayout layout,
        int formFields,
        ICollection<ReviewFinding> findings)
    {
        HashSet<int> imagePages = (read.ScannedPagesSuspected ?? [])
            .Concat(layout.Pages.Where(static page => page.ImageCoverage >= PicturedPageCoverage)
                .Select(static page => page.Page))
            .ToHashSet();
        int emptyPages = 0;
        int lowUtilizationPages = 0;
        foreach (PdfPageText page in read.Pages)
        {
            if (HasTextOrImageContent(page, imagePages))
            {
                lowUtilizationPages += AnalyzeTextPage(
                    page,
                    imagePages,
                    formFields,
                    findings);
                continue;
            }
            emptyPages++;
            findings.Add(PdfReviewChecks.PageWithoutReadableContent.Finding(
                "The page has no readable text, no images covering a quarter of it and does not look scanned; inspect it for unintended blank output.",
                $"page {page.Page}",
                Hint,
                PdfViews.PagePart(page.Page)));
        }
        return new TextAnalysis(emptyPages, lowUtilizationPages);
    }

    /// <summary>The pages <c>pdf query pages</c> reports as SCANNED_PAGES_SUSPECTED, one finding each.</summary>
    private static IReadOnlyList<int> AnalyzeScannedPages(
        PdfReadResult read,
        ICollection<ReviewFinding> findings)
    {
        IReadOnlyList<int> pages = read.ScannedPagesSuspected ?? [];
        foreach (int page in pages)
        {
            findings.Add(PdfReviewChecks.PageWithoutTextLayer.Finding(
                "The page has no extractable text and an image fills its width or height, as on a scan; search and redact_text do not reach its content.",
                $"page {page}",
                "Read the page from its review image, and hide content on it with redact_area, whose coordinates 'aspose-cli pdf render --grid 50' labels.",
                PdfViews.PagePart(page)));
        }
        return pages;
    }

    private static bool HasTextOrImageContent(
        PdfPageText page,
        IReadOnlySet<int> imagePages) =>
        !string.IsNullOrWhiteSpace(page.Text)
        || imagePages.Contains(page.Page);

    private static int AnalyzeTextPage(
        PdfPageText page,
        IReadOnlySet<int> imagePages,
        int formFields,
        ICollection<ReviewFinding> findings)
    {
        int lowUtilization = 0;
        if (!imagePages.Contains(page.Page)
            && formFields == 0
            && page.Text.Trim().Length is > 0 and < 24)
        {
            lowUtilization = 1;
            findings.Add(PdfReviewChecks.PageUtilizationLow.Finding(
                "The page carries very little readable text and no images covering a quarter of it; inspect for an unintended sparse page or pagination break.",
                $"page {page.Page}",
                Hint,
                PdfViews.PagePart(page.Page)));
        }
        if (page.Truncated)
        {
            findings.Add(PdfReviewChecks.TextAnalysisTruncated.Finding(
                "Text analysis reached its extraction budget; visual evidence remains available but structural text checks are incomplete.",
                $"page {page.Page}",
                Hint,
                PdfViews.PagePart(page.Page)));
        }
        return lowUtilization;
    }

    private static FormAnalysis AnalyzeForms(
        IPdfEngine port,
        string filePath,
        Secret? password,
        int pages,
        int formFields,
        ICollection<ReviewFinding> findings)
    {
        if (formFields == 0)
        {
            return new FormAnalysis(0, 0);
        }
        PdfFormResult form = port.ReadForm(filePath, new PdfFormReadRequest
        {
            Password = password,
        });
        int fieldsWithoutPage = form.Fields
            .Where(field => field.Page is null or < 1 || field.Page > pages)
            .Select(static field => field.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();
        findings.Add(PdfReviewChecks.FormAppearanceReviewRequired.Finding(
            $"The PDF contains {formFields} form field(s); inspect every rendered field appearance for stale, clipped, or missing values.",
            "document",
            Hint));
        if (fieldsWithoutPage > 0)
        {
            findings.Add(PdfReviewChecks.FormFieldPageUnresolved.Finding(
                $"{fieldsWithoutPage} form field(s) could not be associated with a valid page.",
                "document",
                Hint));
        }
        return new FormAnalysis(formFields, fieldsWithoutPage);
    }

    private static int AnalyzeFonts(
        IPdfEngine port,
        string filePath,
        Secret? password,
        IReadOnlyList<PdfPageText> pages,
        ICollection<ReviewFinding> findings)
    {
        if (!pages.Any(static page => !string.IsNullOrWhiteSpace(page.Text)))
        {
            return 0;
        }
        IReadOnlyList<PdfFontInfo> fonts = port.GetInfo(filePath, new PdfInfoRequest
        {
            Details = ["fonts"],
            Password = password,
        }).Fonts ?? [];
        int unembeddedFonts = fonts.Count(static font =>
            !font.Embedded && !IsPortableBase14(font.Name));
        if (unembeddedFonts > 0)
        {
            findings.Add(PdfReviewChecks.FontsNotEmbedded.Finding(
                $"{unembeddedFonts} font resource(s) are not embedded; rendering can vary on another machine.",
                "document",
                Hint));
        }
        return unembeddedFonts;
    }

    private static bool IsPortableBase14(string name)
    {
        int subsetSeparator = name.IndexOf('+', StringComparison.Ordinal);
        string normalized = subsetSeparator >= 0 ? name[(subsetSeparator + 1)..] : name;
        return normalized is
            "Courier" or "Courier-Bold" or "Courier-Oblique" or "Courier-BoldOblique"
            or "Helvetica" or "Helvetica-Bold" or "Helvetica-Oblique" or "Helvetica-BoldOblique"
            or "Times-Roman" or "Times-Bold" or "Times-Italic" or "Times-BoldItalic"
            or "Symbol" or "ZapfDingbats";
    }

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };

    private sealed record TextAnalysis(
        int EmptyPages,
        int LowUtilizationPages);

    private sealed record FormAnalysis(
        int Fields,
        int FieldsWithoutPage);
}
