using Aspose.Cli.Product.Cells.Commands;
using Aspose.Cli.Product.Cells.Output;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells;

/// <summary>Pure build-time definition and deferred activation for Cells.</summary>
public sealed class CellsModule : IProductModule
{
    internal static readonly IReadOnlyList<FormatDescriptor> Formats =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Declare("xlsx", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".xlsx", ".xltx"),
        FormatDescriptor.Declare("xlsm", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".xlsm", ".xltm"),
        FormatDescriptor.Declare("xlsb", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".xlsb"),
        FormatDescriptor.Declare("xls", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".xls"),
        FormatDescriptor.Declare("ods", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".ods"),
        FormatDescriptor.Declare("csv", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".csv"),
        FormatDescriptor.Declare("tsv", FormatUse.Input | FormatUse.Convert, 6, 6, null, true, ".tsv"),
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 7, 7, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 8, 8, null, false, ".mhtml"),
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, 9, 9, null, false, ".pdf"),
        FormatDescriptor.Declare("xps", FormatUse.Input | FormatUse.Convert, 10, 10, null, false, ".xps"),
        FormatDescriptor.Declare("json", FormatUse.Input | FormatUse.Convert, 11, 11, null, false, ".json"),
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, 12, 12, null, false, ".md")
            with { Aliases = ["markdown"] },
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], CellsFormatRecognition.Rules);

    /// <summary>Stable Cells manifest reused by the host registry.</summary>
    public static ProductManifest Manifest { get; } = new()
    {
        Id = ProductBuildMetadata.ProductId,
        DisplayName = ProductBuildMetadata.DisplayName,
        DisplayOrder = 100,
        IsDefaultCandidate = true,
        Operations = ProductOperationDescriptor.ForCommand(
            OpNames.All,
            "edit",
            "v2/cells/ops",
            atomicByDefault: true,
            supportsDryRun: true),
            ResourceBudgets =
            [
                ResourceBudgetCapabilities.Domain(CellsBudgetDomains.Sheets, 1_000, 10_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(CellsBudgetDomains.Cells, 1_000_000, 10_000_000, "items", "projection"),
                ResourceBudgetCapabilities.Domain(CellsBudgetDomains.Objects, 100_000, 1_000_000, "items", "post-load"),
            ],
            Engine = ProductEngineCapabilities.LicenseAware(
                "aspose",
                ProductBuildMetadata.EngineName,
                ProductBuildMetadata.SdkVersion),
            AvailableEngines = ["aspose"],
    };

    /// <summary>Returns the pure, immutable Cells product definition.</summary>
    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<IWorkbookEngine>(Manifest)
            .Formats(Formats)
            .Diagnostics(CellsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .Preview(new CellsPreviewAdapter())
            .Review(new CellsReviewAdapter())
            .Output<WorkbookInfoResult>(CellsRenderers.Render)
            .Output<WorkbookReadResult>(CellsRenderers.Render)
            .Output<ConvertResult>(CellsRenderers.Render)
            .Output<RenderResult>(CellsRenderers.Render)
            .Output<EditResult>(CellsRenderers.Render)
            .Output<CreateResult>(CellsRenderers.Render)
            .Output<DiffResult>(CellsRenderers.Render)
            .Output<SearchResult>(CellsRenderers.Render)
            .Commands(CellsCommands.Create)
            .Activator(Activate)
            .Build();

    private static ProductBinding<IWorkbookEngine> Activate(
        ProductActivationContext context) =>
        ProductBinding.Create<IWorkbookEngine, CellsWorkbookEngine>(
            context,
            Manifest.Id,
            resolution => new CellsLicenseGate(
                resolution,
                context.EnvironmentVariable),
            license => new CellsWorkbookEngine(
                license,
                context.ResourceBudgets,
                context.SafeFileWriter));

}
