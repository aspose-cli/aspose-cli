using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Slides;

/// <summary>Slide views and the conservative layout findings of their review.</summary>
internal sealed class SlidesViewAdapter : IProductViewAdapter<ISlidesEngine>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(SlidesViews.Slides, "Slides", ViewPartKinds.Image)];

    public string ReviewView => SlidesViews.Slides;

    public string LiveView => SlidesViews.Slides;

    public bool VisualInspectionRequired => true;

    public IReadOnlyList<ReviewCheck> Checks => SlidesReviewChecks.All;

    public ViewManifest Render(
        ISlidesEngine port,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        port.RenderView(filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        ISlidesEngine port,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        PresentationInfoResult info = port.GetInfo(filePath, new PresentationInfoRequest
        {
            Password = request.Password,
        });
        int inspected = rendered.Parts.Count;
        PresentationReadResult read = port.Read(filePath, new PresentationReadRequest
        {
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
                Metric("slides", info.Presentation.SlideCount, "slides"),
                Metric("inspectedSlides", inspected, "slides"),
                Metric("blankSlides", analysis.BlankSlides, "slides"),
                Metric("outsideShapes", analysis.OutsideShapes, "shapes"),
                Metric("smallTextShapes", analysis.SmallTextShapes, "shapes"),
                Metric("duplicateSlides", analysis.DuplicateSlides, "slides"),
                Metric("severeOverlaps", analysis.SevereOverlaps, "pairs"),
                Metric("coveredCharts", analysis.CoveredCharts, "charts"),
                Metric("highDensitySlides", analysis.HighDensitySlides, "slides"),
                Metric("lowDensitySlides", analysis.LowDensitySlides, "slides"),
                Metric("textOverlaps", analysis.TextOverlaps, "shapes"),
                Metric("textOutsideSlide", analysis.TextOutsideSlide, "shapes"),
                Metric("textOverflows", analysis.TextOverflows, "shapes"),
                Metric("emptyPlaceholders", analysis.EmptyPlaceholders, "placeholders"),
                Metric("lowContrastTexts", analysis.LowContrastTexts, "shapes"),
                Metric("excludedEvaluationWatermarks", analysis.ExcludedEvaluationWatermarks, "shapes"),
            ],
        };
    }

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
