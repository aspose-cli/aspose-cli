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
                ResourceBudget.Domain(WordsBudgetDomains.Nodes, 1_000_000, 5_000_000, "items", "post-load"),
            ],
            Engine = new ProductEngine
            {
                Sdk = ProductBuildMetadata.EngineName,
                SdkVersion = ProductBuildMetadata.SdkVersion,
            },
    };

    /// <summary>Loads the Words object table, which names engine types: the commands call it only when they run.</summary>
    internal static Aspose.Cli.Sdk.Tables.DocumentTable Table() => Engine.Mapping.WordsTable.Definition;

    /// <summary>Returns the pure, immutable Words product definition.</summary>
    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<WordsSession>(Manifest)
            .Formats(WordsFormats.Definitions)
            .Diagnostics(WordsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .DetectFormat(WordsDocumentLoader.DetectFormatId)
            .Describe("Read, edit, convert and render documents through the Aspose.Words object model.", Help)
            .Command(ReadCommand.Create, WordsRead.Run)
            .Command(SearchCommand.Create, WordsSearch.Run)
            .Command(CreateCommand.Create, WordsCreate.Run)
            .Command(EditCommand.Create, WordsEdit.Run)
            .Command(ConvertCommand.Create, WordsConvert.Run)
            .Command(RenderCommand.Create, WordsRender.Run)
            .Command(CompareCommand.Create, WordsCompare.Run)
            .Command(ExtractCommand.Create, WordsExtract.Run)
            .Activator(WordsActivation.Activate)
            .Build();

    private static CommandHelp Help() => new(
        [
            "words read contract.docx",
            "words read contract.docx --at section/0/body/range",
            "words edit contract.docx --ops ops.json --out revised.docx",
        ],
        [
            new("aspose-cli schema v2/words/ops/directory", "the object table: kinds, addresses, rows, adds and actions"),
        ]);
}
