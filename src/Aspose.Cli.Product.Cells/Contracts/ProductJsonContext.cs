using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Generated;

/// <summary>Source-generated serialization metadata for Cells contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsInspectResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsReadResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsConvertResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsRenderResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsEditResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsCreateResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsCompareResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsSearchResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.CellsOpsBatch))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Cells.ProductBuildMetadata.ProductId, Default);
    }
}
