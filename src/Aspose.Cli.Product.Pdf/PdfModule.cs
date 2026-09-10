using Aspose.Cli.Product.Pdf.Commands;
using Aspose.Cli.Product.Pdf.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Pure build-time definition and deferred activation for PDF.</summary>
public sealed class PdfModule : IProductModule
{
    internal static readonly IReadOnlyList<FormatDescriptor> Formats =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Routed("pdf", FormatUse.Input, 0, null, null, ".pdf"),
        FormatDescriptor.Routed("docx", FormatUse.Convert, null, 0, null, ".docx"),
        FormatDescriptor.Routed("xlsx", FormatUse.Convert, null, 1, null, ".xlsx"),
        FormatDescriptor.Routed("pptx", FormatUse.Convert, null, 2, null, ".pptx"),
        FormatDescriptor.Routed("html", FormatUse.Convert, null, 3, null, ".html"),
        FormatDescriptor.Routed("epub", FormatUse.Convert, null, 4, null, ".epub"),
        FormatDescriptor.Routed("txt", FormatUse.Convert, null, 5, null, ".txt"),
        FormatDescriptor.Routed("md", FormatUse.Convert, null, 6, null, ".md"),
        FormatDescriptor.Routed("svg", FormatUse.Convert | FormatUse.Render, null, 7, 2, ".svg"),
        FormatDescriptor.Routed("xps", FormatUse.Convert, null, 8, null, ".xps"),
        FormatDescriptor.Routed("pdfa-1b", FormatUse.Convert, null, 9, null, ".pdf"),
        FormatDescriptor.Routed("pdfa-2b", FormatUse.Convert, null, 10, null, ".pdf"),
        FormatDescriptor.Routed("pdfa-3b", FormatUse.Convert, null, 11, null, ".pdf"),
        FormatDescriptor.Routed("png", FormatUse.Convert | FormatUse.Render, null, 12, 0, ".png"),
        FormatDescriptor.Routed("jpeg", FormatUse.Convert | FormatUse.Render, null, 13, 1, ".jpg", ".jpeg"),
        FormatDescriptor.Routed("tiff", FormatUse.Convert, null, 14, null, ".tiff"),
    ], PdfFormatRecognition.Rules);

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
            .Formats(Formats)
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
