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
        DisplayOrder = ProductBuildMetadata.DisplayOrder,
        IsDefaultCandidate = ProductBuildMetadata.IsDefaultCandidate,
        Operations = [CellsOp.Catalog.Describe("edit")],
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
        Aspose.Cli.Sdk.Extensibility.Product.Define<ICellsEngine>(Manifest)
            .Formats(CellsFormats.Definitions)
            .Diagnostics(CellsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
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
            .Activator(static context => CellsActivation.Activate(context, Manifest.Id))
            .Build();

}
