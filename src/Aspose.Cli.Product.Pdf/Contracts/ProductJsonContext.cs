using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Generated;

/// <summary>Source-generated serialization metadata for PDF contracts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfInfoResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfReadResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfConvertResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfRenderResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfWriteResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfSplitResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfExtractResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfEditResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfFormResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfFormExportResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfSearchResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfValidateResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfSignResult))]
[JsonSerializable(typeof(Product.Pdf.Contracts.PdfOpsBatch))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Pdf.ProductBuildMetadata.ProductId, Default);
    }
}
