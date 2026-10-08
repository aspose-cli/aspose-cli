using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// PDF port facade. It composes the role services and delegates each port method to
/// the one that owns it; Aspose.PDF operations stay inside those services. Every call runs
/// through <see cref="PdfEvaluation.Run"/>, so reading past the evaluation limit is refused.
/// </summary>
internal sealed class PdfEngine : IPdfEngine, IPdfReviewLayoutPort
{
    private readonly PdfReadService _reading;
    private readonly PdfProductionService _production;
    private readonly PdfExtractionService _extraction;
    private readonly PdfMutationService _mutations;
    private readonly PdfFormService _forms;
    private readonly PdfInspectionService _inspection;
    private readonly PdfSigningService _signing;
    private readonly PdfReviewLayoutService _reviewLayout;
    private readonly OutputPipeline<Document> _outputs;

    public PdfEngine(
        OutputPipeline<Document> outputs,
        ResourceBudgetLedger resourceBudgets)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(resourceBudgets);

        _outputs = outputs;
        var loader = new PdfDocumentLoader(resourceBudgets);
        _reading = new PdfReadService(outputs, loader);
        _production = new PdfProductionService(
            outputs,
            resourceBudgets,
            loader);
        _extraction = new PdfExtractionService(
            outputs,
            resourceBudgets,
            loader);
        _mutations = new PdfMutationService(outputs, loader, resourceBudgets.Inputs, resourceBudgets.Deadline);
        _forms = new PdfFormService(outputs, loader);
        _inspection = new PdfInspectionService(outputs, loader);
        _signing = new PdfSigningService(
            outputs,
            resourceBudgets,
            loader);
        _reviewLayout = new PdfReviewLayoutService(outputs, loader);
    }

    public PdfInfoResult GetInfo(string filePath, PdfInfoRequest request) =>
        PdfEvaluation.Run(_outputs, () => _reading.GetInfo(filePath, request));

    public PdfReadResult Read(string filePath, PdfReadRequest request) =>
        PdfEvaluation.Run(_outputs, () => _reading.Read(filePath, request));

    public PdfConvertResult Convert(string filePath, PdfConvertRequest request) =>
        PdfEvaluation.Run(_outputs, () => _production.Convert(filePath, request));

    public PdfRenderResult Render(string filePath, PdfRenderRequest request) =>
        PdfEvaluation.Run(_outputs, () => _production.Render(filePath, request));

    public PdfWriteResult Create(NewPdfRequest request) =>
        PdfEvaluation.Run(_outputs, () => _production.Create(request));

    public PdfWriteResult Merge(PdfMergeRequest request) =>
        PdfEvaluation.Run(
            _outputs,
            () => _production.Merge(request),
            cause: "merging reads every page of the inputs, and together they have more",
            remedy: $"merge inputs that have at most {PdfEvaluation.VisiblePages} pages together");

    public PdfSplitResult Split(string filePath, PdfSplitRequest request) =>
        PdfEvaluation.Run(_outputs, () => _extraction.Split(filePath, request));

    public PdfExtractResult Extract(string filePath, PdfExtractRequest request) =>
        PdfEvaluation.Run(_outputs, () => _extraction.Extract(filePath, request));

    public PdfEditResult ApplyOps(string filePath, PdfOpsBatch batch, PdfEditRequest request) =>
        PdfEvaluation.Run(_outputs, () => _mutations.ApplyOps(filePath, batch, request));

    public PdfFormResult ReadForm(string filePath, PdfFormReadRequest request) =>
        PdfEvaluation.Run(_outputs, () => _forms.ReadForm(filePath, request));

    public PdfFormExportResult ExportForm(string filePath, PdfFormExportRequest request) =>
        PdfEvaluation.Run(_outputs, () => _forms.ExportForm(filePath, request));

    public PdfSearchResult Search(string filePath, PdfSearchRequest request) =>
        PdfEvaluation.Run(_outputs, () => _inspection.Search(filePath, request));

    public PdfValidateResult Validate(string filePath, PdfValidateRequest request) =>
        PdfEvaluation.Run(_outputs, () => _inspection.Validate(filePath, request));

    public ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        PdfEvaluation.Run(_outputs, () => _production.RenderView(filePath, request, artifacts));

    public PdfSignResult Sign(string filePath, PdfSignRequest request) =>
        PdfEvaluation.Run(_outputs, () => _signing.Sign(filePath, request));

    PdfReviewLayout IPdfReviewLayoutPort.InspectReviewLayout(
        string filePath,
        Secret? password,
        int maxPages) =>
        PdfEvaluation.Run(_outputs, () => _reviewLayout.Inspect(filePath, password, maxPages));
}
