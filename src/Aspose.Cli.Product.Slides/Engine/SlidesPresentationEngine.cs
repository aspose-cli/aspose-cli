using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Ports;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Stable Slides port facade. Read, production and edit concerns are composed
/// explicitly and remain internal to this product.
/// </summary>
internal sealed class SlidesPresentationEngine : IPresentationEngine
{
    private readonly SlidesReadService _reads;
    private readonly SlidesProductionService _production;
    private readonly SlidesMutationService _mutations;

    public SlidesPresentationEngine(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(writer);

        var loader = new Engine.Mapping.SlidesPresentationLoader(
            resourceBudgets);
        _reads = new SlidesReadService(licenseGate, loader);
        _production = new SlidesProductionService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
        _mutations = new SlidesMutationService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
    }

    public PresentationInfoResult GetInfo(string filePath, PresentationInfoRequest request) =>
        _reads.GetInfo(filePath, request);

    public PresentationReadResult Read(string filePath, PresentationReadRequest request) =>
        _reads.Read(filePath, request);

    public SlidesConvertResult Convert(string filePath, PresentationConvertRequest request) =>
        _production.Convert(filePath, request);

    public SlidesRenderResult Render(string filePath, PresentationRenderRequest request) =>
        _production.Render(filePath, request);

    public SlidesCreateResult Create(NewPresentationRequest request) =>
        _production.Create(request);

    public SlidesExtractResult Extract(string filePath, PresentationExtractRequest request) =>
        _production.Extract(filePath, request);

    public SlidesEditResult ApplyOps(
        string filePath,
        SlidesOpsBatch batch,
        PresentationEditRequest request) =>
        _mutations.ApplyOps(filePath, batch, request);

    public SlidesSearchResult Search(string filePath, PresentationSearchRequest request) =>
        _reads.Search(filePath, request);

    public PreviewRenderOutcome RenderPreview(
        string filePath,
        PresentationPreviewRequest request,
        IPreviewArtifactSink artifacts) =>
        _production.RenderPreview(filePath, request, artifacts);

    public ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        _production.RenderView(filePath, request, artifacts);
}
