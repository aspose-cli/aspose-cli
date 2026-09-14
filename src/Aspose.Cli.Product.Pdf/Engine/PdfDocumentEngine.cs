using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Ports;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Stable PDF port facade. Aspose.PDF operations remain behind focused,
/// product-owned services.
/// </summary>
internal sealed class PdfDocumentEngine : IPdfEngine, IPdfReviewLayoutPort
{
    private readonly PdfReadService _reading;
    private readonly PdfProductionService _production;
    private readonly PdfExtractionService _extraction;
    private readonly PdfMutationService _edits;
    private readonly PdfFormService _forms;
    private readonly PdfInspectionService _inspection;
    private readonly PdfSigningService _signing;
    private readonly PdfReviewLayoutService _reviewLayout;

    public PdfDocumentEngine(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(writer);

        var loader = new Engine.Mapping.PdfDocumentLoader(resourceBudgets);
        _reading = new PdfReadService(licenseGate, loader);
        _production = new PdfProductionService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
        _extraction = new PdfExtractionService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
        _edits = new PdfMutationService(licenseGate, writer, loader);
        _forms = new PdfFormService(licenseGate, writer, loader);
        _inspection = new PdfInspectionService(licenseGate, loader);
        _signing = new PdfSigningService(
            licenseGate,
            resourceBudgets,
            writer,
            loader);
        _reviewLayout = new PdfReviewLayoutService(licenseGate, loader);
    }

    public PdfInfoResult GetInfo(string filePath, PdfInfoRequest request) =>
        _reading.GetInfo(filePath, request);

    public PdfReadResult Read(string filePath, PdfReadRequest request) =>
        _reading.Read(filePath, request);

    public PdfConvertResult Convert(string filePath, PdfConvertRequest request) =>
        _production.Convert(filePath, request);

    public PdfRenderResult Render(string filePath, PdfRenderRequest request) =>
        _production.Render(filePath, request);

    public PdfWriteResult Create(NewPdfRequest request) =>
        _production.Create(request);

    public PdfWriteResult Merge(PdfMergeRequest request) =>
        _production.Merge(request);

    public PdfSplitResult Split(string filePath, PdfSplitRequest request) =>
        _extraction.Split(filePath, request);

    public PdfExtractResult Extract(string filePath, PdfExtractRequest request) =>
        _extraction.Extract(filePath, request);

    public PdfEditResult ApplyOps(string filePath, PdfOpsBatch batch, PdfEditRequest request) =>
        _edits.ApplyOps(filePath, batch, request);

    public PdfFormResult ReadForm(string filePath, PdfFormReadRequest request) =>
        _forms.ReadForm(filePath, request);

    public PdfEditResult FillForm(string filePath, PdfFormFillRequest request) =>
        _forms.FillForm(filePath, request);

    public PdfFormExportResult ExportForm(string filePath, PdfFormExportRequest request) =>
        _forms.ExportForm(filePath, request);

    public PdfSearchResult Search(string filePath, PdfSearchRequest request) =>
        _inspection.Search(filePath, request);

    public PdfValidateResult Validate(string filePath, PdfValidateRequest request) =>
        _inspection.Validate(filePath, request);

    public PreviewRenderOutcome RenderPreview(
        string filePath,
        PdfPreviewRequest request,
        IPreviewArtifactSink artifacts) =>
        _production.RenderPreview(filePath, request, artifacts);

    public PdfSignResult Sign(string filePath, PdfSignRequest request) =>
        _signing.Sign(filePath, request);

    PdfReviewLayout IPdfReviewLayoutPort.InspectReviewLayout(
        string filePath,
        string? password,
        int maxPages) => _reviewLayout.Inspect(filePath, password, maxPages);
}
