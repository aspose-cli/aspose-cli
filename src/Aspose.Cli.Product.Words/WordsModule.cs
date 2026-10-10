using Aspose.Cli.Product.Words.Commands;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words;

/// <summary>Pure build-time definition and deferred activation for Words.</summary>
public sealed class WordsModule : IProductModule
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = ProductBuildMetadata.DisplayOrder,
        IsDefaultCandidate = ProductBuildMetadata.IsDefaultCandidate,
            ResourceBudgets =
            [
                ResourceBudgetCapabilities.Domain(WordsBudgetDomains.Pages, 10_000, 100_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(WordsBudgetDomains.Nodes, 1_000_000, 5_000_000, "items", "projection"),
            ],
            Engine = ProductEngineCapabilities.LicenseAware(
                "aspose",
                ProductBuildMetadata.EngineName,
                ProductBuildMetadata.SdkVersion),
            AvailableEngines = ["aspose"],
    };

    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<WordsSession>(Manifest)
            .Formats(WordsFormats.Definitions)
            .Diagnostics(WordsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .View(new WordsViewAdapter())
            .DetectFormat(WordsDocumentLoader.DetectFormatId)
            .Describe("Word-processing document automation with layout fidelity.", Help)
            .Command(InspectCommand.Create, WordsInspect.Run)
            .Group("query", "Read bounded document projections without mutating the source.", query => query
                .Command(ReadCommand.Create, WordsRead.Run)
                .Command(SearchCommand.Create, WordsSearch.Run))
            .Command(ConvertCommand.Create, WordsConvert.Run)
            .Command(RenderCommand.Create, WordsRender.Run)
            .Command(CreateCommand.Create, WordsCreate.Run)
            .Command(EditCommand.Create, WordsEdit.Run)
            .Command(CompareCommand.Create, WordsCompare.Run)
            .Command(SplitCommand.Create, WordsSplit.Run)
            .Command(ExtractCommand.Create, WordsExtract.Run)
            .Activator(WordsActivation.Activate)
            .Build();

    private static CommandHelp Help() => new(
        [
            "words inspect contract.docx --detail outline sections --preview",
            "words query blocks contract.docx --blocks 1-30 --scope full",
        ],
        [
            CommandHelpLink.Docs(Manifest, "editing", "the document block model and edit operations"),
            CommandHelpLink.Docs(Manifest, "verification", "read-back, semantic and visual verification"),
            CommandHelpLink.Schema<WordsOp>("the operation JSON schema"),
        ]);
}
