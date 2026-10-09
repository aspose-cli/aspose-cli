using Aspose.Cli.Product.Cells.Commands;
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
        Aspose.Cli.Sdk.Extensibility.Product.Define<CellsSession>(Manifest)
            .Formats(CellsFormats.Definitions)
            .Diagnostics(CellsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .View(new CellsViewAdapter())
            .Describe("Spreadsheet operations (Excel and friends) with engine-grade fidelity.", Help)
            .Command(InfoCommand.Create, CellsInfo.Run)
            .Group("query", "Read bounded workbook data without mutating the source file.", static query => query
                .Command(ReadCommand.Create, CellsRead.Run)
                .Command(SearchCommand.Create, CellsSearch.Run))
            .Command(NewCommand.Create, CellsCreate.Run)
            .Command(EditCommand.Create, CellsEdit.Run)
            .Command(DiffCommand.Create, CellsDiff.Run)
            .Command(ConvertCommand.Create, CellsConvert.Run)
            .Command(RenderCommand.Create, CellsRender.Run)
            .Activator(static context => CellsActivation.Activate(context, Manifest.Id))
            .Build();

    private static CommandHelp Help() => new(
        ["cells inspect book.xlsx --output json"],
        [
            CommandHelpLink.Docs(Manifest, "editing", "the edit-operation vocabulary and recipes"),
            CommandHelpLink.Docs(Manifest, "workbook-standards", "professional workbook construction guidance"),
            CommandHelpLink.Docs(Manifest, "verification", "the spreadsheet delivery verification protocol"),
            CommandHelpLink.Schema(Manifest, "the operations JSON Schema"),
        ]);
}
