using System.Net;
using System.Text;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides;

/// <summary>Slide-aware review evidence with conservative layout findings.</summary>
internal sealed class SlidesReviewAdapter : IProductReviewAdapter<IPresentationEngine>
{
    public string DefaultView => SlidesPreviewAdapter.SlidesView;

    public IReadOnlyList<string> Views { get; } = [SlidesPreviewAdapter.SlidesView];

    public bool VisualInspectionRequired => true;

    public ProductReviewRenderer CreateRenderer(
        IPresentationEngine port,
        string filePath,
        ProductReviewRequest request) => directory =>
    {
        PresentationInfoResult info = port.GetInfo(filePath, new PresentationInfoRequest
        {
            Password = request.Password,
        });
        int inspected = Math.Min(info.Presentation.Slides, request.MaxItems);
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

        SlidesRenderResult rendered = port.Render(filePath, new PresentationRenderRequest
        {
            TargetFormatId = "png",
            OutputPath = Path.Combine(directory, "slide.png"),
            Slides = inspected == 0 ? null : PageRange.Parse($"1-{inspected}"),
            Width = 1600,
            Password = request.Password,
        });
        const string entry = "slides-review.html";
        File.WriteAllText(
            Path.Combine(directory, entry),
            Gallery(rendered.Outputs),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new ProductReviewRenderOutcome(
            entry,
            rendered.Input.Format,
            rendered.Input.SizeBytes)
        {
            VisualInspectionRequired = true,
            Findings = analysis.Findings,
            Coverage =
            [
                Metric("slides", info.Presentation.Slides, "slides"),
                Metric("inspectedSlides", inspected, "slides"),
                Metric("blankSlides", analysis.BlankSlides, "slides"),
                Metric("outsideShapes", analysis.OutsideShapes, "shapes"),
                Metric("smallTextShapes", analysis.SmallTextShapes, "shapes"),
                Metric("duplicateSlides", analysis.DuplicateSlides, "slides"),
                Metric("severeOverlaps", analysis.SevereOverlaps, "pairs"),
                Metric("coveredCharts", analysis.CoveredCharts, "charts"),
                Metric("highDensitySlides", analysis.HighDensitySlides, "slides"),
                Metric("lowDensitySlides", analysis.LowDensitySlides, "slides"),
            ],
            ExpectedItems = info.Presentation.Slides,
            RenderedItems = rendered.Outputs.Count,
            Complete = analysis.Findings.All(static finding => finding.Severity != "error"),
        };
    };

    private static string Gallery(IReadOnlyList<SlideRenderOutput> slides)
    {
        var body = new StringBuilder();
        foreach (SlideRenderOutput slide in slides)
        {
            string name = WebUtility.HtmlEncode(Path.GetFileName(slide.Output.Path));
            body.Append("<figure><img src=\"").Append(name)
                .Append("\" alt=\"Slide ").Append(slide.Slide)
                .Append("\"><figcaption>Slide ").Append(slide.Slide)
                .Append("</figcaption></figure>");
        }
        return "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<style>body{margin:0;padding:24px;background:#e9e8ed;font:14px system-ui;color:#29263a}"
            + "figure{margin:0 auto 28px;max-width:1200px}img{display:block;width:100%;height:auto;background:white;box-shadow:0 8px 28px #211d3b24}"
            + "figcaption{text-align:center;margin-top:8px}</style></head><body>"
            + body + "</body></html>";
    }

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
