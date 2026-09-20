using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Stable Words port facade that composes focused product services.</summary>
internal sealed class WordsDocumentEngine : IDocumentEngine, IWordsReviewLayoutPort
{
    private readonly WordsReadService _reading;
    private readonly WordsInspectionService _inspection;
    private readonly WordsProductionService _production;
    private readonly WordsExtractionService _extraction;
    private readonly WordsMutationService _mutation;
    private readonly WordsReviewLayoutService _reviewLayout;

    public WordsDocumentEngine(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        SafeFileWriter writer)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(writer);
        var loader = new WordsDocumentLoader(resourceBudgets);
        _reading = new WordsReadService(licenseGate, loader);
        _reviewLayout = new WordsReviewLayoutService(licenseGate, loader);
        _inspection = new WordsInspectionService(licenseGate, writer, loader);
        _production = new WordsProductionService(
            licenseGate,
            writer,
            loader,
            resourceBudgets.Inputs);
        _extraction = new WordsExtractionService(
            licenseGate,
            writer,
            loader,
            resourceBudgets);
        _mutation = new WordsMutationService(
            licenseGate,
            writer,
            loader,
            resourceBudgets.Inputs);
    }

    /// <inheritdoc />
    public DocumentInfoResult GetInfo(string filePath, DocumentInfoRequest request) =>
        _reading.GetInfo(filePath, request);

    /// <inheritdoc />
    public DocumentReadResult Read(string filePath, DocumentReadRequest request) =>
        _reading.Read(filePath, request);

    WordsReviewLayout IWordsReviewLayoutPort.InspectReviewLayout(
        string filePath,
        string? password,
        int maxPages) => _reviewLayout.Inspect(filePath, password, maxPages);

    /// <inheritdoc />
    public WordsConvertResult Convert(string filePath, WordsConvertRequest request) =>
        _production.Convert(filePath, request);

    /// <inheritdoc />
    public WordsRenderResult Render(string filePath, WordsRenderRequest request) =>
        _production.Render(filePath, request);

    /// <inheritdoc />
    public ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        _production.RenderView(filePath, request, artifacts);

    /// <inheritdoc />
    public WordsCreateResult CreateDocument(NewDocumentRequest request) =>
        _production.CreateDocument(request);

    /// <inheritdoc />
    public WordsEditResult ApplyOps(string filePath, WordsOpsBatch batch, WordsEditRequest request) =>
        _mutation.ApplyOps(filePath, batch, request);

    /// <inheritdoc />
    public WordsCompareResult Compare(string leftPath, string rightPath, WordsCompareRequest request) =>
        _inspection.Compare(leftPath, rightPath, request);

    /// <inheritdoc />
    public WordsSearchResult Search(string filePath, WordsSearchRequest request) =>
        _inspection.Search(filePath, request);

    /// <inheritdoc />
    public WordsSplitResult Split(string filePath, WordsSplitRequest request) =>
        _extraction.Split(filePath, request);

    /// <inheritdoc />
    public WordsExtractResult Extract(string filePath, WordsExtractRequest request) =>
        _extraction.Extract(filePath, request);
}
