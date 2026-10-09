using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>A validated atomic Cells edit batch, the payload of <c>cells edit --ops</c>.</summary>
[ProductJsonRoot]
public sealed record CellsOpsBatch : BoundedOperationEnvelope<CellsOp>;

/// <summary>
/// The operation document of <c>cells edit --ops</c>. An operation's <c>sheet</c> defaults to
/// the active sheet; rows are 1-based numbers, columns are letters, and cells and ranges use A1
/// notation on that sheet unless a field says it may name another sheet.
/// </summary>
[OperationVocabulary(Sdk.DistributionInfo.SchemaBaseUri + "cells/ops.schema.json", MaximumOperations = 10_000, JsonContext = typeof(CellsOpsJsonContext))]
[JsonConverter(typeof(OperationJsonConverter<CellsOp>))]
public abstract partial record CellsOp : BoundedOperation
{
    /// <summary>The target sheet; the active sheet when omitted.</summary>
    public virtual string? Sheet { get; init; }
}

/// <summary>Source-generated serialization metadata for Cells operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AddCommentOp))]
[JsonSerializable(typeof(AddConditionalFormatOp))]
[JsonSerializable(typeof(AddSheetOp))]
[JsonSerializable(typeof(AddSparklineOp))]
[JsonSerializable(typeof(ClearConditionalFormatsOp))]
[JsonSerializable(typeof(ClearRangeOp))]
[JsonSerializable(typeof(ClearValidationOp))]
[JsonSerializable(typeof(CopyRangeOp))]
[JsonSerializable(typeof(CreateChartOp))]
[JsonSerializable(typeof(CreatePivotOp))]
[JsonSerializable(typeof(CreateTableOp))]
[JsonSerializable(typeof(DefineNameOp))]
[JsonSerializable(typeof(DeleteChartOp))]
[JsonSerializable(typeof(DeleteColumnsOp))]
[JsonSerializable(typeof(DeleteCommentOp))]
[JsonSerializable(typeof(DeleteNameOp))]
[JsonSerializable(typeof(DeleteRowsOp))]
[JsonSerializable(typeof(DeleteSheetOp))]
[JsonSerializable(typeof(EditCommentOp))]
[JsonSerializable(typeof(FormatRangeOp))]
[JsonSerializable(typeof(FreezePanesOp))]
[JsonSerializable(typeof(GroupColumnsOp))]
[JsonSerializable(typeof(GroupRowsOp))]
[JsonSerializable(typeof(ImportRangeOp))]
[JsonSerializable(typeof(ImportSheetOp))]
[JsonSerializable(typeof(InsertColumnsOp))]
[JsonSerializable(typeof(InsertImageOp))]
[JsonSerializable(typeof(InsertRowsOp))]
[JsonSerializable(typeof(MergeCellsOp))]
[JsonSerializable(typeof(MoveSheetOp))]
[JsonSerializable(typeof(ProtectSheetOp))]
[JsonSerializable(typeof(ProtectWorkbookOp))]
[JsonSerializable(typeof(RefreshPivotOp))]
[JsonSerializable(typeof(RemoveDuplicatesOp))]
[JsonSerializable(typeof(RemoveHyperlinkOp))]
[JsonSerializable(typeof(RenameSheetOp))]
[JsonSerializable(typeof(ResizeColumnsOp))]
[JsonSerializable(typeof(ResizeRowsOp))]
[JsonSerializable(typeof(SetActiveSheetOp))]
[JsonSerializable(typeof(SetAutoFilterOp))]
[JsonSerializable(typeof(SetBordersOp))]
[JsonSerializable(typeof(SetDefaultFontOp))]
[JsonSerializable(typeof(SetFormulaOp))]
[JsonSerializable(typeof(SetHyperlinkOp))]
[JsonSerializable(typeof(SetPageSetupOp))]
[JsonSerializable(typeof(SetPrintAreaOp))]
[JsonSerializable(typeof(SetSheetViewOp))]
[JsonSerializable(typeof(SetSheetVisibilityOp))]
[JsonSerializable(typeof(SetTabColorOp))]
[JsonSerializable(typeof(SetValidationOp))]
[JsonSerializable(typeof(SetValuesOp))]
[JsonSerializable(typeof(SortRangeOp))]
[JsonSerializable(typeof(UngroupColumnsOp))]
[JsonSerializable(typeof(UngroupRowsOp))]
[JsonSerializable(typeof(UnmergeCellsOp))]
[JsonSerializable(typeof(UnprotectSheetOp))]
[JsonSerializable(typeof(UnprotectWorkbookOp))]
[JsonSerializable(typeof(UpdateChartOp))]
internal sealed partial class CellsOpsJsonContext : JsonSerializerContext;
