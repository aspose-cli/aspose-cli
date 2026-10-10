using Aspose.Cli.Sdk.Extensibility;
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
        PresentationInfoResult info = SlidesInfo.Run(session, new PresentationInfoRequest
        {
            Input = filePath,
            Password = request.Password,
        });
        int inspected = rendered.Parts.Count;
        PresentationReadResult read = SlidesRead.Run(session, new PresentationReadRequest
        {
            Input = filePath,
            Slides = inspected == 0 ? null : PageRange.Parse($"1-{inspected}"),
            Scope = PresentationReadScopes.Full,
            MaxCharacters = 1_000_000,
            Password = request.Password,
        });
        SlidesReviewAnalysis analysis = SlidesReviewAnalyzer.Analyze(
            read.Slides,
            info.Presentation.WidthPoints,
            info.Presentation.HeightPoints);
        return new ProductReviewAssessment
        {
            Findings = analysis.Findings,
            Coverage =
            [
                ReviewCoverageMetric.Of("slides", info.Presentation.SlideCount, "slides"),
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
                ReviewCoverageMetric.Of("excludedEvaluationWatermarks", analysis.ExcludedEvaluationWatermarks, "shapes"),
            ],
        };
    }
}
