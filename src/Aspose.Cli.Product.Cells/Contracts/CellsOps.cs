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
[OperationVocabulary("ops", JsonContext = typeof(CellsOpsJsonContext), Addresses = new[] { "sheet", "sourceSheet", "range", "cell", "at", "from", "to", "first", "last", "index", "path", "passwordEnv", "sourcePasswordEnv" },
    Common = new[] { "set_values", "set_formula", "format_range", "resize_columns", "insert_chart", "insert_sheet", "rename_sheet", "freeze_panes", "set_borders", "merge_cells", "set_autofilter", "define_table", "sort_range", "add_conditional_format", "set_validation", "insert_pivot", "set_page_setup" })]
[JsonConverter(typeof(OperationJsonConverter<CellsOp>))]
public abstract partial record CellsOp : BoundedOperation
{
    /// <summary>The sheet the operation targets; null for the active sheet or an operation on the workbook.</summary>
    internal string? TargetSheet => this is CellsSheetOp sheetOp ? sheetOp.Sheet : null;
}

/// <summary>An operation on one sheet, named by its sheet field.</summary>
public abstract record CellsSheetOp : CellsOp
{
    /// <summary>The target sheet; the active sheet when omitted.</summary>
    public virtual string? Sheet { get; init; }
}

/// <summary>Source-generated serialization metadata for Cells operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AddCommentOp))]
[JsonSerializable(typeof(AddConditionalFormatOp))]
[JsonSerializable(typeof(InsertSheetOp))]
[JsonSerializable(typeof(AddSparklineOp))]
[JsonSerializable(typeof(ClearAutoFilterOp))]
[JsonSerializable(typeof(ClearConditionalFormatsOp))]
[JsonSerializable(typeof(ClearPrintAreaOp))]
[JsonSerializable(typeof(ClearRangeOp))]
[JsonSerializable(typeof(ClearTabColorOp))]
[JsonSerializable(typeof(ClearValidationOp))]
[JsonSerializable(typeof(CopyRangeOp))]
[JsonSerializable(typeof(InsertChartOp))]
[JsonSerializable(typeof(InsertPivotOp))]
[JsonSerializable(typeof(DefineTableOp))]
[JsonSerializable(typeof(DefineNameOp))]
[JsonSerializable(typeof(DeleteChartOp))]
[JsonSerializable(typeof(DeleteColumnsOp))]
[JsonSerializable(typeof(DeleteCommentOp))]
[JsonSerializable(typeof(DeleteNameOp))]
[JsonSerializable(typeof(DeleteRowsOp))]
[JsonSerializable(typeof(DeleteSheetOp))]
[JsonSerializable(typeof(SetCommentOp))]
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
[JsonSerializable(typeof(DeleteDuplicatesOp))]
[JsonSerializable(typeof(DeleteHyperlinkOp))]
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
[JsonSerializable(typeof(ReplaceTextOp))]
[JsonSerializable(typeof(SetTabColorOp))]
[JsonSerializable(typeof(SetValidationOp))]
[JsonSerializable(typeof(SetValuesOp))]
[JsonSerializable(typeof(SortRangeOp))]
[JsonSerializable(typeof(UngroupColumnsOp))]
[JsonSerializable(typeof(UngroupRowsOp))]
[JsonSerializable(typeof(UnfreezePanesOp))]
[JsonSerializable(typeof(UnmergeCellsOp))]
[JsonSerializable(typeof(UnprotectSheetOp))]
[JsonSerializable(typeof(UnprotectWorkbookOp))]
[JsonSerializable(typeof(SetChartOp))]
internal sealed partial class CellsOpsJsonContext : JsonSerializerContext;
