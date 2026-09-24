using Aspose.Cli.Sdk;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Stable schema identifiers owned by the Slides product.</summary>
public static class SlidesSchemaIds
{
    private const string BaseUri = DistributionInfo.SchemaBaseUri + ProductBuildMetadata.ProductId + "/";
    public const string PresentationInfo = BaseUri + "presentation-info.schema.json";
    public const string PresentationRead = BaseUri + "presentation-read.schema.json";
    public const string ConvertResult = BaseUri + "convert-result.schema.json";
    public const string RenderResult = BaseUri + "render-result.schema.json";
    public const string CreateResult = BaseUri + "create-result.schema.json";
    public const string ExtractResult = BaseUri + "extract-result.schema.json";
    public const string Ops = BaseUri + "ops.schema.json";
    public const string EditResult = BaseUri + "edit-result.schema.json";
    public const string SearchResult = BaseUri + "search-result.schema.json";
}
