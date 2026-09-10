using Aspose.Cli.Product.Slides.Commands;
using Aspose.Cli.Product.Slides.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Slides;

/// <summary>Pure build-time definition and deferred activation for Slides.</summary>
public sealed class SlidesModule : IProductModule
{
    internal static readonly IReadOnlyList<FormatDescriptor> Formats =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Routed("ppt", FormatUse.Input | FormatUse.Convert, 0, 1, null, ".ppt"),
        FormatDescriptor.Routed("pptx", FormatUse.Input | FormatUse.Convert, 1, 0, null, ".pptx"),
        FormatDescriptor.Routed("pptm", FormatUse.Input | FormatUse.Convert, 2, 2, null, ".pptm"),
        FormatDescriptor.Routed("pps", FormatUse.Input, 3, null, null, ".pps"),
        FormatDescriptor.Routed("ppsx", FormatUse.Input, 4, null, null, ".ppsx"),
        FormatDescriptor.Routed("ppsm", FormatUse.Input, 5, null, null, ".ppsm"),
        FormatDescriptor.Routed("pot", FormatUse.Input, 6, null, null, ".pot"),
        FormatDescriptor.Routed("potx", FormatUse.Input, 7, null, null, ".potx"),
        FormatDescriptor.Routed("potm", FormatUse.Input, 8, null, null, ".potm"),
        FormatDescriptor.Routed("odp", FormatUse.Input | FormatUse.Convert, 9, 3, null, ".odp"),
        FormatDescriptor.Routed("otp", FormatUse.Input, 10, null, null, ".otp"),
        FormatDescriptor.Routed("fodp", FormatUse.Input, 11, null, null, ".fodp"),
        FormatDescriptor.Routed("pdf", FormatUse.Convert, null, 4, null, ".pdf"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, null, 5, null, ".xps"),
        FormatDescriptor.Routed("html", FormatUse.Convert, null, 6, null, ".html"),
        FormatDescriptor.Routed("html5", FormatUse.Convert, null, 7, null, ".html"),
        FormatDescriptor.Routed("png", FormatUse.Convert | FormatUse.Render, null, 8, 0, ".png"),
        FormatDescriptor.Routed("jpeg", FormatUse.Convert | FormatUse.Render, null, 9, 1, ".jpg", ".jpeg"),
        FormatDescriptor.Routed("tiff", FormatUse.Convert, null, 10, null, ".tiff"),
        FormatDescriptor.Routed("gif", FormatUse.Convert, null, 11, null, ".gif"),
        FormatDescriptor.Routed("svg", FormatUse.Convert | FormatUse.Render, null, 12, 2, ".svg"),
        FormatDescriptor.Routed("md", FormatUse.Convert, null, 13, null, ".md"),
    ], SlidesFormatRecognition.Rules);

    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = 1200,
        Operations = ProductOperationDescriptor.ForCommand(
            SlidesOps.Names,
            "edit",
            "v2/slides/ops",
            atomicByDefault: true,
            supportsDryRun: true),
            ResourceBudgets =
            [
                ResourceBudgetCapabilities.Domain(SlidesBudgetDomains.Slides, 10_000, 50_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(SlidesBudgetDomains.Shapes, 1_000_000, 5_000_000, "items", "projection"),
                ResourceBudgetCapabilities.Domain(SlidesBudgetDomains.Pixels, 64L * 1024 * 1024, 1024L * 1024 * 1024, "pixels", "pre-render", "--max-pixels"),
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
            .Formats(Formats)
            .Diagnostics(SlidesDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .Preview(new SlidesPreviewAdapter())
            .Review(new SlidesReviewAdapter())
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
