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
            ResourceBudgets =
            [
                ResourceBudgetCapabilities.Domain(CellsBudgetDomains.Sheets, 1_000, 10_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(CellsBudgetDomains.Cells, 10_000_000, 100_000_000, "items", "post-load"),
                ResourceBudgetCapabilities.Domain(CellsBudgetDomains.Objects, 100_000, 1_000_000, "items", "post-load"),
            ],
            Engine = new ProductEngineCapabilities
            {
                Id = "aspose",
                Sdk = ProductBuildMetadata.EngineName,
                SdkVersion = ProductBuildMetadata.SdkVersion,
            },
            AvailableEngines = ["aspose"],
    };

    /// <summary>Loads the Cells object table, which names engine types: the commands call it only when they run.</summary>
    internal static Aspose.Cli.Sdk.Tables.DocumentTable Table() => Engine.Mapping.CellsTable.Definition;

    /// <summary>Returns the pure, immutable Cells product definition.</summary>
    public ProductDefinition Define() =>
        Aspose.Cli.Sdk.Extensibility.Product.Define<CellsSession>(Manifest)
            .Formats(CellsFormats.Definitions)
            .Diagnostics(CellsDiagnostics.All)
            .Json(ProductJsonContext.Definition)
            .Describe("Read, edit, convert and render workbooks through the Aspose.Cells object model.", Help)
            .Command(ReadCommand.Create, CellsRead.Run)
            .Command(SearchCommand.Create, CellsSearch.Run)
            .Command(CreateCommand.Create, CellsCreate.Run)
            .Command(EditCommand.Create, CellsEdit.Run)
            .Command(ConvertCommand.Create, CellsConvert.Run)
            .Command(RenderCommand.Create, CellsRender.Run)
            .Activator(CellsActivation.Activate)
            .Build();

    private static CommandHelp Help() => new(
        [
            "cells read book.xlsx",
            "cells read book.xlsx --at \"sheet/Sales 2026/range/A1:F6\"",
            "cells edit book.xlsx --ops ops.json --out revised.xlsx",
        ],
        [
            new("aspose-cli schema v2/cells/ops --operation directory", "the object table: kinds, addresses, rows, adds and actions"),
        ]);
}
