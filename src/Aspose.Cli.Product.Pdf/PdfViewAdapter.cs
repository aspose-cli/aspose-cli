using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
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

    public bool VisualInspectionRequired => true;

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
            Details = ["pages", "forms"],
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
        int unusualPages = AnalyzePageSizes(info.Pages, findings);
        TextAnalysis text = AnalyzeText(
            read,
            info.Forms?.Fields ?? 0,
            findings);
        FormAnalysis forms = AnalyzeForms(
            port,
            filePath,
            request.Password,
            info.Pdf.Pages,
            info.Forms?.Fields ?? 0,
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
                Metric("pages", info.Pdf.Pages, "pages"),
                Metric("inspectedPages", inspected, "pages"),
                Metric("emptyTextPages", text.EmptyPages, "pages"),
                Metric("lowUtilizationPages", text.LowUtilizationPages, "pages"),
                Metric("outsideTextFragments", layout.Pages.Sum(static page => page.OutsideTextFragments), "fragments"),
                Metric("unusualPageSizes", unusualPages, "pages"),
                Metric("unembeddedFonts", unembeddedFonts, "fonts"),
                Metric("formFields", forms.Fields, "fields"),
                Metric("formFieldsWithoutPage", forms.FieldsWithoutPage, "fields"),
            ],
            Complete = findings.All(static finding => finding.Severity != "error"),
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
            findings.Add(Finding(
                "PDF_TEXT_OUTSIDE_PAGE",
                "warning",
                $"{page.OutsideTextFragments} text fragment(s) extend beyond the page rectangle and may be clipped.",
                $"page {page.Page}"));
        }
    }

    private static int AnalyzePageSizes(
        IReadOnlyList<PdfPageInfo>? pages,
        ICollection<ReviewFinding> findings)
    {
        int unusualPages = 0;
        foreach (PdfPageInfo page in pages ?? [])
        {
            if (IsUnusualPageSize(page))
            {
                unusualPages++;
                findings.Add(Finding(
                    "PDF_UNUSUAL_PAGE_SIZE",
                    "warning",
                    $"Page size {page.WidthPoints:0.##} x {page.HeightPoints:0.##} pt is unusual and may preview poorly.",
                    $"page {page.Number}"));
            }
        }
        return unusualPages;
    }

    private static bool IsUnusualPageSize(PdfPageInfo page) =>
        page.WidthPoints < 72
        || page.HeightPoints < 72
        || page.WidthPoints > 2_880
        || page.HeightPoints > 2_880;

    private static TextAnalysis AnalyzeText(
        PdfReadResult read,
        int formFields,
        ICollection<ReviewFinding> findings)
    {
        HashSet<int> suspectedScans = (read.ScannedPagesSuspected ?? [])
            .ToHashSet();
        int emptyPages = 0;
        int lowUtilizationPages = 0;
        foreach (PdfPageText page in read.Pages)
        {
            if (HasReadableOrScannedContent(page, suspectedScans))
            {
                lowUtilizationPages += AnalyzeTextPage(
                    page,
                    suspectedScans,
                    formFields,
                    findings);
                continue;
            }
            emptyPages++;
            findings.Add(Finding(
                "PDF_PAGE_WITHOUT_READABLE_CONTENT",
                "warning",
                "The page has no readable text and was not identified as a scanned page; inspect it for unintended blank output.",
                $"page {page.Number}"));
        }
        return new TextAnalysis(emptyPages, lowUtilizationPages);
    }

    private static bool HasReadableOrScannedContent(
        PdfPageText page,
        IReadOnlySet<int> suspectedScans) =>
        !string.IsNullOrWhiteSpace(page.Text)
        || suspectedScans.Contains(page.Number);

    private static int AnalyzeTextPage(
        PdfPageText page,
        IReadOnlySet<int> suspectedScans,
        int formFields,
        ICollection<ReviewFinding> findings)
    {
        int lowUtilization = 0;
        if (!suspectedScans.Contains(page.Number)
            && formFields == 0
            && page.Text.Trim().Length is > 0 and < 24)
        {
            lowUtilization = 1;
            findings.Add(Finding(
                "PDF_LOW_PAGE_UTILIZATION",
                "warning",
                "The page contains very little readable content; inspect for an unintended sparse page or pagination break.",
                $"page {page.Number}"));
        }
        if (page.Truncated)
        {
            findings.Add(Finding(
                "PDF_TEXT_ANALYSIS_TRUNCATED",
                "warning",
                "Text analysis reached its extraction budget; visual evidence remains available but structural text checks are incomplete.",
                $"page {page.Number}"));
        }
        return lowUtilization;
    }

    private static FormAnalysis AnalyzeForms(
        IPdfEngine port,
        string filePath,
        string? password,
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
        int fieldsWithoutPage = form.Fields.Count(field =>
            field.Page is null or < 1 || field.Page > pages);
        findings.Add(Finding(
            "PDF_FORM_APPEARANCE_REVIEW_REQUIRED",
            "info",
            $"The PDF contains {formFields} form field(s); inspect every rendered field appearance for stale, clipped, or missing values.",
            "document"));
        if (fieldsWithoutPage > 0)
        {
            findings.Add(Finding(
                "PDF_FORM_FIELD_PAGE_UNRESOLVED",
                "warning",
                $"{fieldsWithoutPage} form field(s) could not be associated with a valid page.",
                "document"));
        }
        return new FormAnalysis(formFields, fieldsWithoutPage);
    }

    private static int AnalyzeFonts(
        IPdfEngine port,
        string filePath,
        string? password,
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
            findings.Add(Finding(
                "PDF_FONTS_NOT_EMBEDDED",
                "warning",
                $"{unembeddedFonts} font resource(s) are not embedded; rendering can vary on another machine.",
                "document"));
        }
        return unembeddedFonts;
    }

    private static ReviewFinding Finding(string code, string severity, string message, string location) => new()
    {
        Code = code,
        Severity = severity,
        Message = message,
        Location = location,
        Hint = "Correct the affected final PDF page, save, and run review again in a new directory.",
    };

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
