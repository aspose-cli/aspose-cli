using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Generated;

/// <summary>Source-generated serialization metadata for Cells contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(Product.Cells.Contracts.WorkbookInfoResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.WorkbookReadResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.ConvertResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.RenderResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.EditResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.EditVerification))]
[JsonSerializable(typeof(Product.Cells.Contracts.CreateResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.DiffResult))]
[JsonSerializable(typeof(Product.Cells.Contracts.SearchResult))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(Product.Cells.Contracts.OpsBatch))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Cells.ProductBuildMetadata.ProductId, Default);
    }
}
