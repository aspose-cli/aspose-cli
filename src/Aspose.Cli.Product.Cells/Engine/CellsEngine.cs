using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Editing;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Product.Cells.Ports;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Stable Cells port facade. Product behavior is owned by cohesive internal
/// services; this type only composes and delegates them.
/// </summary>
internal sealed class CellsEngine : ICellsEngine, ICellsReviewLayoutPort
{
    private readonly CellsReadService _reading;
    private readonly CellsInspectionService _inspection;
    private readonly CellsProductionService _production;
    private readonly CellsMutationService _mutations;
    private readonly CellsReviewLayoutService _reviewLayout;

    public CellsEngine(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter fileWriter)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(fileWriter);

        var loader = new CellsWorkbookLoader(resourceBudgets);
        var saver = new CellsSavePipeline(fileWriter, loader);
        _reading = new CellsReadService(licenseGate, resourceBudgets, loader);
        _inspection = new CellsInspectionService(licenseGate, resourceBudgets, loader);
        _production = new CellsProductionService(licenseGate, fileWriter, loader, saver, resourceBudgets);
        _mutations = new CellsMutationService(licenseGate, loader, saver, resourceBudgets,
            new CellsEditVerifier(loader, resourceBudgets));
        _reviewLayout = new CellsReviewLayoutService(licenseGate, loader);
    }

    public WorkbookInfoResult GetInfo(string filePath, InfoRequest request) =>
        _reading.GetInfo(filePath, request);

    public WorkbookReadResult Read(string filePath, ReadRequest request) =>
        _reading.Read(filePath, request);

    public ConvertResult Convert(string filePath, ConvertRequest request) =>
        _production.Convert(filePath, request);

    public RenderResult Render(string filePath, RenderRequest request) =>
        _production.Render(filePath, request);

    public ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        _production.RenderView(filePath, request, artifacts);

    public EditResult ApplyOps(string filePath, CellsOpsBatch batch, EditRequest options) =>
        _mutations.ApplyOps(filePath, batch, options);

    public CreateResult Create(NewWorkbookRequest request) =>
        _production.Create(request);

    public DiffResult Diff(string leftPath, string rightPath, DiffRequest request) =>
        _inspection.Diff(leftPath, rightPath, request);

    public SearchResult Search(string filePath, SearchRequest request) =>
        _inspection.Search(filePath, request);

    public CellsReviewLayout Inspect(
        string filePath,
        string? password) =>
        _reviewLayout.Inspect(filePath, password);
}
