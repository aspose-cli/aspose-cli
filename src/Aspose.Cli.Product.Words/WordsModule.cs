using Aspose.Cli.Product.Words.Commands;
using Aspose.Cli.Product.Words.Output;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words;

/// <summary>Pure build-time definition and deferred activation for Words.</summary>
public sealed class WordsModule : IProductModule
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = 1700,
        Operations = ProductOperationDescriptor.ForCommand(
            WordsOps.Names,
            "edit",
            "v2/words/ops",
            atomicByDefault: true,
            supportsDryRun: true),
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
        Aspose.Cli.Sdk.Extensibility.Product.Define<IDocumentEngine>(Manifest)
            .Formats(WordsFormats.Definitions)
            .Diagnostics(WordsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .View(new WordsViewAdapter())
            .Output<DocumentInfoResult>(WordsRenderers.Render)
            .Output<DocumentReadResult>(WordsRenderers.Render)
            .Output<WordsConvertResult>(WordsRenderers.Render)
            .Output<WordsRenderResult>(WordsRenderers.Render)
            .Output<WordsCreateResult>(WordsRenderers.Render)
            .Output<WordsEditResult>(WordsRenderers.Render)
            .Output<WordsCompareResult>(WordsRenderers.Render)
            .Output<WordsSearchResult>(WordsRenderers.Render)
            .Output<WordsSplitResult>(WordsRenderers.Render)
            .Output<WordsExtractResult>(WordsRenderers.Render)
            .Commands(WordsCommands.Create)
            .Activator(Activate)
            .Build();

    private static ProductBinding<IDocumentEngine> Activate(
        ProductActivationContext context) =>
        ProductBinding.Create<IDocumentEngine>(
            context,
            Manifest.Id,
            resolution => new WordsLicenseGate(
                resolution,
                context.EnvironmentVariable),
            license => new WordsDocumentEngine(
                license,
                context.ResourceBudgets,
                context.SafeFileWriter),
            license => new WordsFontEnvironment(
                license,
                context.ResourceBudgets));

}
