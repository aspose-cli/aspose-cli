using Aspose.Cli.Sdk.IO;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Applies one validated operation to an in-memory workbook: routes it to its mapper, which
/// does the work and returns the count of cells it touched where that is meaningful, and
/// launders Aspose.Cells exceptions into <see cref="EngineOpException"/>.
/// <see cref="CellsMutationService"/> runs the batch.
/// </summary>
internal sealed class CellsMutationHandlers : ICellsOpHandler<long?>
{
    private readonly Workbook _workbook;
    private readonly IReadOnlyDictionary<string, Secret>? _secrets;
    private readonly InputResourceScope _inputs;
    private readonly CellsImportSources _sources;

    internal CellsMutationHandlers(
        Workbook workbook,
        IReadOnlyDictionary<string, Secret>? secrets,
        InputResourceScope inputs,
        CellsImportSources sources)
    {
        _workbook = workbook;
        _secrets = secrets;
        _inputs = inputs;
        _sources = sources;
    }

    /// <summary>
    /// Applies one op, laundering the SDK's <see cref="CellsException"/> into the
    /// Core-visible <see cref="EngineOpException"/>; a mapper's own
    /// <c>CliException</c> propagates untouched for the runner to normalize.
    /// </summary>
    internal long? Run(CellsOp op)
    {
        try
        {
            if (ReadsComputedValues(op))
            {
                _workbook.CalculateFormula();
            }

            return op.Accept(this);
        }
        catch (CellsException ex)
        {
            throw new EngineOpException(ex.Message, ex);
        }
        finally { _inputs.ThrowIfFailed(); }
    }

    /// <summary>
    /// Ops whose result depends on formula results calculate the batch's earlier
    /// edits first, so ordering never depends on an explicit recalculation step.
    /// </summary>
    private static bool ReadsComputedValues(CellsOp op) => op switch
    {
        ResizeRowsOp resize => resize.Height is null,
        ResizeColumnsOp resize => resize.Width is null,
        SortRangeOp or RemoveDuplicatesOp or CreatePivotOp or RefreshPivotOp => true,
        _ => false,
    };

    /// <summary>True once an operation that names no sheet was applied to the active sheet.</summary>
    internal bool DefaultedToActiveSheet { get; private set; }

    private Worksheet Sheet(CellsOp op)
    {
        DefaultedToActiveSheet |= op.Sheet is null;
        return Sheets.Resolve(_workbook, op);
    }

    public long? Apply(AddCommentOp operation) => CommentOps.AddComment(Sheet(operation), operation);

    public long? Apply(AddConditionalFormatOp operation) => ConditionalFormatOps.AddConditionalFormat(Sheet(operation), operation);

    public long? Apply(AddSheetOp operation) => SheetOps.AddSheet(_workbook, operation);

    public long? Apply(AddSparklineOp operation) => SparklineOps.AddSparkline(_workbook, Sheet(operation), operation);

    public long? Apply(ClearConditionalFormatsOp operation) => ConditionalFormatOps.ClearConditionalFormats(Sheet(operation), operation);

    public long? Apply(ClearRangeOp operation) => CellOps.Clear(Sheet(operation), operation);

    public long? Apply(ClearValidationOp operation) => ValidationOps.ClearValidation(Sheet(operation), operation);

    public long? Apply(CopyRangeOp operation) => CellOps.Copy(Sheet(operation), operation);

    public long? Apply(CreateChartOp operation) => ChartPivotOps.CreateChart(Sheet(operation), operation);

    public long? Apply(CreatePivotOp operation) => ChartPivotOps.CreatePivot(Sheet(operation), operation);

    public long? Apply(CreateTableOp operation) => TableOps.CreateTable(Sheet(operation), operation);

    public long? Apply(DefineNameOp operation) => NameOps.DefineName(_workbook, operation);

    public long? Apply(DeleteChartOp operation) => ChartPivotOps.DeleteChart(Sheet(operation), operation);

    public long? Apply(DeleteColumnsOp operation) => RowColumnOps.DeleteColumns(Sheet(operation), operation);

    public long? Apply(DeleteCommentOp operation) => CommentOps.DeleteComment(Sheet(operation), operation);

    public long? Apply(DeleteNameOp operation) => NameOps.DeleteName(_workbook, operation);

    public long? Apply(DeleteRowsOp operation) => RowColumnOps.DeleteRows(Sheet(operation), operation);

    public long? Apply(DeleteSheetOp operation) => SheetOps.DeleteSheet(_workbook, Sheet(operation));

    public long? Apply(EditCommentOp operation) => CommentOps.EditComment(Sheet(operation), operation);

    public long? Apply(FormatRangeOp operation) => CellOps.Format(_workbook, Sheet(operation), operation);

    public long? Apply(FreezePanesOp operation) => SheetOps.Freeze(Sheet(operation), operation);

    public long? Apply(GroupColumnsOp operation) => OutlineOps.GroupColumns(Sheet(operation), operation);

    public long? Apply(GroupRowsOp operation) => OutlineOps.GroupRows(Sheet(operation), operation);

    public long? Apply(ImportRangeOp operation) => ImportOps.ImportRange(Sheet(operation), operation, _sources);

    // The operation's sheet names the source sheet, not a sheet of this workbook.
    public long? Apply(ImportSheetOp operation) => ImportOps.ImportSheet(_workbook, operation, _sources);

    public long? Apply(InsertColumnsOp operation) => RowColumnOps.InsertColumns(Sheet(operation), operation);

    public long? Apply(InsertImageOp operation) => ImageOps.InsertImage(Sheet(operation), operation, _inputs);

    public long? Apply(InsertRowsOp operation) => RowColumnOps.InsertRows(Sheet(operation), operation);

    public long? Apply(MergeCellsOp operation) => CellOps.Merge(Sheet(operation), operation.Range, merged: true);

    public long? Apply(MoveSheetOp operation) => SheetOps.MoveSheet(Sheet(operation), operation);

    public long? Apply(ProtectSheetOp operation) => ProtectOps.ProtectSheet(Sheet(operation), operation, _secrets);

    public long? Apply(ProtectWorkbookOp operation) => ProtectOps.ProtectWorkbook(_workbook, operation, _secrets);

    public long? Apply(RefreshPivotOp operation) => ChartPivotOps.RefreshPivot(Sheet(operation), operation);

    public long? Apply(RemoveDuplicatesOp operation) => DedupeOps.RemoveDuplicates(Sheet(operation), operation);

    public long? Apply(RemoveHyperlinkOp operation) => HyperlinkOps.RemoveHyperlink(Sheet(operation), operation);

    public long? Apply(RenameSheetOp operation) => SheetOps.RenameSheet(Sheet(operation), operation);

    public long? Apply(ResizeColumnsOp operation) => RowColumnOps.ResizeColumns(Sheet(operation), operation);

    public long? Apply(ResizeRowsOp operation) => RowColumnOps.ResizeRows(Sheet(operation), operation);

    public long? Apply(SetActiveSheetOp operation) => SheetOps.SetActiveSheet(_workbook, Sheet(operation));

    public long? Apply(SetAutoFilterOp operation) => SortFilterOps.SetAutoFilter(Sheet(operation), operation);

    public long? Apply(SetBordersOp operation) => BorderOps.SetBorders(_workbook, Sheet(operation), operation);

    // Workbook-scoped: the operation's sheet is deliberately not resolved.
    public long? Apply(SetDefaultFontOp operation) => LookOps.SetDefaultFont(_workbook, operation);

    /// <summary>The anchor cells set_formula wrote, which the batch checks for unknown functions.</summary>
    internal List<Cell> FormulaAnchors { get; } = [];

    public long? Apply(SetFormulaOp operation)
    {
        long written = CellOps.SetFormula(Sheet(operation), operation, out Cell anchor);
        FormulaAnchors.Add(anchor);
        return written;
    }

    public long? Apply(SetHyperlinkOp operation) => HyperlinkOps.SetHyperlink(Sheet(operation), operation);

    public long? Apply(SetPageSetupOp operation) => PageSetupOps.SetPageSetup(Sheet(operation), operation);

    public long? Apply(SetPrintAreaOp operation) => PageSetupOps.SetPrintArea(Sheet(operation), operation);

    public long? Apply(SetSheetViewOp operation) => LookOps.SetSheetView(Sheet(operation), operation);

    public long? Apply(SetSheetVisibilityOp operation) => SheetOps.SetVisibility(Sheet(operation), operation);

    public long? Apply(SetTabColorOp operation) => LookOps.SetTabColor(Sheet(operation), operation);

    public long? Apply(SetValidationOp operation) => ValidationOps.SetValidation(Sheet(operation), operation);

    public long? Apply(SetValuesOp operation) => CellOps.SetValues(Sheet(operation), operation);

    public long? Apply(SortRangeOp operation) => SortFilterOps.SortRange(_workbook, Sheet(operation), operation);

    public long? Apply(UngroupColumnsOp operation) => OutlineOps.UngroupColumns(Sheet(operation), operation);

    public long? Apply(UngroupRowsOp operation) => OutlineOps.UngroupRows(Sheet(operation), operation);

    public long? Apply(UnmergeCellsOp operation) => CellOps.Merge(Sheet(operation), operation.Range, merged: false);

    public long? Apply(UnprotectSheetOp operation) => ProtectOps.UnprotectSheet(Sheet(operation), operation, _secrets);

    public long? Apply(UnprotectWorkbookOp operation) => ProtectOps.UnprotectWorkbook(_workbook, operation, _secrets);

    public long? Apply(UpdateChartOp operation) => ChartPivotOps.UpdateChart(Sheet(operation), operation);
}
