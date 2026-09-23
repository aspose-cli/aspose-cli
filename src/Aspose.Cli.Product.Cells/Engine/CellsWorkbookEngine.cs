using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Product.Cells.Ports;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Stable Cells port facade. Product behavior is owned by cohesive internal
/// services; this type only composes and delegates them.
/// </summary>
internal sealed class CellsWorkbookEngine
    : IWorkbookEngine, IWorkbookReviewPort, IFontEnvironment
{
    private readonly CellsQueryService _queries;
    private readonly CellsOutputService _output;
    private readonly CellsMutationService _mutations;

    public CellsWorkbookEngine(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter fileWriter)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(fileWriter);

        var loader = new WorkbookLoadService(resourceBudgets);
        var saver = new WorkbookSaveService(fileWriter, loader);
        _queries = new CellsQueryService(licenseGate, resourceBudgets, loader);
        _output = new CellsOutputService(licenseGate, fileWriter, loader, saver, resourceBudgets);
        _mutations = new CellsMutationService(licenseGate, loader, saver, resourceBudgets,
            new CellsEditVerifier(loader, resourceBudgets));
    }

    public WorkbookInfoResult GetInfo(string filePath, InfoRequest request) =>
        _queries.GetInfo(filePath, request);

    public WorkbookReviewLayout InspectReviewLayout(
        string filePath,
        string? password) =>
        _queries.InspectReviewLayout(filePath, password);

    public WorkbookReadResult Read(string filePath, ReadRequest request) =>
        _queries.Read(filePath, request);

    public ConvertResult Convert(string filePath, ConvertRequest request) =>
        _output.Convert(filePath, request);

    public RenderResult Render(string filePath, RenderRequest request) =>
        _output.Render(filePath, request);

    public Aspose.Cli.Sdk.Views.ViewManifest RenderView(
        string filePath,
        Aspose.Cli.Sdk.Views.ViewRenderRequest request,
        Aspose.Cli.Sdk.Views.IViewArtifactSink artifacts) =>
        _output.RenderView(filePath, request, artifacts);

    public EditResult ApplyOps(string filePath, OpsBatch batch, EditRequest options) =>
        _mutations.ApplyOps(filePath, batch, options);

    public CreateResult CreateWorkbook(NewWorkbookRequest request) =>
        _mutations.CreateWorkbook(request);

    public DiffResult Diff(string leftPath, string rightPath, DiffRequest request) =>
        _queries.Diff(leftPath, rightPath, request);

    public SearchResult Search(string filePath, SearchRequest request) =>
        _queries.Search(filePath, request);

    public FontListResult ListFonts() => _queries.ListFonts();

    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) =>
        _queries.CheckFonts(filePath, request);

    public IDisposable UseFonts(Aspose.Cli.Sdk.Rendering.FontSearchProfile profile) =>
        FontOps.Use(profile);
}
