using System.Text.Json;
using Aspose.Cli.Product.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Advanced = Aspose.Cli.Product.Cells.Operations.CellsAdvancedOpValidator;
using Core = Aspose.Cli.Product.Cells.Operations.CellsCoreOpValidator;
using static Aspose.Cli.Product.Cells.Operations.CellsValidationSupport;

namespace Aspose.Cli.Product.Cells.Operations;

/// <summary>
/// Parses and validates an ops document before anything touches the engine.
/// Structural problems (bad JSON, unknown ops, missing fields) and semantic
/// problems (bad ranges, ragged matrices, mismatched dimensions) all fail
/// here with <c>OPS_INVALID</c> and the offending op's index — engines only
/// ever receive batches that are internally consistent.
/// </summary>
internal static class OpsParser
{
    private const int MaximumOperations = 10_000;

    /// <summary>Parses an ops JSON document into a validated batch.</summary>
    /// <exception cref="CliException"><c>OPS_INVALID</c> with the failure reason.</exception>
    public static OpsBatch Parse(string json)
    {
        // An empty document is a user mistake (a truncated generation step, a
        // broken heredoc, an empty stdin), not a programmer error: guarding it
        // with ThrowIfNullOrEmpty escaped the taxonomy as INTERNAL.
        if (string.IsNullOrWhiteSpace(json))
        {
            throw CellsErrors.OpsInvalid("the ops document is empty");
        }

        OpsBatch batch;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            BoundedJsonValidation.ValidateNoDuplicateProperties(
                document.RootElement,
                static reason => new JsonException(reason));
            batch = ProductJsonContext.Definition.Deserialize<OpsBatch>(json);
        }
        catch (Exception ex) when (
            ex is JsonException
                or InvalidOperationException
                or NotSupportedException)
        {
            throw CellsErrors.OpsInvalid(ex.Message);
        }

        BoundedOperationValidation.ValidateEnvelope(
            batch,
            CellsSchemaIds.Ops,
            static reason => CellsErrors.OpsInvalid(reason));

        if (batch.Ops.Count is < 1 or > MaximumOperations)
        {
            throw CellsErrors.OpsInvalid(
                $"the ops array must contain 1-{MaximumOperations} operations");
        }

        return Prepare(batch);
    }

    /// <summary>Validates a composed batch and assigns stable missing ids.</summary>
    internal static OpsBatch Prepare(OpsBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Ops.Count is < 1 or > MaximumOperations)
        {
            throw CellsErrors.OpsInvalid(
                $"the ops array must contain 1-{MaximumOperations} operations");
        }

        IReadOnlyList<Op> identified = BoundedOperationIds.Assign(
            batch.Ops,
            static operation => operation.Id,
            static (operation, id) => operation with { Id = id },
            static reason => CellsErrors.OpsInvalid(reason));
        var validated = new Op[identified.Count];
        for (int index = 0; index < identified.Count; index++)
        {
            validated[index] = CellsOpValidator.Validate(
                identified[index],
                index);
        }

        return batch with { Ops = validated };
    }

}

/// <summary>Routes each operation to its cohesive validation family.</summary>
internal static class CellsOpValidator
{
    private static readonly IReadOnlyDictionary<Type, Func<Op, int, Op>> Validators =
        new Dictionary<Type, Func<Op, int, Op>>
        {
            [typeof(SetValuesOp)] = static (op, index) => Core.ValidateSetValues((SetValuesOp)op, index),
            [typeof(SetFormulaOp)] = static (op, index) => Core.ValidateSetFormula((SetFormulaOp)op, index),
            [typeof(ClearRangeOp)] = static (op, index) => Core.ValidateClear((ClearRangeOp)op, index),
            [typeof(CopyRangeOp)] = static (op, index) => Core.ValidateCopy((CopyRangeOp)op, index),
            [typeof(FormatRangeOp)] = static (op, index) => Core.ValidateFormat((FormatRangeOp)op, index),
            [typeof(MergeCellsOp)] = static (op, index) => Core.ValidateRange((MergeCellsOp)op, ((MergeCellsOp)op).Range, index),
            [typeof(UnmergeCellsOp)] = static (op, index) => Core.ValidateRange((UnmergeCellsOp)op, ((UnmergeCellsOp)op).Range, index),
            [typeof(InsertRowsOp)] = static (op, index) => Core.ValidateRow((InsertRowsOp)op, ((InsertRowsOp)op).At, ((InsertRowsOp)op).Count, index),
            [typeof(DeleteRowsOp)] = static (op, index) => Core.ValidateRow((DeleteRowsOp)op, ((DeleteRowsOp)op).At, ((DeleteRowsOp)op).Count, index),
            [typeof(InsertColumnsOp)] = static (op, index) => Core.ValidateColumn((InsertColumnsOp)op, ((InsertColumnsOp)op).At, ((InsertColumnsOp)op).Count, index),
            [typeof(DeleteColumnsOp)] = static (op, index) => Core.ValidateColumn((DeleteColumnsOp)op, ((DeleteColumnsOp)op).At, ((DeleteColumnsOp)op).Count, index),
            [typeof(ResizeRowsOp)] = static (op, index) => Core.ValidateResizeRows((ResizeRowsOp)op, index),
            [typeof(ResizeColumnsOp)] = static (op, index) => Core.ValidateResizeColumns((ResizeColumnsOp)op, index),
            [typeof(AddSheetOp)] = static (op, index) => Core.ValidateAddSheet((AddSheetOp)op, index),
            [typeof(RenameSheetOp)] = static (op, index) => Core.ValidateRenameSheet((RenameSheetOp)op, index),
            [typeof(DeleteSheetOp)] = static (op, index) => Core.ValidateNamedSheet((DeleteSheetOp)op, index),
            [typeof(SetSheetVisibilityOp)] = static (op, index) => Core.ValidateNamedSheet((SetSheetVisibilityOp)op, index),
            [typeof(MoveSheetOp)] = static (op, index) => Core.ValidateMoveSheet((MoveSheetOp)op, index),
            [typeof(FreezePanesOp)] = static (op, index) => Core.ValidateFreeze((FreezePanesOp)op, index),
            [typeof(CreateChartOp)] = static (op, index) => Core.ValidateChart((CreateChartOp)op, index),
            [typeof(CreatePivotOp)] = static (op, index) => Core.ValidatePivot((CreatePivotOp)op, index),
            [typeof(SetPageSetupOp)] = static (op, index) => Core.ValidatePageSetup((SetPageSetupOp)op, index),
            [typeof(SetPrintAreaOp)] = static (op, index) => Core.ValidatePrintArea((SetPrintAreaOp)op, index),
            [typeof(InsertImageOp)] = static (op, index) => Core.ValidateInsertImage((InsertImageOp)op, index),
            [typeof(RefreshPivotOp)] = static (op, _) => op,
            [typeof(CreateTableOp)] = static (op, index) => Core.ValidateRange((CreateTableOp)op, ((CreateTableOp)op).Range, index),
            [typeof(SetAutoFilterOp)] = static (op, index) => Core.ValidateAutoFilter((SetAutoFilterOp)op, index),
            [typeof(SortRangeOp)] = static (op, index) => Core.ValidateSort((SortRangeOp)op, index),
            [typeof(SetValidationOp)] = static (op, index) => Core.ValidateValidation((SetValidationOp)op, index),
            [typeof(DefineNameOp)] = static (op, index) => Core.ValidateDefineName((DefineNameOp)op, index),
            [typeof(DeleteNameOp)] = static (op, index) => Core.ValidateName((DeleteNameOp)op, ((DeleteNameOp)op).Name, index),
            [typeof(AddCommentOp)] = static (op, index) => Core.ValidateComment((AddCommentOp)op, ((AddCommentOp)op).Cell, ((AddCommentOp)op).Text, index),
            [typeof(EditCommentOp)] = static (op, index) => Core.ValidateComment((EditCommentOp)op, ((EditCommentOp)op).Cell, ((EditCommentOp)op).Text, index),
            [typeof(DeleteCommentOp)] = static (op, index) => Core.ValidateCommentCell((DeleteCommentOp)op, ((DeleteCommentOp)op).Cell, index),
            [typeof(ProtectSheetOp)] = static (op, index) => Core.ValidateProtect((ProtectSheetOp)op, index),
            [typeof(UnprotectSheetOp)] = static (op, _) => op,
            [typeof(GroupRowsOp)] = static (op, index) => Core.ValidateRowSpan((GroupRowsOp)op, ((GroupRowsOp)op).From, ((GroupRowsOp)op).To, index),
            [typeof(UngroupRowsOp)] = static (op, index) => Core.ValidateRowSpan((UngroupRowsOp)op, ((UngroupRowsOp)op).From, ((UngroupRowsOp)op).To, index),
            [typeof(GroupColumnsOp)] = static (op, index) => Core.ValidateColumnSpan((GroupColumnsOp)op, ((GroupColumnsOp)op).From, ((GroupColumnsOp)op).To, index),
            [typeof(UngroupColumnsOp)] = static (op, index) => Core.ValidateColumnSpan((UngroupColumnsOp)op, ((UngroupColumnsOp)op).From, ((UngroupColumnsOp)op).To, index),
            [typeof(ClearValidationOp)] = static (op, index) => Core.ValidateRange((ClearValidationOp)op, ((ClearValidationOp)op).Range, index),
            [typeof(RemoveDuplicatesOp)] = static (op, index) => Core.ValidateRemoveDuplicates((RemoveDuplicatesOp)op, index),
            [typeof(ProtectWorkbookOp)] = static (op, _) => op,
            [typeof(UnprotectWorkbookOp)] = static (op, _) => op,
            [typeof(SetHyperlinkOp)] = static (op, index) => Core.ValidateSetHyperlink((SetHyperlinkOp)op, index),
            [typeof(RemoveHyperlinkOp)] = static (op, index) => Core.ValidateCommentCell((RemoveHyperlinkOp)op, ((RemoveHyperlinkOp)op).Cell, index),
            [typeof(UpdateChartOp)] = static (op, index) => Advanced.ValidateUpdateChart((UpdateChartOp)op, index),
            [typeof(AddConditionalFormatOp)] = static (op, index) => Advanced.ValidateAddConditionalFormat((AddConditionalFormatOp)op, index),
            [typeof(ClearConditionalFormatsOp)] = static (op, index) => Core.ValidateRange((ClearConditionalFormatsOp)op, ((ClearConditionalFormatsOp)op).Range, index),
            [typeof(SetBordersOp)] = static (op, index) => Advanced.ValidateSetBorders((SetBordersOp)op, index),
            [typeof(SetDefaultFontOp)] = static (op, index) => Advanced.ValidateSetDefaultFont((SetDefaultFontOp)op, index),
            [typeof(SetTabColorOp)] = static (op, index) => Advanced.ValidateSetTabColor((SetTabColorOp)op, index),
            [typeof(SetSheetViewOp)] = static (op, index) => Advanced.ValidateSetSheetView((SetSheetViewOp)op, index),
            [typeof(DeleteChartOp)] = static (op, index) => Advanced.ValidateDeleteChart((DeleteChartOp)op, index),
            [typeof(AddSparklineOp)] = static (op, index) => Advanced.ValidateAddSparkline((AddSparklineOp)op, index),
            [typeof(SetActiveSheetOp)] = static (op, index) => Core.ValidateNamedSheet((SetActiveSheetOp)op, index),
            [typeof(RecalculateOp)] = static (op, index) => ValidateRecalculate((RecalculateOp)op, index),
        };

    internal static Op Validate(Op op, int index)
    {
        try
        {
            if (Validators.TryGetValue(op.GetType(), out Func<Op, int, Op>? validator))
            {
                return validator(op, index);
            }

            throw CellsErrors.OpsInvalidAt(index, op.OpName, "the op is not supported by this build");
        }
        catch (CliException ex) when (ex.Code == CellsDiagnostics.RangeInvalid)
        {
            throw CellsErrors.OpsInvalidAt(index, op.OpName, ex.Message);
        }
    }

    private static RecalculateOp ValidateRecalculate(RecalculateOp op, int index)
    {
        Require(op.Sheet is null, index, op, "'sheet' is not valid for a workbook recalculation");
        return op;
    }
}

internal static class CellsCoreOpValidator
{
    internal static SetFormulaOp ValidateSetFormula(SetFormulaOp op, int index) =>
        ValidateRangeOf(op, op.Range, index,
            () => Require(op.Formula.StartsWith('='), index, op, "the formula must start with '='"));

    internal static ClearRangeOp ValidateClear(ClearRangeOp op, int index) =>
        ValidateRangeOf(op, op.Range, index,
            () => Require(
                op.What is null || ClearTargets.All.Contains(op.What),
                index,
                op,
                $"'what' must be one of: {string.Join(", ", ClearTargets.All)}"));

    internal static T ValidateRange<T>(T op, string range, int index) where T : Op =>
        ValidateRangeOf(op, range, index);

    internal static T ValidateRow<T>(T op, int at, int? count, int index) where T : Op =>
        ValidateRowOp(op, at, count, index);

    internal static T ValidateColumn<T>(T op, string at, int? count, int index) where T : Op =>
        ValidateColumnOp(op, at, count, index);

    internal static T ValidateNamedSheet<T>(T op, int index) where T : Op =>
        ValidateSheetIsNamed(op, index);

    internal static T ValidateName<T>(T op, string name, int index) where T : Op =>
        ValidateNamed(op, name, index);

    internal static SetValuesOp ValidateSetValues(SetValuesOp op, int index)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range, index, op);
        RequirePresent(op.Values, "values", index, op);
        IReadOnlyList<IReadOnlyList<object?>> values = JsonValueMatrix.Normalize(
            op.Values, reason => CellsErrors.OpsInvalidAt(index, op.OpName, reason));

        bool isAnchor = range.CellCount == 1;
        if (!isAnchor && (range.RowCount != values.Count || range.ColumnCount != values[0].Count))
        {
            throw CellsErrors.OpsInvalidAt(index, op.OpName,
                $"the range spans {range.RowCount}x{range.ColumnCount} but the matrix is {values.Count}x{values[0].Count}",
                "Use a single anchor cell (e.g. \"B2\") to write a matrix of any size, or make the range match the matrix.");
        }

        return op with { Values = values };
    }

    internal static CopyRangeOp ValidateCopy(CopyRangeOp op, int index)
    {
        // From/To may be sheet-qualified for cross-sheet copies.
        RequirePresent(op.From, "from", index, op);
        RequirePresent(op.To, "to", index, op);
        _ = A1.ParseRange(op.From);
        RangeSpec to = A1.ParseRange(op.To);
        if (to.Range.CellCount != 1)
        {
            throw CellsErrors.OpsInvalidAt(index, op.OpName,
                "'to' must be a single anchor cell, e.g. \"Summary!A1\"");
        }

        return op;
    }

    internal static FormatRangeOp ValidateFormat(FormatRangeOp op, int index)
    {
        _ = ParseUnqualifiedRange(op.Range, index, op);
        StyleData style = op.Style;

        Require(style != new StyleData(), index, op, "the style has no fields set");
        RequireColor(style.Color, "color", index, op);
        RequireColor(style.Bg, "bg", index, op);
        Require(style.HAlign is null || HorizontalAlignments.All.Contains(style.HAlign), index, op,
            $"'hAlign' must be one of: {string.Join(", ", HorizontalAlignments.All)}");
        Require(style.VAlign is null || VerticalAlignments.All.Contains(style.VAlign), index, op,
            $"'vAlign' must be one of: {string.Join(", ", VerticalAlignments.All)}");
        Require(style.Size is null or > 0, index, op, "'size' must be positive");
        Require(style.Indent is null or (>= 0 and <= 250), index, op, "'indent' must be between 0 and 250");

        return op;
    }

    internal static ResizeRowsOp ValidateResizeRows(ResizeRowsOp op, int index)
    {
        Require(op.From >= 1, index, op, "'from' is a 1-based row number");
        Require(op.To is null || op.To >= op.From, index, op, "'to' must not be smaller than 'from'");
        Require(op.Height is null or >= 0, index, op, "'height' must not be negative");
        return op;
    }

    internal static ResizeColumnsOp ValidateResizeColumns(ResizeColumnsOp op, int index)
    {
        int from = ParseColumn(op.From);
        if (op.To is { } toLetters)
        {
            Require(ParseColumn(toLetters) >= from, index, op,
                "'to' must not be left of 'from'");
        }

        Require(op.Width is null or >= 0, index, op, "'width' must not be negative");
        return op;
    }

    internal static FreezePanesOp ValidateFreeze(FreezePanesOp op, int index)
    {
        RequirePresent(op.Cell, "cell", index, op);
        _ = A1.ParseCell(op.Cell);
        return op;
    }

    internal static CreateChartOp ValidateChart(CreateChartOp op, int index)
    {
        Require(ChartTypes.All.Contains(op.Type), index, op,
            $"'type' must be one of: {string.Join(", ", ChartTypes.All)}");
        // dataRange may be sheet-qualified so a chart on one sheet can plot
        // another's data (as create_pivot's sourceRange already allows); the
        // engine's SetChartDataRange resolves the qualified reference. Placement
        // ('at') stays on the op's sheet.
        RequirePresent(op.DataRange, "dataRange", index, op);
        _ = A1.ParseRange(op.DataRange);

        RangeRef placement = ParseUnqualifiedRange(op.At, index, op);
        Require(placement.CellCount > 1, index, op,
            "'at' must be a range the chart is placed over, e.g. \"E2:L18\"");

        Advanced.ValidateChartCosmetics(op, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels, index);
        // The type is known here, so the pie/axis mismatch fails before any
        // file is opened; update_chart only learns the real type in the
        // engine, which enforces the same rule there.
        Require(op.Type != ChartTypes.Pie || op.AxisTitles is null, index, op,
            "a 'pie' chart has no axes; omit 'axisTitles'");
        return op;
    }

    internal static CreatePivotOp ValidatePivot(CreatePivotOp op, int index)
    {
        RequirePresent(op.SourceRange, "sourceRange", index, op);
        RequirePresent(op.At, "at", index, op);
        _ = A1.ParseRange(op.SourceRange); // may be sheet-qualified
        RangeSpec at = A1.ParseRange(op.At);
        Require(at.SheetName is null && at.Range.CellCount == 1, index, op,
            "'at' must be a single unqualified anchor cell on the op's sheet, e.g. \"F3\"");

        Require(op.Values.Count > 0, index, op, "'values' must name at least one field to aggregate");
        foreach (PivotValueField value in op.Values)
        {
            Require(!string.IsNullOrWhiteSpace(value.Field), index, op, "a value field name is empty");
            Require(value.Function is null || PivotFunctions.All.Contains(value.Function), index, op,
                $"'function' must be one of: {string.Join(", ", PivotFunctions.All)}");
            // The engine accepts arbitrary format codes, so presence is the
            // only thing to check.
            Require(value.NumberFormat is null || !string.IsNullOrWhiteSpace(value.NumberFormat), index, op,
                "'numberFormat' must not be empty; omit it instead");
        }

        Require(op.Rows is not { Count: 0 }, index, op, "'rows' must not be an empty array; omit it instead");
        Require(op.Columns is not { Count: 0 }, index, op, "'columns' must not be an empty array; omit it instead");
        return op;
    }

    internal static SetPageSetupOp ValidatePageSetup(SetPageSetupOp op, int index)
    {
        bool any = op.Orientation is not null || op.PaperSize is not null || op.FitToWidth is not null
            || op.FitToHeight is not null || op.Scale is not null || op.Margins is not null
            || op.Header is not null || op.Footer is not null;
        Require(any, index, op, "set_page_setup needs at least one field to change");

        Require(op.Orientation is null || PageOrientations.All.Contains(op.Orientation), index, op,
            $"'orientation' must be one of: {string.Join(", ", PageOrientations.All)}");
        Require(op.PaperSize is null || PaperSizes.All.Contains(op.PaperSize), index, op,
            $"'paperSize' must be one of: {string.Join(", ", PaperSizes.All)}");
        Require(op.FitToWidth is null or >= 0, index, op, "'fitToWidth' must not be negative (0 = automatic)");
        Require(op.FitToHeight is null or >= 0, index, op, "'fitToHeight' must not be negative (0 = automatic)");
        Require(op.Scale is null or (>= 10 and <= 400), index, op, "'scale' must be between 10 and 400");

        if (op.Margins is { } margins)
        {
            bool anyMargin = margins.Top is not null || margins.Bottom is not null || margins.Left is not null
                || margins.Right is not null || margins.Header is not null || margins.Footer is not null;
            Require(anyMargin, index, op, "'margins' has no sides set; omit it instead");
            Require(
                (margins.Top is null or >= 0) && (margins.Bottom is null or >= 0) && (margins.Left is null or >= 0)
                    && (margins.Right is null or >= 0) && (margins.Header is null or >= 0) && (margins.Footer is null or >= 0),
                index, op, "margins must not be negative");
        }

        return op;
    }

    internal static SetPrintAreaOp ValidatePrintArea(SetPrintAreaOp op, int index)
    {
        if (op.Range is { } range)
        {
            _ = ParseUnqualifiedRange(range, index, op);
        }

        return op;
    }

    internal static InsertImageOp ValidateInsertImage(InsertImageOp op, int index)
    {
        Require(!string.IsNullOrWhiteSpace(op.Path), index, op, "the image 'path' must not be empty");
        RangeRef at = ParseUnqualifiedRange(op.At, index, op);
        Require(at.CellCount == 1, index, op, "'at' must be a single anchor cell, e.g. \"E2\"");
        Require(op.Width is null or >= 1, index, op, "'width' must be a positive pixel count");
        Require(op.Height is null or >= 1, index, op, "'height' must be a positive pixel count");
        return op;
    }

    internal static SetAutoFilterOp ValidateAutoFilter(SetAutoFilterOp op, int index)
    {
        if (op.Off is true)
        {
            return op;
        }

        Require(op.Range is not null, index, op, "'range' is required unless 'off' is true");
        _ = ParseUnqualifiedRange(op.Range!, index, op);
        return op;
    }

    internal static SortRangeOp ValidateSort(SortRangeOp op, int index)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range, index, op);
        Require(op.By.Count > 0, index, op, "'by' must name at least one column to sort by");
        foreach (SortKey key in op.By)
        {
            // The engine sorts by an absolute column; one outside the sorted
            // range is silently a no-op (the caller's sort intent vanishes with
            // no error). Reject it up front, as remove_duplicates already does.
            int absolute = ParseColumn(key.Column);
            Require(absolute >= range.Start.Column && absolute <= range.End.Column, index, op,
                $"sort column '{key.Column}' is outside the range '{op.Range}'");
            Require(key.Order is null || SortOrders.All.Contains(key.Order), index, op,
                $"'order' must be one of: {string.Join(", ", SortOrders.All)}");
        }

        return op;
    }

    internal static DefineNameOp ValidateDefineName(DefineNameOp op, int index)
    {
        Require(!string.IsNullOrWhiteSpace(op.Name), index, op, "the defined name must not be empty");
        Require(!string.IsNullOrWhiteSpace(op.RefersTo), index, op, "'refersTo' must not be empty");
        return op;
    }

    internal static T ValidateComment<T>(T op, string cell, string text, int index)
        where T : Op
    {
        RangeRef at = ParseUnqualifiedRange(cell, index, op);
        Require(at.CellCount == 1, index, op, "'cell' must be a single cell, e.g. \"B2\"");
        Require(!string.IsNullOrEmpty(text), index, op, "'text' must not be empty");
        return op;
    }

    internal static T ValidateCommentCell<T>(T op, string cell, int index)
        where T : Op
    {
        RangeRef at = ParseUnqualifiedRange(cell, index, op);
        Require(at.CellCount == 1, index, op, "'cell' must be a single cell, e.g. \"B2\"");
        return op;
    }

    internal static ProtectSheetOp ValidateProtect(ProtectSheetOp op, int index)
    {
        foreach (string action in op.Allow ?? [])
        {
            Require(ProtectActions.All.Contains(action), index, op,
                $"'allow' items must be one of: {string.Join(", ", ProtectActions.All)}");
        }

        return op;
    }

    private static T ValidateNamed<T>(T op, string name, int index)
        where T : Op
    {
        Require(!string.IsNullOrWhiteSpace(name), index, op, "the defined name must not be empty");
        return op;
    }

    internal static SetValidationOp ValidateValidation(SetValidationOp op, int index)
    {
        _ = ParseUnqualifiedRange(op.Range, index, op);
        Require(ValidationTypes.All.Contains(op.Type), index, op,
            $"'type' must be one of: {string.Join(", ", ValidationTypes.All)}");

        if (op.Type == ValidationTypes.List)
        {
            bool hasItems = op.ListItems is { Count: > 0 };
            bool hasSource = !string.IsNullOrWhiteSpace(op.ListSource);
            Require(hasItems ^ hasSource, index, op,
                "a 'list' validation needs exactly one of 'listItems' or 'listSource'");
        }
        else if (op.Type == ValidationTypes.Custom)
        {
            Require(!string.IsNullOrWhiteSpace(op.Value1), index, op,
                "a 'custom' validation needs 'value1' set to a formula");
        }
        else
        {
            Require(op.Operator is not null && ValidationOperators.All.Contains(op.Operator), index, op,
                $"'operator' is required and must be one of: {string.Join(", ", ValidationOperators.All)}");
            Require(!string.IsNullOrWhiteSpace(op.Value1), index, op, "'value1' is required");
            bool between = op.Operator is ValidationOperators.Between or ValidationOperators.NotBetween;
            Require(!between || !string.IsNullOrWhiteSpace(op.Value2), index, op,
                "'between'/'notBetween' need 'value2' as the upper bound");
        }

        return op;
    }

    internal static AddSheetOp ValidateAddSheet(AddSheetOp op, int index)
    {
        Require(!string.IsNullOrWhiteSpace(op.Name), index, op, "the sheet name must not be empty");
        Require(op.Position is null or >= 0, index, op, "'position' is zero-based and must not be negative");
        return op;
    }

    internal static RenameSheetOp ValidateRenameSheet(RenameSheetOp op, int index)
    {
        Require(!string.IsNullOrWhiteSpace(op.To), index, op, "the new sheet name must not be empty");
        return op;
    }

    private static T ValidateSheetIsNamed<T>(T op, int index)
        where T : Op
    {
        Require(op.Sheet is not null, index, op, "'sheet' is required: name the target sheet explicitly");
        return op;
    }

    internal static MoveSheetOp ValidateMoveSheet(MoveSheetOp op, int index)
    {
        Require(op.Sheet is not null, index, op, "'sheet' is required: name the sheet to move explicitly");
        Require(op.Position >= 0, index, op, "'position' is a zero-based destination index and must not be negative");
        return op;
    }

    private static T ValidateRangeOf<T>(T op, string range, int index, Action? extra = null)
        where T : Op
    {
        _ = ParseUnqualifiedRange(range, index, op);
        extra?.Invoke();
        return op;
    }

    private static T ValidateRowOp<T>(T op, int at, int? count, int index)
        where T : Op
    {
        Require(at >= 1, index, op, "'at' is a 1-based row number");
        Require(count is null or >= 1, index, op, "'count' must be at least 1");
        return op;
    }

    private static T ValidateColumnOp<T>(T op, string at, int? count, int index)
        where T : Op
    {
        _ = ParseColumn(at);
        Require(count is null or >= 1, index, op, "'count' must be at least 1");
        return op;
    }

    internal static T ValidateRowSpan<T>(T op, int from, int? to, int index)
        where T : Op
    {
        Require(from >= 1, index, op, "'from' is a 1-based row number");
        Require(to is null || to >= from, index, op, "'to' must not be smaller than 'from'");
        return op;
    }

    internal static T ValidateColumnSpan<T>(T op, string from, string? to, int index)
        where T : Op
    {
        int first = ParseColumn(from);
        if (to is { } toLetters)
        {
            Require(ParseColumn(toLetters) >= first, index, op, "'to' must not be left of 'from'");
        }

        return op;
    }

    internal static RemoveDuplicatesOp ValidateRemoveDuplicates(RemoveDuplicatesOp op, int index)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range, index, op);
        if (op.Columns is { } columns)
        {
            Require(columns.Count > 0, index, op, "'columns' must not be an empty array; omit it instead");
            foreach (string column in columns)
            {
                int absolute = ParseColumn(column);
                Require(absolute >= range.Start.Column && absolute <= range.End.Column, index, op,
                    $"column '{column}' is outside the range '{op.Range}'");
            }
        }

        return op;
    }

    internal static SetHyperlinkOp ValidateSetHyperlink(SetHyperlinkOp op, int index)
    {
        RangeRef at = ParseUnqualifiedRange(op.Cell, index, op);
        Require(at.CellCount == 1, index, op, "'cell' must be a single cell, e.g. \"B2\"");

        bool hasUrl = !string.IsNullOrWhiteSpace(op.Url);
        bool hasTarget = !string.IsNullOrWhiteSpace(op.Target);
        Require(hasUrl ^ hasTarget, index, op, "give exactly one of 'url' or 'target'");
        return op;
    }

}

internal static class CellsAdvancedOpValidator
{
    internal static UpdateChartOp ValidateUpdateChart(UpdateChartOp op, int index)
    {
        bool hasIndex = op.Index is not null;
        bool hasName = !string.IsNullOrWhiteSpace(op.Name);
        Require(hasIndex ^ hasName, index, op, "identify the chart by exactly one of 'index' or 'name'");
        Require(op.Index is null or >= 0, index, op, "'index' is zero-based and must not be negative");

        bool anyChange = op.Title is not null || op.DataRange is not null
            || op.Type is not null || op.SeriesInRows is not null
            || op.Legend is not null || op.AxisTitles is not null
            || op.SeriesColors is not null || op.DataLabels is not null;
        Require(anyChange, index, op,
            "update_chart needs at least one field to change (title, dataRange, type, seriesInRows, "
            + "legend, axisTitles, seriesColors or dataLabels)");

        Require(op.Type is null || ChartTypes.All.Contains(op.Type), index, op,
            $"'type' must be one of: {string.Join(", ", ChartTypes.All)}");
        if (op.DataRange is { } dataRange)
        {
            // As with create_chart, a replacement data range may be sheet-qualified.
            _ = A1.ParseRange(dataRange);
        }

        ValidateChartCosmetics(op, op.Legend, op.AxisTitles, op.SeriesColors, op.DataLabels, index);
        // Pie + axisTitles is only statically known when the op also sets the
        // type; identifying an existing pie is the engine mapper's job.
        Require(op.Type != ChartTypes.Pie || op.AxisTitles is null, index, op,
            "a 'pie' chart has no axes; omit 'axisTitles'");
        return op;
    }

    internal static DeleteChartOp ValidateDeleteChart(DeleteChartOp op, int index)
    {
        bool hasIndex = op.Index is not null;
        bool hasName = !string.IsNullOrWhiteSpace(op.Name);
        Require(hasIndex ^ hasName, index, op, "identify the chart by exactly one of 'index' or 'name'");
        Require(op.Index is null or >= 0, index, op, "'index' is zero-based and must not be negative");
        return op;
    }

    /// <summary>
    /// The cosmetic fields shared by <c>create_chart</c> and
    /// <c>update_chart</c>. An empty object or empty array is rejected the way
    /// <c>margins</c> is — the caller meant something and got nothing.
    /// </summary>
    internal static void ValidateChartCosmetics(
        Op op,
        ChartLegendData? legend,
        ChartAxisTitlesData? axisTitles,
        IReadOnlyList<string>? seriesColors,
        ChartDataLabelsData? dataLabels,
        int index)
    {
        if (legend is { } chartLegend)
        {
            Require(chartLegend.Visible is not null || chartLegend.Position is not null, index, op,
                "'legend' has no fields set; omit it instead");
            Require(chartLegend.Position is null || LegendPositions.All.Contains(chartLegend.Position), index, op,
                $"'legend.position' must be one of: {string.Join(", ", LegendPositions.All)}");
        }

        if (axisTitles is { } titles)
        {
            Require(titles.Category is not null || titles.Value is not null, index, op,
                "'axisTitles' has no fields set; omit it instead");
        }

        if (seriesColors is { } colors)
        {
            Require(colors.Count > 0, index, op,
                "'seriesColors' must not be an empty array; omit it instead");
            foreach (string color in colors)
            {
                Require(color is not null, index, op, "'seriesColors' items must not be null");
                RequireColor(color, "seriesColors", index, op);
            }
        }

        if (dataLabels is { } labels)
        {
            Require(labels.Visible is not null || labels.Format is not null, index, op,
                "'dataLabels' has no fields set; omit it instead");
        }
    }

    internal static AddConditionalFormatOp ValidateAddConditionalFormat(AddConditionalFormatOp op, int index)
    {
        _ = ParseUnqualifiedRange(op.Range, index, op);
        ConditionalRule rule = op.Rule;
        Require(ConditionalRuleKinds.All.Contains(rule.Kind), index, op,
            $"'rule.kind' must be one of: {string.Join(", ", ConditionalRuleKinds.All)}");

        switch (rule.Kind)
        {
            case ConditionalRuleKinds.CellValue:
                Require(rule.Operator is not null && ValidationOperators.All.Contains(rule.Operator), index, op,
                    $"a 'cellValue' rule needs 'operator', one of: {string.Join(", ", ValidationOperators.All)}");
                Require(!string.IsNullOrWhiteSpace(rule.Value1), index, op, "a 'cellValue' rule needs 'value1'");
                bool between = rule.Operator is ValidationOperators.Between or ValidationOperators.NotBetween;
                Require(!between || !string.IsNullOrWhiteSpace(rule.Value2), index, op,
                    "'between'/'notBetween' need 'value2'");
                Require(op.Style is not null, index, op, "a 'cellValue' rule needs a 'style' to apply");
                break;
            case ConditionalRuleKinds.ColorScale:
                RequireColor(rule.MinColor, "rule.minColor", index, op);
                RequireColor(rule.MidColor, "rule.midColor", index, op);
                RequireColor(rule.MaxColor, "rule.maxColor", index, op);
                Require(rule.MinColor is not null && rule.MaxColor is not null, index, op,
                    "a 'colorScale' rule needs 'minColor' and 'maxColor' (add 'midColor' for a 3-point scale)");
                break;
            case ConditionalRuleKinds.DataBar:
                RequireColor(rule.BarColor, "rule.barColor", index, op);
                Require(rule.BarColor is not null, index, op, "a 'dataBar' rule needs 'barColor'");
                break;
            case ConditionalRuleKinds.Duplicates:
                Require(op.Style is not null, index, op, "a 'duplicates' rule needs a 'style' to apply");
                break;
            case ConditionalRuleKinds.Formula:
                Require(!string.IsNullOrWhiteSpace(rule.Value1), index, op,
                    "a 'formula' rule needs 'value1' set to a formula",
                    "The formula anchors at the range's top-left cell and shifts per cell; anchor the tested "
                    + "column with '$' to highlight whole rows, e.g. =$F2=\"OVERDUE\" over A2:F100.");
                Require(op.Style is not null, index, op, "a 'formula' rule needs a 'style' to apply");
                // The engine wraps a formula without the leading '=' into a
                // string literal that never matches — silent zero matches
                // (probe-verified). Normalize instead of rejecting, exactly as
                // set_validation's listSource and define_name's refersTo do.
                if (!rule.Value1!.StartsWith('='))
                {
                    op = op with { Rule = rule with { Value1 = "=" + rule.Value1 } };
                }

                break;
            case ConditionalRuleKinds.TopBottom:
                Require(
                    rule.Rank is >= 1 and <= 1000 && (rule.Percent is not true || rule.Rank <= 100), index, op,
                    "a 'topBottom' rule needs 'rank' (1-1000; 1-100 with 'percent')");
                Require(op.Style is not null, index, op, "a 'topBottom' rule needs a 'style' to apply");
                break;
            case ConditionalRuleKinds.IconSet:
                Require(rule.IconSet is not null && IconSetNames.All.Contains(rule.IconSet), index, op,
                    $"an 'iconSet' rule needs 'iconSet', one of: {string.Join(", ", IconSetNames.All)}");
                // Icons come from the set; a dxf style would be dead weight the
                // caller believes took effect — reject rather than ignore.
                Require(op.Style is null, index, op, "an 'iconSet' rule draws icons; omit 'style'");
                break;
        }

        if (op.Style is { } style)
        {
            RequireColor(style.Color, "style.color", index, op);
            RequireColor(style.Bg, "style.bg", index, op);
        }

        return op;
    }

    internal static AddSparklineOp ValidateAddSparkline(AddSparklineOp op, int index)
    {
        // The data range may be sheet-qualified (as create_chart's dataRange):
        // KPI sparklines on a dashboard sheet plot a data sheet. The location
        // stays on the op's sheet.
        RequirePresent(op.DataRange, "dataRange", index, op);
        _ = A1.ParseRange(op.DataRange);

        RequirePresent(op.Location, "location", index, op);
        RangeRef location = ParseUnqualifiedRange(op.Location, index, op);
        Require(location.RowCount == 1 || location.ColumnCount == 1, index, op,
            "'location' must be a single cell or a one-row/one-column range, e.g. \"F2\" or \"F2:F10\"");

        Require(op.Type is null || SparklineTypes.All.Contains(op.Type), index, op,
            $"'type' must be one of: {string.Join(", ", SparklineTypes.All)}");
        RequireColor(op.Color, "color", index, op);
        return op;
    }

    internal static SetBordersOp ValidateSetBorders(SetBordersOp op, int index)
    {
        RangeRef range = ParseUnqualifiedRange(op.Range, index, op);
        RequirePresent(op.Edges, "edges", index, op);
        Require(op.Edges.Count > 0, index, op,
            "'edges' must not be empty; use [\"all\"] for a full grid");
        foreach (string edge in op.Edges)
        {
            Require(edge is not null && BorderEdges.All.Contains(edge), index, op,
                $"'edges' items must be one of: {string.Join(", ", BorderEdges.All)}");
        }

        Require(op.Style is null || BorderLineStyles.All.Contains(op.Style), index, op,
            $"'style' must be one of: {string.Join(", ", BorderLineStyles.All)}");
        RequireColor(op.Color, "color", index, op);

        // A single cell has no inner boundaries, so a purely-inside edge set
        // (horizontal/vertical, or inside which expands to them) would apply
        // nothing — a silent no-op the caller reads as success. Reject it with
        // the fix spelled out.
        if (range.CellCount == 1)
        {
            bool hasOuterEdge = op.Edges.Any(static edge => edge
                is not (BorderEdges.Inside or BorderEdges.Horizontal or BorderEdges.Vertical));
            Require(hasOuterEdge, index, op,
                $"a single-cell range has no inner boundaries, so edges [{string.Join(", ", op.Edges)}] would draw nothing",
                "Use outline (or top/bottom/left/right) on a single cell, or a multi-cell range for inside borders.");
        }

        return op;
    }

    internal static SetDefaultFontOp ValidateSetDefaultFont(SetDefaultFontOp op, int index)
    {
        RequirePresent(op.Name, "name", index, op);
        string name = op.Name.Trim();
        Require(name.Length > 0, index, op, "the font name must not be empty");
        Require(op.Size is null or (>= 1 and <= 409), index, op, "'size' must be between 1 and 409 points");
        return op with { Name = name };
    }

    internal static SetTabColorOp ValidateSetTabColor(SetTabColorOp op, int index)
    {
        // A null color is valid: it removes the tab color.
        RequireColor(op.Color, "color", index, op);
        return op;
    }

    internal static SetSheetViewOp ValidateSetSheetView(SetSheetViewOp op, int index)
    {
        bool any = op.Gridlines is not null || op.Zoom is not null || op.Headings is not null;
        Require(any, index, op,
            "set_sheet_view needs at least one field to change (gridlines, zoom or headings)");

        // The engine's zoom setter silently ignores an out-of-range value and
        // keeps the old zoom (probe-verified), so this check is the only guard
        // between the caller and a silent no-op.
        Require(op.Zoom is null or (>= 10 and <= 400), index, op, "'zoom' must be between 10 and 400");
        return op;
    }

}

internal static class CellsValidationSupport
{
    internal static RangeRef ParseUnqualifiedRange(string text, int index, Op op)
    {
        // An explicit JSON null satisfies STJ's `required` (which checks the
        // property is PRESENT, not non-null), so it reaches here and would
        // deref-crash as INTERNAL. LLMs and serializers emit explicit nulls for
        // unset fields constantly, so catch it as the domain error it is.
        RequirePresent(text, "range", index, op);

        RangeSpec spec = A1.ParseRange(text);
        if (spec.SheetName is not null)
        {
            throw CellsErrors.OpsInvalidAt(index, op.OpName,
                $"the range '{text}' must not be sheet-qualified; use the op's 'sheet' field instead");
        }

        return spec.Range;
    }

    internal static int ParseColumn(string letters) => A1.ParseColumn(letters);

    internal static void Require(bool condition, int index, Op op, string reason, string? hint = null)
    {
        if (!condition)
        {
            throw CellsErrors.OpsInvalidAt(index, op.OpName, reason, hint);
        }
    }

    /// <summary>
    /// Rejects an explicitly-null required field as a domain error. STJ's
    /// `required` only guarantees the property is present, so a JSON <c>null</c>
    /// passes deserialization and would otherwise deref-crash as INTERNAL.
    /// </summary>
    internal static void RequirePresent([System.Diagnostics.CodeAnalysis.NotNull] object? value, string field, int index, Op op)
    {
        if (value is null)
        {
            throw CellsErrors.OpsInvalidAt(index, op.OpName, $"'{field}' must not be null");
        }
    }

    internal static void RequireColor(string? value, string field, int index, Op op)
    {
        if (value is null)
        {
            return;
        }

        bool valid = value.Length == 7 && value[0] == '#'
            && value.Skip(1).All(static c => char.IsAsciiHexDigit(c));
        Require(valid, index, op, $"'{field}' must be a #RRGGBB color, e.g. \"#1F4E79\"");
    }
}
