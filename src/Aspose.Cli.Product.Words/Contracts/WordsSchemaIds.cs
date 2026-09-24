using Aspose.Cli.Sdk;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Canonical schema identifiers owned by the Words product.</summary>
public static class WordsSchemaIds
{
    private const string Base = DistributionInfo.SchemaBaseUri + ProductBuildMetadata.ProductId + "/";

    public const string DocumentInfo = Base + "document-info.schema.json";
    public const string DocumentRead = Base + "document-read.schema.json";
    public const string ConvertResult = Base + "convert-result.schema.json";
    public const string RenderResult = Base + "render-result.schema.json";
    public const string CreateResult = Base + "create-result.schema.json";
    public const string EditResult = Base + "edit-result.schema.json";
    public const string CompareResult = Base + "compare-result.schema.json";
    public const string SearchResult = Base + "search-result.schema.json";
    public const string SplitResult = Base + "split-result.schema.json";
    public const string ExtractResult = Base + "extract-result.schema.json";
    public const string Ops = Base + "ops.schema.json";
}
