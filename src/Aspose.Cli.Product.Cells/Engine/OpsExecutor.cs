using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Applies a validated ops batch to an in-memory workbook. The product-neutral
/// loop — index bookkeeping and the atomic-vs-best-effort contract — lives in
/// <see cref="OpsBatchRunner"/>; this class supplies the SDK-specific dispatch
/// and launders Aspose.Cells' own exception into <see cref="EngineOpException"/>
/// so Core normalizes failures without ever referencing the engine SDK. Each
/// op's work lives in a mapper.
/// </summary>
internal static class OpsExecutor
{
    public static IReadOnlyList<BoundedOperationOutcome> Execute(Workbook workbook, OpsBatch batch, bool continueOnError,
        IReadOnlyDictionary<string, string?>? secrets) =>
        OpsBatchRunner.Run(batch, op => Apply(workbook, op, secrets), continueOnError);

    /// <summary>
    /// Applies one op, laundering the SDK's <see cref="CellsException"/> into the
    /// Core-visible <see cref="EngineOpException"/>; a mapper's own
    /// <c>CliException</c> propagates untouched for the runner to normalize.
    /// </summary>
    private static long? Apply(Workbook workbook, Op op, IReadOnlyDictionary<string, string?>? secrets)
    {
        try
        {
            return Dispatch(workbook, op, secrets);
        }
        catch (CellsException ex)
        {
            throw new EngineOpException(ex.Message, ex);
        }
    }

    /// <summary>Routes one op to its mapper; returns the touched cell count where meaningful.</summary>
    private static long? Dispatch(Workbook workbook, Op op, IReadOnlyDictionary<string, string?>? secrets) => op switch
    {
        RecalculateOp => Recalculate(workbook),
        SetValuesOp or SetFormulaOp or ClearRangeOp or CopyRangeOp or FormatRangeOp
            or MergeCellsOp or UnmergeCellsOp => DispatchCell(workbook, op),
        InsertRowsOp or DeleteRowsOp or InsertColumnsOp or DeleteColumnsOp
            or ResizeRowsOp or ResizeColumnsOp => DispatchStructure(workbook, op),
        AddSheetOp or RenameSheetOp or DeleteSheetOp or SetSheetVisibilityOp or MoveSheetOp or FreezePanesOp
            or SetActiveSheetOp
            => DispatchSheet(workbook, op),
        CreateChartOp or CreatePivotOp or InsertImageOp or RefreshPivotOp or CreateTableOp
            or UpdateChartOp or DeleteChartOp or AddSparklineOp => DispatchObject(workbook, op),
        SetPageSetupOp or SetPrintAreaOp or SetAutoFilterOp or SortRangeOp or SetValidationOp
            or DefineNameOp or DeleteNameOp or ClearValidationOp or AddConditionalFormatOp
            or ClearConditionalFormatsOp or SetBordersOp => DispatchData(workbook, op),
        AddCommentOp or EditCommentOp or DeleteCommentOp or ProtectSheetOp or UnprotectSheetOp
            or GroupRowsOp or UngroupRowsOp or GroupColumnsOp or UngroupColumnsOp or RemoveDuplicatesOp
            or ProtectWorkbookOp or UnprotectWorkbookOp or SetHyperlinkOp or RemoveHyperlinkOp
            => DispatchReview(workbook, op, secrets),
        SetDefaultFontOp or SetTabColorOp or SetSheetViewOp => DispatchLook(workbook, op),
        _ => throw new InvalidOperationException($"Unhandled op type {op.GetType().Name}."),
    };

    private static long? Recalculate(Workbook workbook)
    {
        workbook.CalculateFormula();
        return null;
    }

    private static long? DispatchCell(Workbook workbook, Op op) => op switch
    {
        SetValuesOp setValues => CellOps.SetValues(Sheets.Resolve(workbook, op), setValues),
        SetFormulaOp setFormula => CellOps.SetFormula(Sheets.Resolve(workbook, op), setFormula),
        ClearRangeOp clear => CellOps.Clear(Sheets.Resolve(workbook, op), clear),
        CopyRangeOp copy => CellOps.Copy(workbook, op, copy),
        FormatRangeOp format => CellOps.Format(workbook, Sheets.Resolve(workbook, op), format),
        MergeCellsOp merge => CellOps.Merge(Sheets.Resolve(workbook, op), merge.Range, merged: true),
        UnmergeCellsOp unmerge => CellOps.Merge(Sheets.Resolve(workbook, op), unmerge.Range, merged: false),
        _ => throw new InvalidOperationException(),
    };

    private static long? DispatchStructure(Workbook workbook, Op op) => op switch
    {
        InsertRowsOp insertRows => RowColumnOps.InsertRows(Sheets.Resolve(workbook, op), insertRows),
        DeleteRowsOp deleteRows => RowColumnOps.DeleteRows(Sheets.Resolve(workbook, op), deleteRows),
        InsertColumnsOp insertColumns => RowColumnOps.InsertColumns(Sheets.Resolve(workbook, op), insertColumns),
        DeleteColumnsOp deleteColumns => RowColumnOps.DeleteColumns(Sheets.Resolve(workbook, op), deleteColumns),
        ResizeRowsOp resizeRows => RowColumnOps.ResizeRows(Sheets.Resolve(workbook, op), resizeRows),
        ResizeColumnsOp resizeColumns => RowColumnOps.ResizeColumns(Sheets.Resolve(workbook, op), resizeColumns),
        _ => throw new InvalidOperationException(),
    };

    private static long? DispatchSheet(Workbook workbook, Op op) => op switch
    {
        AddSheetOp addSheet => SheetOps.AddSheet(workbook, addSheet),
        RenameSheetOp rename => SheetOps.RenameSheet(Sheets.Resolve(workbook, op), rename),
        DeleteSheetOp => SheetOps.DeleteSheet(workbook, Sheets.Resolve(workbook, op)),
        SetSheetVisibilityOp visibility => SheetOps.SetVisibility(Sheets.Resolve(workbook, op), visibility),
        MoveSheetOp move => SheetOps.MoveSheet(Sheets.Resolve(workbook, op), move),
        FreezePanesOp freeze => SheetOps.Freeze(Sheets.Resolve(workbook, op), freeze),
        SetActiveSheetOp => SheetOps.SetActiveSheet(workbook, Sheets.Resolve(workbook, op)),
        _ => throw new InvalidOperationException(),
    };

    private static long? DispatchObject(Workbook workbook, Op op) => op switch
    {
        CreateChartOp chart => ChartPivotOps.CreateChart(Sheets.Resolve(workbook, op), chart),
        CreatePivotOp pivot => ChartPivotOps.CreatePivot(Sheets.Resolve(workbook, op), pivot),
        InsertImageOp insertImage => ImageOps.InsertImage(Sheets.Resolve(workbook, op), insertImage),
        RefreshPivotOp refreshPivot => ChartPivotOps.RefreshPivot(Sheets.Resolve(workbook, op), refreshPivot),
        CreateTableOp createTable => TableOps.CreateTable(Sheets.Resolve(workbook, op), createTable),
        UpdateChartOp updateChart => ChartPivotOps.UpdateChart(Sheets.Resolve(workbook, op), updateChart),
        DeleteChartOp deleteChart => ChartPivotOps.DeleteChart(Sheets.Resolve(workbook, op), deleteChart),
        AddSparklineOp addSparkline => SparklineOps.AddSparkline(workbook, Sheets.Resolve(workbook, op), addSparkline),
        _ => throw new InvalidOperationException(),
    };

    private static long? DispatchData(Workbook workbook, Op op) => op switch
    {
        SetPageSetupOp pageSetup => PageSetupOps.SetPageSetup(Sheets.Resolve(workbook, op), pageSetup),
        SetPrintAreaOp printArea => PageSetupOps.SetPrintArea(Sheets.Resolve(workbook, op), printArea),
        SetAutoFilterOp autoFilter => SortFilterOps.SetAutoFilter(Sheets.Resolve(workbook, op), autoFilter),
        SortRangeOp sort => SortFilterOps.SortRange(workbook, Sheets.Resolve(workbook, op), sort),
        SetValidationOp validation => ValidationOps.SetValidation(Sheets.Resolve(workbook, op), validation),
        DefineNameOp defineName => NameOps.DefineName(workbook, defineName),
        DeleteNameOp deleteName => NameOps.DeleteName(workbook, deleteName),
        ClearValidationOp clearValidation => ValidationOps.ClearValidation(Sheets.Resolve(workbook, op), clearValidation),
        AddConditionalFormatOp addConditional => ConditionalFormatOps.AddConditionalFormat(workbook, Sheets.Resolve(workbook, op), addConditional),
        ClearConditionalFormatsOp clearConditional => ConditionalFormatOps.ClearConditionalFormats(Sheets.Resolve(workbook, op), clearConditional),
        SetBordersOp setBorders => BorderOps.SetBorders(workbook, Sheets.Resolve(workbook, op), setBorders),
        _ => throw new InvalidOperationException(),
    };

    private static long? DispatchReview(Workbook workbook, Op op, IReadOnlyDictionary<string, string?>? secrets) => op switch
    {
        AddCommentOp addComment => CommentOps.AddComment(Sheets.Resolve(workbook, op), addComment),
        EditCommentOp editComment => CommentOps.EditComment(Sheets.Resolve(workbook, op), editComment),
        DeleteCommentOp deleteComment => CommentOps.DeleteComment(Sheets.Resolve(workbook, op), deleteComment),
        ProtectSheetOp protect => ProtectOps.ProtectSheet(Sheets.Resolve(workbook, op), protect, secrets),
        UnprotectSheetOp unprotect => ProtectOps.UnprotectSheet(Sheets.Resolve(workbook, op), unprotect, secrets),
        GroupRowsOp groupRows => OutlineOps.GroupRows(Sheets.Resolve(workbook, op), groupRows),
        UngroupRowsOp ungroupRows => OutlineOps.UngroupRows(Sheets.Resolve(workbook, op), ungroupRows),
        GroupColumnsOp groupColumns => OutlineOps.GroupColumns(Sheets.Resolve(workbook, op), groupColumns),
        UngroupColumnsOp ungroupColumns => OutlineOps.UngroupColumns(Sheets.Resolve(workbook, op), ungroupColumns),
        RemoveDuplicatesOp removeDuplicates => DedupeOps.RemoveDuplicates(Sheets.Resolve(workbook, op), removeDuplicates),
        ProtectWorkbookOp protectWorkbook => ProtectOps.ProtectWorkbook(workbook, protectWorkbook, secrets),
        UnprotectWorkbookOp unprotectWorkbook => ProtectOps.UnprotectWorkbook(workbook, unprotectWorkbook, secrets),
        SetHyperlinkOp setHyperlink => HyperlinkOps.SetHyperlink(Sheets.Resolve(workbook, op), setHyperlink),
        RemoveHyperlinkOp removeHyperlink => HyperlinkOps.RemoveHyperlink(Sheets.Resolve(workbook, op), removeHyperlink),
        _ => throw new InvalidOperationException(),
    };

    private static long? DispatchLook(Workbook workbook, Op op) => op switch
    {
        // Workbook-scoped: sheet resolution is deliberately skipped.
        SetDefaultFontOp setDefaultFont => LookOps.SetDefaultFont(workbook, setDefaultFont),
        SetTabColorOp setTabColor => LookOps.SetTabColor(Sheets.Resolve(workbook, op), setTabColor),
        SetSheetViewOp setSheetView => LookOps.SetSheetView(Sheets.Resolve(workbook, op), setSheetView),
        _ => throw new InvalidOperationException(),
    };
}
