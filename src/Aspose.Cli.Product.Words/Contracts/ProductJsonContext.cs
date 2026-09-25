using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Generated;

/// <summary>Source-generated serialization metadata for Words contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(Product.Words.Contracts.DocumentInfoResult))]
[JsonSerializable(typeof(Product.Words.Contracts.DocumentReadResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsConvertResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsRenderResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsCreateResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsEditResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsCompareResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsSearchResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsSplitResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsExtractResult))]
[JsonSerializable(typeof(Product.Words.Contracts.WordsOpsBatch))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Words.ProductBuildMetadata.ProductId, Default);
    }
}
