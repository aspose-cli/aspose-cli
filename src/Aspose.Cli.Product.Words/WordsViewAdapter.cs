using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Words;

/// <summary>Product-owned page views and the structural heuristics of their review.</summary>
internal sealed class WordsViewAdapter : IProductViewAdapter<IDocumentEngine>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(WordsViews.Pages, "Pages", ViewPartKinds.Image)];

    public string ReviewView => WordsViews.Pages;

    public string LiveView => WordsViews.Pages;

    public bool VisualInspectionRequired => true;

    public ViewManifest Render(
        IDocumentEngine port,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        port.RenderView(filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        IDocumentEngine port,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        DocumentInfoResult info = port.GetInfo(filePath, new DocumentInfoRequest
        {
            Details = ["sections", "outline", "images", "tables", "fonts"],
            Password = request.Password,
        });
        if (port is not IWordsReviewLayoutPort layoutPort)
        {
            throw new InvalidOperationException(
                "The active Words engine does not expose the product-owned review layout port.");
        }
        WordsReviewLayout layout = layoutPort.InspectReviewLayout(
            filePath,
            request.Password,
            rendered.Parts.Count);

        var findings = new List<ReviewFinding>();
        if (info.Document.Words == 0
            && (info.Tables?.Count ?? 0) == 0
            && (info.Images?.Count ?? 0) == 0)
        {
            findings.Add(Finding(
                "WORDS_DOCUMENT_EMPTY",
                "warning",
                "The document has no readable text, tables, or images.",
                "document"));
        }
        AddLayoutFindings(layout, findings);
        if (info.Document.RevisionsPresent)
        {
            findings.Add(Finding(
                "WORDS_REVISIONS_PRESENT",
                "info",
                "Tracked revisions are present and must not be accepted as a visual-only repair.",
                "document"));
        }

        return new ProductReviewAssessment
        {
            Findings = findings,
            Warnings = info.Warnings,
            Coverage =
            [
                Metric("pages", info.Document.Pages, "pages"),
                Metric("words", info.Document.Words, "words"),
                Metric("renderedPages", rendered.Parts.Count, "pages"),
                Metric("blankPages", layout.Pages.Count(static page => !HasVisibleContent(page)), "pages"),
                Metric("lowUtilizationPages", layout.Pages.Count(IsExtremelyLowUtilization), "pages"),
                Metric("outsideObjects", layout.Pages.Sum(static page => page.OutsideObjects), "objects"),
                Metric("orphanedHeadings", layout.OrphanedHeadings.Count, "headings"),
            ],
            Complete = findings.All(static finding => finding.Severity != "error"),
        };
    }

    private static void AddLayoutFindings(
        WordsReviewLayout layout,
        ICollection<ReviewFinding> findings)
    {
        foreach (WordsReviewPageLayout page in layout.Pages)
        {
            string location = $"page {page.Page}";
            if (!HasVisibleContent(page))
            {
                findings.Add(Finding(
                    "WORDS_BLANK_PAGE",
                    "warning",
                    "The fixed-page layout contains no visible body text, table rows, or drawing objects.",
                    location));
            }
            else if (IsExtremelyLowUtilization(page))
            {
                findings.Add(Finding(
                    "WORDS_PAGE_UTILIZATION_LOW",
                    "warning",
                    $"Visible body content occupies only {page.ContentAreaRatio:P1} of the page bounding area.",
                    location));
            }
            if (page.MinimumFontSize is > 0 and < 7)
            {
                findings.Add(Finding(
                    "WORDS_FONT_SIZE_TOO_SMALL",
                    "warning",
                    $"Visible body text uses a minimum font size of {page.MinimumFontSize:0.#} pt.",
                    location));
            }
            if (page.MaximumFontSize is > 72)
            {
                findings.Add(Finding(
                    "WORDS_FONT_SIZE_UNUSUALLY_LARGE",
                    "warning",
                    $"Visible body text uses a maximum font size of {page.MaximumFontSize:0.#} pt.",
                    location));
            }
            if (page.OutsideObjects > 0)
            {
                string names = string.Join(", ", page.OutsideObjectNames);
                findings.Add(Finding(
                    "WORDS_OBJECT_OUTSIDE_PAGE",
                    "warning",
                    $"{page.OutsideObjects} drawing object(s) extend beyond the physical page boundary: {names}.",
                    location));
            }
            if (page.ExplicitPageBreaks > 1)
            {
                findings.Add(Finding(
                    "WORDS_PAGE_BREAKS_EXCESSIVE",
                    "warning",
                    $"The page contains {page.ExplicitPageBreaks} explicit page-break controls.",
                    location));
            }
        }

        foreach (WordsReviewHeadingLayout heading in layout.OrphanedHeadings)
        {
            findings.Add(Finding(
                "WORDS_HEADING_ORPHANED",
                "warning",
                $"Heading level {heading.Level} '{heading.Text}' follows prior page content but its body starts on the next page.",
                $"page {heading.Page}, block {heading.Block}"));
        }
    }

    private static bool HasVisibleContent(WordsReviewPageLayout page) =>
        page.ContentLeft is not null || page.VisualObjects > 0;

    private static bool IsExtremelyLowUtilization(WordsReviewPageLayout page) =>
        page.Page > 1
        && HasVisibleContent(page)
        && page.ContentAreaRatio < 0.025
        && page.VisibleCharacters < 80
        && page.VisualObjects <= 1;

    private static ReviewFinding Finding(string code, string severity, string message, string location) => new()
    {
        Code = code,
        Severity = severity,
        Message = message,
        Location = location,
        Hint = "Adjust the affected page structure or pagination, save, and run review again in a new directory.",
    };

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
