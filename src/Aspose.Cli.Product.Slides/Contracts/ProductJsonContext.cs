using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Generated;

/// <summary>Source-generated serialization metadata for Slides contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(Product.Slides.Contracts.PresentationInfoResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.PresentationReadResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesConvertResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesRenderResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesCreateResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesExtractResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesEditResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesSearchResult))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesOpsBatch))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Slides.ProductBuildMetadata.ProductId, Default);
    }
}
