namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The op vocabulary of this build as data: the single place a new op is
/// registered. <see cref="OpNames"/> exposes the wire names and the
/// discriminator converter resolves them to types, and both derive from this
/// one list — so the wire name, the CLR record type and the documented order
/// can never drift apart as the vocabulary grows.
/// </summary>
/// <remarks>
/// Deliberately not a generic <c>OpRegistry&lt;TOp&gt;</c>: that shared
/// machinery is only justified once a second product needs it. A completeness
/// test in the contract tests fails the build if an <see cref="Op"/> record is
/// added without a row here.
/// </remarks>
public static class CellsOps
{
    // The ordered source of truth for both membership and documentation order.
    // Append new ops at the end: the order is wire-visible — it drives
    // OpNames.All, capabilities.operations and the schema's "op" enum.
    private static readonly (string Name, Type Type)[] Entries =
    [
        (OpNames.SetValues, typeof(SetValuesOp)),
        (OpNames.SetFormula, typeof(SetFormulaOp)),
        (OpNames.ClearRange, typeof(ClearRangeOp)),
        (OpNames.CopyRange, typeof(CopyRangeOp)),
        (OpNames.FormatRange, typeof(FormatRangeOp)),
        (OpNames.MergeCells, typeof(MergeCellsOp)),
        (OpNames.UnmergeCells, typeof(UnmergeCellsOp)),
        (OpNames.InsertRows, typeof(InsertRowsOp)),
        (OpNames.DeleteRows, typeof(DeleteRowsOp)),
        (OpNames.InsertColumns, typeof(InsertColumnsOp)),
        (OpNames.DeleteColumns, typeof(DeleteColumnsOp)),
        (OpNames.ResizeRows, typeof(ResizeRowsOp)),
        (OpNames.ResizeColumns, typeof(ResizeColumnsOp)),
        (OpNames.AddSheet, typeof(AddSheetOp)),
        (OpNames.RenameSheet, typeof(RenameSheetOp)),
        (OpNames.DeleteSheet, typeof(DeleteSheetOp)),
        (OpNames.SetSheetVisibility, typeof(SetSheetVisibilityOp)),
        (OpNames.FreezePanes, typeof(FreezePanesOp)),
        (OpNames.CreateChart, typeof(CreateChartOp)),
        (OpNames.CreatePivot, typeof(CreatePivotOp)),
        (OpNames.SetPageSetup, typeof(SetPageSetupOp)),
        (OpNames.SetPrintArea, typeof(SetPrintAreaOp)),
        (OpNames.InsertImage, typeof(InsertImageOp)),
        (OpNames.RefreshPivot, typeof(RefreshPivotOp)),
        (OpNames.CreateTable, typeof(CreateTableOp)),
        (OpNames.SetAutoFilter, typeof(SetAutoFilterOp)),
        (OpNames.SortRange, typeof(SortRangeOp)),
        (OpNames.SetValidation, typeof(SetValidationOp)),
        (OpNames.DefineName, typeof(DefineNameOp)),
        (OpNames.DeleteName, typeof(DeleteNameOp)),
        (OpNames.AddComment, typeof(AddCommentOp)),
        (OpNames.EditComment, typeof(EditCommentOp)),
        (OpNames.DeleteComment, typeof(DeleteCommentOp)),
        (OpNames.ProtectSheet, typeof(ProtectSheetOp)),
        (OpNames.UnprotectSheet, typeof(UnprotectSheetOp)),
        (OpNames.GroupRows, typeof(GroupRowsOp)),
        (OpNames.UngroupRows, typeof(UngroupRowsOp)),
        (OpNames.GroupColumns, typeof(GroupColumnsOp)),
        (OpNames.UngroupColumns, typeof(UngroupColumnsOp)),
        (OpNames.ClearValidation, typeof(ClearValidationOp)),
        (OpNames.RemoveDuplicates, typeof(RemoveDuplicatesOp)),
        (OpNames.ProtectWorkbook, typeof(ProtectWorkbookOp)),
        (OpNames.UnprotectWorkbook, typeof(UnprotectWorkbookOp)),
        (OpNames.SetHyperlink, typeof(SetHyperlinkOp)),
        (OpNames.RemoveHyperlink, typeof(RemoveHyperlinkOp)),
        (OpNames.UpdateChart, typeof(UpdateChartOp)),
        (OpNames.AddConditionalFormat, typeof(AddConditionalFormatOp)),
        (OpNames.ClearConditionalFormats, typeof(ClearConditionalFormatsOp)),
        (OpNames.MoveSheet, typeof(MoveSheetOp)),
        (OpNames.SetBorders, typeof(SetBordersOp)),
        (OpNames.SetDefaultFont, typeof(SetDefaultFontOp)),
        (OpNames.SetTabColor, typeof(SetTabColorOp)),
        (OpNames.SetSheetView, typeof(SetSheetViewOp)),
        (OpNames.DeleteChart, typeof(DeleteChartOp)),
        (OpNames.AddSparkline, typeof(AddSparklineOp)),
        (OpNames.SetActiveSheet, typeof(SetActiveSheetOp)),
        (OpNames.Recalculate, typeof(RecalculateOp)),
    ];

    /// <summary>Wire name to concrete op record type, keyed ordinally.</summary>
    public static IReadOnlyDictionary<string, Type> Registry { get; } =
        Entries.ToDictionary(e => e.Name, e => e.Type, StringComparer.Ordinal);

    /// <summary>Every op name in registration (documentation) order.</summary>
    public static IReadOnlyList<string> Names { get; } =
        Entries.Select(e => e.Name).ToArray();
}
