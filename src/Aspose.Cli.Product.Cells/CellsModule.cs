using Aspose.Cli.Product.Cells.Commands;
using Aspose.Cli.Product.Cells.Output;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells;

/// <summary>Pure build-time definition and deferred activation for Cells.</summary>
public sealed class CellsModule : IProductModule
{
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
            .Formats(CellsFormats.Definitions)
            .Diagnostics(CellsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .Preview(new CellsPreviewAdapter())
            .View(new CellsViewAdapter())
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
