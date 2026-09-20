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
[JsonSerializable(typeof(Product.Words.Contracts.ReplaceTextOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetTextOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertParagraphsOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertMarkdownOp))]
[JsonSerializable(typeof(Product.Words.Contracts.DeleteBlocksOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertBreakOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertImageOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertTableOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetTableCellOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertTocOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertBookmarkOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertHyperlinkOp))]
[JsonSerializable(typeof(Product.Words.Contracts.InsertFieldOp))]
[JsonSerializable(typeof(Product.Words.Contracts.AddSectionOp))]
[JsonSerializable(typeof(Product.Words.Contracts.DeleteSectionOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetPageSetupOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetHeaderOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetFooterOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetPageNumbersOp))]
[JsonSerializable(typeof(Product.Words.Contracts.FormatTextOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetStyleOp))]
[JsonSerializable(typeof(Product.Words.Contracts.DefineStyleOp))]
[JsonSerializable(typeof(Product.Words.Contracts.ApplyListOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetDefaultFontOp))]
[JsonSerializable(typeof(Product.Words.Contracts.SetPropertiesOp))]
[JsonSerializable(typeof(Product.Words.Contracts.AddWatermarkOp))]
[JsonSerializable(typeof(Product.Words.Contracts.RemoveWatermarkOp))]
[JsonSerializable(typeof(Product.Words.Contracts.ProtectOp))]
[JsonSerializable(typeof(Product.Words.Contracts.UnprotectOp))]
[JsonSerializable(typeof(Product.Words.Contracts.AcceptRevisionsOp))]
[JsonSerializable(typeof(Product.Words.Contracts.RejectRevisionsOp))]
[JsonSerializable(typeof(Product.Words.Contracts.AddCommentOp))]
[JsonSerializable(typeof(Product.Words.Contracts.RemoveCommentsOp))]
[JsonSerializable(typeof(Product.Words.Contracts.AppendDocumentOp))]
[JsonSerializable(typeof(Product.Words.Contracts.MailMergeOp))]
[JsonSerializable(typeof(Product.Words.Contracts.UpdateFieldsOp))]
internal sealed partial class ProductJsonContext : JsonSerializerContext
{
    internal static ProductJsonDefinition Definition => DefinitionHolder.Value;

    private static class DefinitionHolder
    {
        internal static readonly ProductJsonDefinition Value =
            new(Product.Words.ProductBuildMetadata.ProductId, Default);
    }
}
