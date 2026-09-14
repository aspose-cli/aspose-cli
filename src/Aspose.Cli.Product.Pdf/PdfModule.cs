using Aspose.Cli.Product.Pdf.Commands;
using Aspose.Cli.Product.Pdf.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Pure build-time definition and deferred activation for PDF.</summary>
public sealed class PdfModule : IProductModule
{
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = 900,
        Operations = ProductOperationDescriptor.ForCommand(
            PdfOps.Names,
            "edit",
            "v2/pdf/ops",
            atomicByDefault: true,
            supportsDryRun: true),
            ResourceBudgets =
            [
                ResourceBudgetCapabilities.Domain(PdfBudgetDomains.Pages, 10_000, 100_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(PdfBudgetDomains.Objects, 1_000_000, 5_000_000, "items", "projection"),
            ],
            Engine = ProductEngineCapabilities.LicenseAware(
                "aspose",
                ProductBuildMetadata.EngineName,
                ProductBuildMetadata.SdkVersion),
            AvailableEngines = ["aspose"],
    };

    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<IPdfEngine>(Manifest)
            .Formats(PdfFormats.Definitions)
            .Diagnostics(PdfDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .Preview(new PdfPreviewAdapter())
            .Review(new PdfReviewAdapter())
            .Output<PdfInfoResult>(PdfRenderers.Render)
            .Output<PdfReadResult>(PdfRenderers.Render)
            .Output<PdfConvertResult>(PdfRenderers.Render)
            .Output<PdfRenderResult>(PdfRenderers.Render)
            .Output<PdfWriteResult>(PdfRenderers.Render)
            .Output<PdfSplitResult>(PdfRenderers.Render)
            .Output<PdfExtractResult>(PdfRenderers.Render)
            .Output<PdfEditResult>(PdfRenderers.Render)
            .Output<PdfFormResult>(PdfRenderers.Render)
            .Output<PdfFormExportResult>(PdfRenderers.Render)
            .Output<PdfSearchResult>(PdfRenderers.Render)
            .Output<PdfValidateResult>(PdfRenderers.Render)
            .Output<PdfSignResult>(PdfRenderers.Render)
            .Commands(PdfCommands.Create)
            .Activator(Activate)
            .Build();

    private static ProductBinding<IPdfEngine> Activate(
        ProductActivationContext context) =>
        ProductBinding.Create<IPdfEngine>(
            context,
            Manifest.Id,
            resolution => new PdfLicenseGate(
                resolution,
                context.EnvironmentVariable),
            license => new PdfDocumentEngine(
                license,
                context.ResourceBudgets,
                context.SafeFileWriter),
            license => new PdfFontEnvironment(
                license,
                context.ResourceBudgets));

}
