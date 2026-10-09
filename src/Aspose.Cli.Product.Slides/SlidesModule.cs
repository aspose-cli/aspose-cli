using Aspose.Cli.Product.Slides.Commands;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Slides;

/// <summary>Pure build-time definition and deferred activation for Slides.</summary>
public sealed class SlidesModule : IProductModule
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = ProductBuildMetadata.DisplayOrder,
        IsDefaultCandidate = ProductBuildMetadata.IsDefaultCandidate,
        Operations = [SlidesOp.Catalog.Describe("edit")],
            ResourceBudgets =
            [
                ResourceBudgetCapabilities.Domain(SlidesBudgetDomains.Slides, 10_000, 50_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(SlidesBudgetDomains.Shapes, 1_000_000, 5_000_000, "items", "projection"),
            ],
            Engine = ProductEngineCapabilities.LicenseAware(
                "aspose",
                ProductBuildMetadata.EngineName,
                ProductBuildMetadata.SdkVersion),
            AvailableEngines = ["aspose"],
    };

    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product
            .Define<SlidesSession>(Manifest)
            .Formats(SlidesFormats.Definitions)
            .Diagnostics(SlidesDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .View(new SlidesViewAdapter())
            // Aspose.Slides writes to standard output while it renders (known issue SLIDES-FALLBACK-STDOUT).
            .Guard(static (_, run) => SlidesStandardOutput.Muted(run))
            .Describe("Presentation automation with slide, layout and notes semantics.", Help)
            .Command(InfoCommand.Create, SlidesInfo.Run)
            .Group("query", "Read bounded presentation projections without mutating the source.", static query => query
                .Command(ReadCommand.Create, SlidesRead.Run)
                .Command(SearchCommand.Create, SlidesSearch.Run))
            .Command(ConvertCommand.Create, SlidesExport.Convert)
            .Command(RenderCommand.Create, SlidesExport.Render)
            .Command(NewCommand.Create, SlidesCreate.Run)
            .Command(EditCommand.Create, SlidesEdit.Run)
            .Command(ExtractCommand.Create, SlidesExtract.Run)
            .Activator(static context => SlidesActivation.Activate(context, Manifest.Id))
            .Build();

    private static CommandHelp Help() => new(
        [
            "slides create deck.pptx --from-markdown outline.md --template brand.pptx",
            "slides inspect deck.pptx --preview --detail masters layouts fonts notes",
            "slides query slides deck.pptx --slides 1-5 --scope full --notes --output json",
            "slides edit deck.pptx --ops deck-ops.json --out revised.pptx",
            "review revised.pptx --out revised.review",
        ],
        [
            CommandHelpLink.Docs(Manifest, "editing", "atomic presentation operations"),
            CommandHelpLink.Docs(Manifest, "verification", "slide read-back and visual review"),
            CommandHelpLink.Schema(Manifest, "the operation JSON schema"),
        ]);
}
