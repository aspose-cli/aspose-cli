using Aspose.Cli.Sdk.IO;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Applies one validated operation to an in-memory workbook: routes it to its mapper, which
/// does the work, and launders Aspose.Cells exceptions into <see cref="EngineOpException"/>.
/// It counts nothing: the SDK runner compares the snapshots <see cref="CellsSnapshots"/> takes.
/// </summary>
internal sealed class CellsMutationHandlers : ICellsOpHandler<ValueTuple>
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
    /// engine-neutral <see cref="EngineOpException"/>; a mapper's own
    /// <c>CliException</c> propagates untouched for the runner to normalize.
    /// </summary>
    internal void Run(CellsOp op)
    {
        try
        {
            if (ReadsComputedValues(op))
            {
                _workbook.CalculateFormula();
            }

            op.Accept(this);
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
        ResizeColumnsOp resize => resize.WidthChars is null,
        SortRangeOp or DeleteDuplicatesOp or InsertPivotOp or RefreshPivotOp => true,
        _ => false,
    };

    /// <summary>True once an operation that names no sheet was applied to the active sheet.</summary>
    internal bool DefaultedToActiveSheet { get; private set; }

    private Worksheet Sheet(CellsSheetOp op)
    {
        DefaultedToActiveSheet |= op.Sheet is null;
        return Sheets.Resolve(_workbook, op);
    }

    public ValueTuple Apply(AddCommentOp operation)
    {
        CommentOps.AddComment(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(AddConditionalFormatOp operation)
    {
        ConditionalFormatOps.AddConditionalFormat(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(InsertSheetOp operation)
    {
        SheetOps.AddSheet(_workbook, operation);
        return default;
    }

    public ValueTuple Apply(AddSparklineOp operation)
    {
        SparklineOps.AddSparkline(_workbook, Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ClearAutoFilterOp operation)
    {
        SortFilterOps.ClearAutoFilter(Sheet(operation));
        return default;
    }

    public ValueTuple Apply(ClearPrintAreaOp operation)
    {
        PageSetupOps.ClearPrintArea(Sheet(operation));
        return default;
    }

    public ValueTuple Apply(ClearTabColorOp operation)
    {
        LookOps.ClearTabColor(Sheet(operation));
        return default;
    }

    public ValueTuple Apply(UnfreezePanesOp operation)
    {
        SheetOps.Unfreeze(Sheet(operation));
        return default;
    }

    public ValueTuple Apply(ClearConditionalFormatsOp operation)
    {
        ConditionalFormatOps.ClearConditionalFormats(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ClearRangeOp operation)
    {
        CellOps.Clear(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ClearValidationOp operation)
    {
        ValidationOps.ClearValidation(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(CopyRangeOp operation)
    {
        CellOps.Copy(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(InsertChartOp operation)
    {
        ChartPivotOps.CreateChart(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(InsertPivotOp operation)
    {
        ChartPivotOps.CreatePivot(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DefineTableOp operation)
    {
        TableOps.CreateTable(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DefineNameOp operation)
    {
        NameOps.DefineName(_workbook, operation);
        return default;
    }

    public ValueTuple Apply(DeleteChartOp operation)
    {
        ChartPivotOps.DeleteChart(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DeleteColumnsOp operation)
    {
        RowColumnOps.DeleteColumns(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DeleteCommentOp operation)
    {
        CommentOps.DeleteComment(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DeleteNameOp operation)
    {
        NameOps.DeleteName(_workbook, operation);
        return default;
    }

    public ValueTuple Apply(DeleteRowsOp operation)
    {
        RowColumnOps.DeleteRows(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DeleteSheetOp operation)
    {
        SheetOps.DeleteSheet(_workbook, Sheet(operation));
        return default;
    }

    public ValueTuple Apply(SetCommentOp operation)
    {
        CommentOps.EditComment(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(FormatRangeOp operation)
    {
        CellOps.Format(_workbook, Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(FreezePanesOp operation)
    {
        SheetOps.Freeze(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(GroupColumnsOp operation)
    {
        OutlineOps.GroupColumns(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(GroupRowsOp operation)
    {
        OutlineOps.GroupRows(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ImportRangeOp operation)
    {
        ImportOps.ImportRange(Sheet(operation), operation, _sources);
        return default;
    }

    public ValueTuple Apply(ImportSheetOp operation)
    {
        ImportOps.ImportSheet(_workbook, operation, _sources);
        return default;
    }

    public ValueTuple Apply(InsertColumnsOp operation)
    {
        RowColumnOps.InsertColumns(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(InsertImageOp operation)
    {
        ImageOps.InsertImage(Sheet(operation), operation, _inputs);
        return default;
    }

    public ValueTuple Apply(InsertRowsOp operation)
    {
        RowColumnOps.InsertRows(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(MergeCellsOp operation)
    {
        CellOps.Merge(Sheet(operation), operation.Range, merged: true);
        return default;
    }

    public ValueTuple Apply(MoveSheetOp operation)
    {
        SheetOps.MoveSheet(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ProtectSheetOp operation)
    {
        ProtectOps.ProtectSheet(Sheet(operation), operation, _secrets);
        return default;
    }

    public ValueTuple Apply(ProtectWorkbookOp operation)
    {
        ProtectOps.ProtectWorkbook(_workbook, operation, _secrets);
        return default;
    }

    public ValueTuple Apply(RefreshPivotOp operation)
    {
        ChartPivotOps.RefreshPivot(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DeleteDuplicatesOp operation)
    {
        DedupeOps.RemoveDuplicates(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(DeleteHyperlinkOp operation)
    {
        HyperlinkOps.RemoveHyperlink(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(RenameSheetOp operation)
    {
        SheetOps.RenameSheet(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ResizeColumnsOp operation)
    {
        RowColumnOps.ResizeColumns(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(ResizeRowsOp operation)
    {
        RowColumnOps.ResizeRows(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetActiveSheetOp operation)
    {
        SheetOps.SetActiveSheet(_workbook, Sheet(operation));
        return default;
    }

    public ValueTuple Apply(SetAutoFilterOp operation)
    {
        SortFilterOps.SetAutoFilter(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetBordersOp operation)
    {
        BorderOps.SetBorders(_workbook, Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetDefaultFontOp operation)
    {
        LookOps.SetDefaultFont(_workbook, operation);
        return default;
    }

    /// <summary>The anchor cells set_formula wrote, which the batch checks for unknown functions.</summary>
    internal List<Cell> FormulaAnchors { get; } = [];

    public ValueTuple Apply(SetFormulaOp operation)
    {
        CellOps.SetFormula(Sheet(operation), operation, out Cell anchor);
        FormulaAnchors.Add(anchor);
        return default;
    }

    public ValueTuple Apply(SetHyperlinkOp operation)
    {
        HyperlinkOps.SetHyperlink(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetPageSetupOp operation)
    {
        PageSetupOps.SetPageSetup(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetPrintAreaOp operation)
    {
        PageSetupOps.SetPrintArea(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetSheetViewOp operation)
    {
        LookOps.SetSheetView(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetSheetVisibilityOp operation)
    {
        SheetOps.SetVisibility(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetTabColorOp operation)
    {
        LookOps.SetTabColor(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetValidationOp operation)
    {
        ValidationOps.SetValidation(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SetValuesOp operation)
    {
        CellOps.SetValues(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(SortRangeOp operation)
    {
        SortFilterOps.SortRange(_workbook, Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(UngroupColumnsOp operation)
    {
        OutlineOps.UngroupColumns(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(UngroupRowsOp operation)
    {
        OutlineOps.UngroupRows(Sheet(operation), operation);
        return default;
    }

    public ValueTuple Apply(UnmergeCellsOp operation)
    {
        CellOps.Merge(Sheet(operation), operation.Range, merged: false);
        return default;
    }

    public ValueTuple Apply(UnprotectSheetOp operation)
    {
        ProtectOps.UnprotectSheet(Sheet(operation), operation, _secrets);
        return default;
    }

    public ValueTuple Apply(UnprotectWorkbookOp operation)
    {
        ProtectOps.UnprotectWorkbook(_workbook, operation, _secrets);
        return default;
    }

    public ValueTuple Apply(SetChartOp operation)
    {
        ChartPivotOps.UpdateChart(Sheet(operation), operation);
        return default;
    }
}
