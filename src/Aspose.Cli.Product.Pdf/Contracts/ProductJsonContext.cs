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
[JsonSerializable(typeof(Product.Pdf.Contracts.RotatePagesOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.DeletePagesOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.MovePagesOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.InsertPagesFromOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.InsertBlankPageOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.CropPagesOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.SetPageSizeOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddWatermarkTextOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddWatermarkImageOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddPageNumbersOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddHeaderTextOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddFooterTextOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddStampImageOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddLinkOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.RedactTextOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.RedactAreaOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.SetMetadataOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.RemoveMetadataOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddBookmarkOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.DeleteBookmarksOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.AddAttachmentOp), TypeInfoPropertyName = "PdfAddAttachmentOp")]
[JsonSerializable(typeof(Product.Pdf.Contracts.RemoveAttachmentOp), TypeInfoPropertyName = "PdfRemoveAttachmentOp")]
[JsonSerializable(typeof(Product.Pdf.Contracts.SetPageLabelsOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.SetFormFieldOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.FlattenFormsOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.EncryptPdfOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.DecryptPdfOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.OptimizePdfOp))]
[JsonSerializable(typeof(Product.Pdf.Contracts.LinearizePdfOp))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Pdf.ProductBuildMetadata.ProductId, Default);
    }
}
