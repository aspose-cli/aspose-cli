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
[JsonSerializable(typeof(Product.Slides.Contracts.AddSlideOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.DeleteSlidesOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.MoveSlideOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.DuplicateSlideOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetSlideHiddenOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.ApplyLayoutOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetBackgroundOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.AddSectionOp), TypeInfoPropertyName = "SlidesAddSectionOp")]
[JsonSerializable(typeof(Product.Slides.Contracts.AppendPresentationOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetTitleOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetBodyOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetTextOp), TypeInfoPropertyName = "SlidesSetTextOp")]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesReplaceTextOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetNotesOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesInsertImageOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.InsertShapeOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesInsertTableOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesSetTableCellOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.InsertChartOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.UpdateChartDataOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.DeleteShapeOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetShapeStyleOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetFooterOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetTransitionOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SlidesSetPropertiesOp))]
[JsonSerializable(typeof(Product.Slides.Contracts.SetSlideSizeOp))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Slides.ProductBuildMetadata.ProductId, Default);
    }
}
