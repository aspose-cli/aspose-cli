using Aspose.Cli.Product.Slides.Commands;
using Aspose.Cli.Product.Slides.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides;

/// <summary>Pure build-time definition and deferred activation for Slides.</summary>
public sealed class SlidesModule : IProductModule
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = 1200,
        Operations = [SlidesOps.Catalog.Describe("edit")],
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
            .Define<IPresentationEngine>(Manifest)
            .Formats(SlidesFormats.Definitions)
            .Diagnostics(SlidesDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .View(new SlidesViewAdapter())
            .Output<PresentationInfoResult>(SlidesRenderers.Render)
            .Output<PresentationReadResult>(SlidesRenderers.Render)
            .Output<SlidesConvertResult>(SlidesRenderers.Render)
            .Output<SlidesRenderResult>(SlidesRenderers.Render)
            .Output<SlidesCreateResult>(SlidesRenderers.Render)
            .Output<SlidesExtractResult>(SlidesRenderers.Render)
            .Output<SlidesEditResult>(SlidesRenderers.Render)
            .Output<SlidesSearchResult>(SlidesRenderers.Render)
            .Commands(SlidesCommands.Create)
            .Activator(Activate)
            .Build();

    private static ProductBinding<IPresentationEngine> Activate(
        ProductActivationContext context) =>
        ProductBinding.Create<IPresentationEngine>(
            context,
            Manifest.Id,
            resolution => new SlidesLicenseGate(
                resolution,
                context.EnvironmentVariable),
            license => new SlidesPresentationEngine(
                license,
                context.ResourceBudgets,
                context.SafeFileWriter),
            license => new SlidesFontEnvironment(
                license,
                context.ResourceBudgets));

}
