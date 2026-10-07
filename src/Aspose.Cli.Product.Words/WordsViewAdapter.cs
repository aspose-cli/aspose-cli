using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Words;

/// <summary>Product-owned page views and the structural heuristics of their review.</summary>
internal sealed class WordsViewAdapter : IProductViewAdapter<IWordsEngine>
{
    private const string RepairHint =
        "Adjust the affected page structure or pagination, save, and run review again in a new directory.";

    public IReadOnlyList<ProductView> Views { get; } =
        [new(WordsViews.Pages, "Pages", ViewPartKinds.Image)];

    public string ReviewView => WordsViews.Pages;

    public string LiveView => WordsViews.Pages;

    public bool VisualInspectionRequired => true;

    public IReadOnlyList<ReviewCheck> Checks => WordsReviewChecks.All;

    public ViewManifest Render(
        IWordsEngine port,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        port.RenderView(filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        IWordsEngine port,
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
        if (info.Document.WordCount == 0
            && (info.Tables?.Count ?? 0) == 0
            && (info.Images?.Count ?? 0) == 0)
        {
            findings.Add(WordsReviewChecks.DocumentEmpty.Finding(
                "The document has no readable text, tables, or images.",
                "document",
                RepairHint));
        }
        AddLayoutFindings(layout, findings);
        if (info.Document.RevisionsPresent)
        {
            findings.Add(WordsReviewChecks.RevisionsPresent.Finding(
                $"The document contains {info.Document.RevisionCount} tracked revision(s), which must not be accepted as a visual-only repair.",
                "document",
                "Disclose the revisions; accept or reject them with accept_revisions or reject_revisions only when the user decides."));
        }

        if (layout.EvaluationMarks > 0 || layout.EvaluationTruncated)
        {
            const string Truncated = "the notice that evaluation mode cut the document short there, so the content after it is missing.";
            string marks = $"{layout.EvaluationMarks} paragraph(s) hold the evaluation text a run without a license saved into the file, with its watermark.";
            findings.Add(WordsReviewChecks.EvaluationMarks.Finding(
                layout.EvaluationMarks == 0 ? $"The document ends with {Truncated}"
                    : layout.EvaluationTruncated ? $"{marks} The document also ends with {Truncated}"
                    : marks,
                "document",
                "Regenerate the document from its original inputs with an Aspose.Words license; editing this file keeps the marks."));
        }

        if (info.Document.CommentCount > 0)
        {
            findings.Add(WordsReviewChecks.CommentsPresent.Finding(
                $"The document contains {info.Document.CommentCount} comment(s).",
                "document",
                "Disclose the comments; remove them with remove_comments only when the user asks."));
        }

        return new ProductReviewAssessment
        {
            Findings = findings,
            Warnings = info.Warnings,
            Coverage =
            [
                Metric("pages", info.Document.PageCount, "pages"),
                Metric("words", info.Document.WordCount, "words"),
                Metric("renderedPages", rendered.Parts.Count, "pages"),
                Metric("blankPages", layout.Pages.Count(static page => !HasVisibleContent(page)), "pages"),
                Metric("lowUtilizationPages", layout.Pages.Count(IsExtremelyLowUtilization), "pages"),
                Metric("outsideObjects", layout.Pages.Sum(static page => page.OutsideObjects), "objects"),
                Metric("orphanedHeadings", layout.OrphanedHeadings.Count, "headings"),
            ],
        };
    }

    private static void AddLayoutFindings(
        WordsReviewLayout layout,
        ICollection<ReviewFinding> findings)
    {
        foreach (WordsReviewPageLayout page in layout.Pages)
        {
            string location = $"page {page.Page}";
            string part = WordsViews.PagePart(page.Page);
            if (!HasVisibleContent(page))
            {
                findings.Add(WordsReviewChecks.PageBlank.Finding(
                    "The fixed-page layout contains no visible body text, table rows, or drawing objects.",
                    location,
                    RepairHint,
                    part));
            }
            else if (IsExtremelyLowUtilization(page))
            {
                findings.Add(WordsReviewChecks.PageUtilizationLow.Finding(
                    $"Visible body content occupies only {page.ContentAreaRatio:P1} of the page bounding area.",
                    location,
                    RepairHint,
                    part));
            }
            if (page.MinimumFontSize is > 0 and < 7)
            {
                findings.Add(WordsReviewChecks.TextTooSmall.Finding(
                    $"Visible body text uses a minimum font size of {page.MinimumFontSize:0.#} pt.",
                    location,
                    RepairHint,
                    part));
            }
            if (page.MaximumFontSize is > 72)
            {
                findings.Add(WordsReviewChecks.TextTooLarge.Finding(
                    $"Visible body text uses a maximum font size of {page.MaximumFontSize:0.#} pt.",
                    location,
                    RepairHint,
                    part));
            }
            if (page.OutsideObjects > 0)
            {
                string names = string.Join(", ", page.OutsideObjectNames);
                findings.Add(WordsReviewChecks.ObjectOutsidePage.Finding(
                    $"{page.OutsideObjects} drawing object(s) extend beyond the physical page boundary: {names}.",
                    location,
                    RepairHint,
                    part));
            }
            if (page.ExplicitPageBreaks > 1)
            {
                findings.Add(WordsReviewChecks.PageBreaksExcessive.Finding(
                    $"The page contains {page.ExplicitPageBreaks} explicit page-break controls.",
                    location,
                    RepairHint,
                    part));
            }
        }

        foreach (WordsReviewHeadingLayout heading in layout.OrphanedHeadings)
        {
            findings.Add(WordsReviewChecks.HeadingOrphaned.Finding(
                $"Heading level {heading.Level} '{heading.Text}' follows prior page content but its body starts on the next page.",
                $"page {heading.Page}, block {heading.Block}",
                RepairHint,
                WordsViews.PagePart(heading.Page)));
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

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
