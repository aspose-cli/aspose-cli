using Aspose.Cli.Sdk.Operations;
using Core = Aspose.Cli.Product.Cells.Contracts.CellsCoreOpValidator;
using Advanced = Aspose.Cli.Product.Cells.Contracts.CellsAdvancedOpValidator;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The Cells operation vocabulary: the single place an operation is registered. Append new
/// operations at the end; the order is published through capabilities and the schema.
/// </summary>
public static class CellsOps
{
    public static OperationCatalog<Op> Catalog { get; } = new OperationCatalog<Op>(CellsSchemaIds.Ops, maximumOperations: 10_000)
        .Add<SetValuesOp>("set_values", static op => Core.ValidateSetValues(op))
        .Add<SetFormulaOp>("set_formula", static op => Core.ValidateSetFormula(op))
        .Add<ClearRangeOp>("clear_range", static op => Core.ValidateClear(op))
        .Add<CopyRangeOp>("copy_range", static op => Core.ValidateCopy(op))
        .Add<FormatRangeOp>("format_range", static op => Core.ValidateFormat(op))
        .Add<MergeCellsOp>("merge_cells", static op => Core.ValidateRange(op, op.Range))
        .Add<UnmergeCellsOp>("unmerge_cells", static op => Core.ValidateRange(op, op.Range))
        .Add<InsertRowsOp>("insert_rows", static op => Core.ValidateRow(op, op.At, op.Count))
        .Add<DeleteRowsOp>("delete_rows", static op => Core.ValidateRow(op, op.At, op.Count))
        .Add<InsertColumnsOp>("insert_columns", static op => Core.ValidateColumn(op, op.At, op.Count))
        .Add<DeleteColumnsOp>("delete_columns", static op => Core.ValidateColumn(op, op.At, op.Count))
        .Add<ResizeRowsOp>("resize_rows", static op => Core.ValidateResizeRows(op))
        .Add<ResizeColumnsOp>("resize_columns", static op => Core.ValidateResizeColumns(op))
        .Add<AddSheetOp>("add_sheet", static op => Core.ValidateAddSheet(op))
        .Add<RenameSheetOp>("rename_sheet", static op => Core.ValidateRenameSheet(op))
        .Add<DeleteSheetOp>("delete_sheet", static op => Core.ValidateNamedSheet(op))
        .Add<SetSheetVisibilityOp>("set_sheet_visibility", static op => Core.ValidateNamedSheet(op))
        .Add<FreezePanesOp>("freeze_panes", static op => Core.ValidateFreeze(op))
        .Add<CreateChartOp>("create_chart", static op => Core.ValidateChart(op))
        .Add<CreatePivotOp>("create_pivot", static op => Core.ValidatePivot(op))
        .Add<SetPageSetupOp>("set_page_setup", static op => Core.ValidatePageSetup(op))
        .Add<SetPrintAreaOp>("set_print_area", static op => Core.ValidatePrintArea(op))
        .Add<InsertImageOp>("insert_image", static op => Core.ValidateInsertImage(op))
        .Add<RefreshPivotOp>("refresh_pivot")
        .Add<CreateTableOp>("create_table", static op => Core.ValidateRange(op, op.Range))
        .Add<SetAutoFilterOp>("set_autofilter", static op => Core.ValidateAutoFilter(op))
        .Add<SortRangeOp>("sort_range", static op => Core.ValidateSort(op))
        .Add<SetValidationOp>("set_validation", static op => Core.ValidateValidation(op))
        .Add<DefineNameOp>("define_name", static op => Core.ValidateDefineName(op))
        .Add<DeleteNameOp>("delete_name", static op => Core.ValidateName(op, op.Name))
        .Add<AddCommentOp>("add_comment", static op => Core.ValidateComment(op, op.Cell, op.Text))
        .Add<EditCommentOp>("edit_comment", static op => Core.ValidateComment(op, op.Cell, op.Text))
        .Add<DeleteCommentOp>("delete_comment", static op => Core.ValidateCommentCell(op, op.Cell))
        .Add<ProtectSheetOp>("protect_sheet", static op => Core.ValidateProtect(op))
        .Add<UnprotectSheetOp>("unprotect_sheet")
        .Add<GroupRowsOp>("group_rows", static op => Core.ValidateRowSpan(op, op.From, op.To))
        .Add<UngroupRowsOp>("ungroup_rows", static op => Core.ValidateRowSpan(op, op.From, op.To))
        .Add<GroupColumnsOp>("group_columns", static op => Core.ValidateColumnSpan(op, op.From, op.To))
        .Add<UngroupColumnsOp>("ungroup_columns", static op => Core.ValidateColumnSpan(op, op.From, op.To))
        .Add<ClearValidationOp>("clear_validation", static op => Core.ValidateRange(op, op.Range))
        .Add<RemoveDuplicatesOp>("remove_duplicates", static op => Core.ValidateRemoveDuplicates(op))
        .Add<ProtectWorkbookOp>("protect_workbook")
        .Add<UnprotectWorkbookOp>("unprotect_workbook")
        .Add<SetHyperlinkOp>("set_hyperlink", static op => Core.ValidateSetHyperlink(op))
        .Add<RemoveHyperlinkOp>("remove_hyperlink", static op => Core.ValidateCommentCell(op, op.Cell))
        .Add<UpdateChartOp>("update_chart", static op => Advanced.ValidateUpdateChart(op))
        .Add<AddConditionalFormatOp>("add_conditional_format", static op => Advanced.ValidateAddConditionalFormat(op))
        .Add<ClearConditionalFormatsOp>("clear_conditional_formats", static op => Core.ValidateRange(op, op.Range))
        .Add<MoveSheetOp>("move_sheet", static op => Core.ValidateMoveSheet(op))
        .Add<SetBordersOp>("set_borders", static op => Advanced.ValidateSetBorders(op))
        .Add<SetDefaultFontOp>("set_default_font", static op => Advanced.ValidateSetDefaultFont(op))
        .Add<SetTabColorOp>("set_tab_color", static op => Advanced.ValidateSetTabColor(op))
        .Add<SetSheetViewOp>("set_sheet_view", static op => Advanced.ValidateSetSheetView(op))
        .Add<DeleteChartOp>("delete_chart", static op => Advanced.ValidateDeleteChart(op))
        .Add<AddSparklineOp>("add_sparkline", static op => Advanced.ValidateAddSparkline(op))
        .Add<SetActiveSheetOp>("set_active_sheet", static op => Core.ValidateNamedSheet(op));
}
