using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Views;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>Stable Words port facade that composes focused product services.</summary>
internal sealed class WordsEngine : IWordsEngine, IWordsReviewLayoutPort
{
    private readonly WordsReadService _reading;
    private readonly WordsInspectionService _inspection;
    private readonly WordsProductionService _production;
    private readonly WordsExtractionService _extraction;
    private readonly WordsMutationService _mutation;
    private readonly WordsReviewLayoutService _reviewLayout;

    public WordsEngine(
        OutputPipeline<Document> outputs,
        ResourceBudgetLedger resourceBudgets)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        var loader = new WordsDocumentLoader(resourceBudgets, outputs);
        _reading = new WordsReadService(outputs, loader);
        _reviewLayout = new WordsReviewLayoutService(outputs, loader);
        _inspection = new WordsInspectionService(outputs, loader);
        _production = new WordsProductionService(
            outputs,
            loader,
            resourceBudgets);
        _extraction = new WordsExtractionService(
            outputs,
            loader);
        _mutation = new WordsMutationService(
            outputs,
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
        Secret? password,
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
    public WordsCreateResult Create(NewDocumentRequest request) =>
        _production.Create(request);

    /// <inheritdoc />
    public string? DetectFormat(string filePath) => WordsDocumentLoader.DetectFormatId(filePath);

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
