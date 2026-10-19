using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Slides;

/// <summary>Slide views and the conservative layout findings of their review.</summary>
internal sealed class SlidesViewAdapter : IProductViewAdapter<SlidesSession>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(SlidesViews.Slides, "Slides", ViewPartKinds.Image)];

    public string ReviewView => SlidesViews.Slides;

    public string LiveView => SlidesViews.Slides;

    public bool VisualInspectionRequired => true;

    public IReadOnlyList<ReviewCheck> Checks => SlidesReviewChecks.All;

    public ViewManifest Render(
        SlidesSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        SlidesView.Render(session, filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        SlidesSession session,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        int inspected = rendered.Parts.Count;
        SlidesReviewDeck deck = SlidesView.ReviewLayout(session, filePath, request, inspected);
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            deck.Slides,
            deck.SlideWidth,
            deck.SlideHeight,
            session.Outputs.License == LicenseState.Evaluation);
        return new ProductReviewAssessment
        {
            Findings = analysis.Findings,
            Coverage =
            [
                ReviewCoverageMetric.Of("slides", deck.SlideCount, "slides"),
                ReviewCoverageMetric.Of("inspectedSlides", inspected, "slides"),
                ReviewCoverageMetric.Of("blankSlides", analysis.BlankSlides, "slides"),
                ReviewCoverageMetric.Of("outsideShapes", analysis.OutsideShapes, "shapes"),
                ReviewCoverageMetric.Of("smallTextShapes", analysis.SmallTextShapes, "shapes"),
                ReviewCoverageMetric.Of("duplicateSlides", analysis.DuplicateSlides, "slides"),
                ReviewCoverageMetric.Of("severeOverlaps", analysis.SevereOverlaps, "pairs"),
                ReviewCoverageMetric.Of("coveredCharts", analysis.CoveredCharts, "charts"),
                ReviewCoverageMetric.Of("highDensitySlides", analysis.HighDensitySlides, "slides"),
                ReviewCoverageMetric.Of("lowDensitySlides", analysis.LowDensitySlides, "slides"),
                ReviewCoverageMetric.Of("textOverlaps", analysis.TextOverlaps, "shapes"),
                ReviewCoverageMetric.Of("textOutsideSlide", analysis.TextOutsideSlide, "shapes"),
                ReviewCoverageMetric.Of("textOverflows", analysis.TextOverflows, "shapes"),
                ReviewCoverageMetric.Of("emptyPlaceholders", analysis.EmptyPlaceholders, "placeholders"),
                ReviewCoverageMetric.Of("lowContrastTexts", analysis.LowContrastTexts, "shapes"),
                .. EnvelopeParts.ExcludedEvaluationMarks(session.Outputs.License, analysis.ExcludedEvaluationMarks ?? 0, "shapes"),
            ],
        };
    }
}
