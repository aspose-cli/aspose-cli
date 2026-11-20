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
            ResourceBudgets =
            [
                ResourceBudget.Domain(SlidesBudgetDomains.Slides, 10_000, 50_000, "items", "post-load"),
                ResourceBudget.Domain(SlidesBudgetDomains.Shapes, 1_000_000, 5_000_000, "items", "projection"),
            ],
            Engine = new ProductEngine
            {
                Sdk = ProductBuildMetadata.EngineName,
                SdkVersion = ProductBuildMetadata.SdkVersion,
            },
    };

    /// <summary>Loads the Slides object table, which names engine types: the commands call it only when they run.</summary>
    internal static Aspose.Cli.Sdk.Tables.DocumentTable Table() => Engine.Mapping.SlidesTable.Definition;

    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product
            .Define<SlidesSession>(Manifest)
            .Formats(SlidesFormats.Definitions)
            .Diagnostics(SlidesDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            // Aspose.Slides writes to standard output while it renders (known issue SLIDES-FALLBACK-STDOUT).
            .Guard(static (_, run) => SlidesStandardOutput.Muted(run))
            .Describe("Read, edit, convert and render presentations through the Aspose.Slides object model.", Help)
            .Command(ReadCommand.Create, SlidesRead.Run)
            .Command(SearchCommand.Create, SlidesSearch.Run)
            .Command(ConvertCommand.Create, SlidesExport.Convert)
            .Command(RenderCommand.Create, SlidesExport.Render)
            .Command(CreateCommand.Create, SlidesCreate.Run)
            .Command(EditCommand.Create, SlidesEdit.Run)
            .Command(ExtractCommand.Create, SlidesExtract.Run)
            .Activator(SlidesActivation.Activate)
            .Build();

    private static CommandHelp Help() => new(
        [
            "slides create deck.pptx --template brand.potx",
            "slides read deck.pptx --kind slide --kind shape",
            "slides read deck.pptx --at slide/256",
            "slides edit deck.pptx --ops ops.json --out revised.pptx",
        ],
        [
            new("aspose-cli schema v2/slides/ops/directory", "the object table: kinds, addresses, rows, adds and actions"),
        ]);
}
