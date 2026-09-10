using Aspose.Cli.Product.Words.Commands;
using Aspose.Cli.Product.Words.Output;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words;

/// <summary>Pure build-time definition and deferred activation for Words.</summary>
public sealed class WordsModule : IProductModule
{
    internal static readonly IReadOnlyList<FormatDescriptor> Formats =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".doc"),
        FormatDescriptor.Declare("dot", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".dot"),
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".docx"),
        FormatDescriptor.Declare("docm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".docm"),
        FormatDescriptor.Declare("dotx", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".dotx"),
        FormatDescriptor.Declare("dotm", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".dotm"),
        FormatDescriptor.Declare("flatopc", FormatUse.Input | FormatUse.Convert, 6, 6, null, false, ".xml"),
        FormatDescriptor.Declare("rtf", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".rtf"),
        FormatDescriptor.Declare("wordml", FormatUse.Input | FormatUse.Convert, 8, 8, null, false, ".xml"),
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 9, 15, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 10, 17, null, false, ".mhtml"),
        FormatDescriptor.Declare("odt", FormatUse.Input | FormatUse.Convert, 11, 21, null, true, ".odt"),
        FormatDescriptor.Declare("ott", FormatUse.Input | FormatUse.Convert, 12, 22, null, true, ".ott"),
        FormatDescriptor.Declare("txt", FormatUse.Input | FormatUse.Convert, 13, 23, null, true, ".txt"),
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, 14, 24, null, true, ".md"),
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, 15, 9, null, false, ".pdf"),
        FormatDescriptor.Declare("epub", FormatUse.Input | FormatUse.Convert, 16, 18, null, true, ".epub"),
        FormatDescriptor.Declare("mobi", FormatUse.Input | FormatUse.Convert, 17, 19, null, true, ".mobi"),
        FormatDescriptor.Declare("azw3", FormatUse.Input | FormatUse.Convert, 18, 20, null, true, ".azw3"),
        FormatDescriptor.Declare("chm", FormatUse.Input, 19, null, null, true, ".chm"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, null, 10, null, false, ".xps"),
        FormatDescriptor.Declare("openxps", FormatUse.Convert, null, 11, null, false, ".oxps"),
        FormatDescriptor.Declare("ps", FormatUse.Convert, null, 12, null, false, ".ps"),
        FormatDescriptor.Declare("pcl", FormatUse.Convert, null, 13, null, false, ".pcl"),
        FormatDescriptor.Declare("html-fixed", FormatUse.Convert, null, 16, null, false, ".html"),
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg"),
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], WordsFormatRecognition.Rules);

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
            .Formats(Formats)
            .Diagnostics(WordsDiagnostics.All)
            .Provides(
                StandardProductCapabilities.DocumentToPdf,
                static binding => new WordsDocumentCapabilities(binding))
            .Provides(
                StandardProductCapabilities.DocumentPageRenderer,
                static binding => new WordsDocumentCapabilities(binding))
            .Json(ProductJsonContext.Definition)
            .Preview(new WordsPreviewAdapter())
            .Review(new WordsReviewAdapter())
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
