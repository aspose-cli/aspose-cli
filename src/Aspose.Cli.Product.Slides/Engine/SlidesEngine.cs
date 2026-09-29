using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// Stable Slides port facade that composes focused product services. Every call runs with standard
/// output muted, since the SDK writes to it while rendering (known issue SLIDES-FALLBACK-STDOUT).
/// </summary>
internal sealed class SlidesEngine : ISlidesEngine
{
    private readonly SlidesReadService _reads;
    private readonly SlidesInspectionService _inspection;
    private readonly SlidesProductionService _production;
    private readonly SlidesExtractionService _extraction;
    private readonly SlidesMutationService _mutations;

    public SlidesEngine(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(writer);

        var loader = new SlidesPresentationLoader(resourceBudgets);
        _reads = new SlidesReadService(licenseGate, loader);
        _inspection = new SlidesInspectionService(licenseGate, loader);
        _production = new SlidesProductionService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
        _extraction = new SlidesExtractionService(licenseGate, resourceBudgets, loader);
        _mutations = new SlidesMutationService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
    }

    public PresentationInfoResult GetInfo(string filePath, PresentationInfoRequest request) =>
        SlidesStandardOutput.Muted(() => _reads.GetInfo(filePath, request));

    public PresentationReadResult Read(string filePath, PresentationReadRequest request) =>
        SlidesStandardOutput.Muted(() => _reads.Read(filePath, request));

    public SlidesConvertResult Convert(string filePath, PresentationConvertRequest request) =>
        SlidesStandardOutput.Muted(() => _production.Convert(filePath, request));

    public SlidesRenderResult Render(string filePath, PresentationRenderRequest request) =>
        SlidesStandardOutput.Muted(() => _production.Render(filePath, request));

    public SlidesCreateResult Create(NewPresentationRequest request) =>
        SlidesStandardOutput.Muted(() => _production.Create(request));

    public SlidesExtractResult Extract(string filePath, PresentationExtractRequest request) =>
        SlidesStandardOutput.Muted(() => _extraction.Extract(filePath, request));

    public SlidesEditResult ApplyOps(
        string filePath,
        SlidesOpsBatch batch,
        PresentationEditRequest request) =>
        SlidesStandardOutput.Muted(() => _mutations.ApplyOps(filePath, batch, request));

    public SlidesSearchResult Search(string filePath, PresentationSearchRequest request) =>
        SlidesStandardOutput.Muted(() => _inspection.Search(filePath, request));

    public ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        SlidesStandardOutput.Muted(() => _production.RenderView(filePath, request, artifacts));
}
