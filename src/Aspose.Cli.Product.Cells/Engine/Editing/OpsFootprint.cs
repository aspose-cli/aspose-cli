using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Extracts the visible footprint of an ops batch — where in the workbook the
/// edits landed — as neutral <see cref="CellsEditTarget"/>s. Verification
/// reads it to check that the edits landed where they were asked to. The
/// footprint is a summary, not an audit: targets are de-duplicated, capped at sixteen, and mapped at
/// the granularity that is useful to look at — a range where the op has one,
/// a cell for single-cell ops, the whole sheet for structural changes (A1
/// deliberately rejects whole-row/column ranges, so a row or column band has
/// no range representation), and nothing at all for workbook-level ops with
/// no visible spot to focus.
/// </summary>
internal static class OpsFootprint
{
    /// <summary>The spotlight points somewhere; sixteen spots is already everywhere.</summary>
    private const int MaxTargets = 16;

    /// <summary>
    /// Collects the distinct targets of <paramref name="batch"/>, in batch
    /// order, capped at sixteen. A target's null sheet means the op ran on
    /// the active sheet; a null range means the sheet as a whole.
    /// </summary>
    /// <param name="batch">The ops batch that was applied.</param>
    public static IReadOnlyList<CellsEditTarget> Collect(CellsOpsBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var targets = new List<CellsEditTarget>();
        var seen = new HashSet<(string? Sheet, string? Range)>();
        foreach (CellsOp op in batch.Ops)
        {
            if (TargetOf(op) is not { } target)
            {
                continue;
            }

            // Sheet names and A1 references are case-insensitive in a workbook.
            if (!seen.Add((Fold(target.Sheet), Fold(target.Range))))
            {
                continue;
            }

            targets.Add(target);
            if (targets.Count == MaxTargets)
            {
                break;
            }
        }

        return targets;
    }

    /// <summary>
    /// Cells an operation writes, charged against the cells budget before it runs: the
    /// target range, or the source range of a copy.
    /// </summary>
    internal static long CellCost(CellsOp op) => op switch
    {
        CopyRangeOp copy => A1.ParseRange(copy.From).Range.CellCount,
        ImportRangeOp import => A1.ParseRange(import.From).Range.CellCount,
        _ => TargetOf(op) is { Range: { } range } ? A1.ParseRange(range).Range.CellCount : 0,
    };

    internal static IReadOnlyList<string> OutcomeTargets(CellsOp op) =>
        TargetOf(op) is { Sheet: not null } target
            ? [target.Range is null ? target.Sheet : $"{target.Sheet}!{target.Range}"]
            : [];

    /// <summary>Maps one op to its spotlight target, or null when it has none.</summary>
    internal static CellsEditTarget? TargetOf(CellsOp op) => op switch
    {
        SetValuesOp or SetFormulaOp or ClearRangeOp or FormatRangeOp or MergeCellsOp
            or UnmergeCellsOp or SortRangeOp or SetValidationOp or ClearValidationOp
            or RemoveDuplicatesOp or CreateTableOp or AddConditionalFormatOp
            or ClearConditionalFormatsOp or SetBordersOp or SetAutoFilterOp or SetPrintAreaOp
            or AddSparklineOp => RangeTarget(op),
        CopyRangeOp or ImportRangeOp or AddCommentOp or EditCommentOp or DeleteCommentOp or SetHyperlinkOp
            or RemoveHyperlinkOp or InsertRowsOp or DeleteRowsOp or InsertColumnsOp or DeleteColumnsOp
            or ResizeRowsOp or ResizeColumnsOp or GroupRowsOp or UngroupRowsOp
            or GroupColumnsOp or UngroupColumnsOp => CellOrStructureTarget(op),
        AddSheetOp or ImportSheetOp or RenameSheetOp or DeleteSheetOp or SetSheetVisibilityOp or MoveSheetOp
            or FreezePanesOp or ProtectSheetOp or UnprotectSheetOp or SetPageSetupOp
            or SetTabColorOp or SetSheetViewOp or SetActiveSheetOp => SheetTarget(op),
        _ => ObjectOrWorkbookTarget(op),
    };

    private static CellsEditTarget RangeTarget(CellsOp op) => op switch
    {
        // Range ops spotlight the range they edited. SetAutoFilter and
        // SetPrintArea carry an optional range (null clears/removes) and
        // degrade to the sheet as a whole when it is absent.
        // An anchor cell spans the matrix written from it.
        SetValuesOp o => new CellsEditTarget(o.Sheet, MatrixRange(o.Range, o.Values)),
        SetFormulaOp o => new CellsEditTarget(o.Sheet, o.Range),
        ClearRangeOp o => new CellsEditTarget(o.Sheet, o.Range),
        FormatRangeOp o => new CellsEditTarget(o.Sheet, o.Range),
        MergeCellsOp o => new CellsEditTarget(o.Sheet, o.Range),
        UnmergeCellsOp o => new CellsEditTarget(o.Sheet, o.Range),
        SortRangeOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetValidationOp o => new CellsEditTarget(o.Sheet, o.Range),
        ClearValidationOp o => new CellsEditTarget(o.Sheet, o.Range),
        RemoveDuplicatesOp o => new CellsEditTarget(o.Sheet, o.Range),
        CreateTableOp o => new CellsEditTarget(o.Sheet, o.Range),
        AddConditionalFormatOp o => new CellsEditTarget(o.Sheet, o.Range),
        ClearConditionalFormatsOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetBordersOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetAutoFilterOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetPrintAreaOp o => new CellsEditTarget(o.Sheet, o.Range),
        // A sparkline's visible change is the location strip it draws into,
        // not the data it reads.
        AddSparklineOp o => new CellsEditTarget(o.Sheet, o.Location),
        _ => throw new InvalidOperationException(),
    };

    private static CellsEditTarget CellOrStructureTarget(CellsOp op) => op switch
    {
        // A copy changes its destination; the destination anchor may be
        // sheet-qualified and then overrides the op's sheet.
        CopyRangeOp o => CopyTarget(o.Sheet, o.To, from: null),
        // An import spotlights the whole range it wrote.
        ImportRangeOp o => CopyTarget(o.Sheet, o.To, o.From),

        // Single-cell ops spotlight their cell.
        AddCommentOp o => new CellsEditTarget(o.Sheet, o.Cell),
        EditCommentOp o => new CellsEditTarget(o.Sheet, o.Cell),
        DeleteCommentOp o => new CellsEditTarget(o.Sheet, o.Cell),
        SetHyperlinkOp o => new CellsEditTarget(o.Sheet, o.Cell),
        RemoveHyperlinkOp o => new CellsEditTarget(o.Sheet, o.Cell),

        // Row/column structure shifts everything below or to the right, and
        // A1 has no whole-row/column form — spotlight the sheet.
        InsertRowsOp o => new CellsEditTarget(o.Sheet, null),
        DeleteRowsOp o => new CellsEditTarget(o.Sheet, null),
        InsertColumnsOp o => new CellsEditTarget(o.Sheet, null),
        DeleteColumnsOp o => new CellsEditTarget(o.Sheet, null),
        ResizeRowsOp o => new CellsEditTarget(o.Sheet, null),
        ResizeColumnsOp o => new CellsEditTarget(o.Sheet, null),
        GroupRowsOp o => new CellsEditTarget(o.Sheet, null),
        UngroupRowsOp o => new CellsEditTarget(o.Sheet, null),
        GroupColumnsOp o => new CellsEditTarget(o.Sheet, null),
        UngroupColumnsOp o => new CellsEditTarget(o.Sheet, null),
        _ => throw new InvalidOperationException(),
    };

    private static CellsEditTarget SheetTarget(CellsOp op) => op switch
    {
        // Sheet-level ops spotlight the affected sheet as a whole. For
        // add/rename the sheet worth looking at is the one that exists after
        // the edit: the new sheet's name, the renamed sheet's new name.
        AddSheetOp o => new CellsEditTarget(o.Name, null),
        // An imported sheet is named after its source sheet unless it is given a name; the
        // source's first sheet is known only once the source is open.
        ImportSheetOp o => new CellsEditTarget(o.Name ?? o.Sheet, null),
        RenameSheetOp o => new CellsEditTarget(o.To, null),
        DeleteSheetOp o => new CellsEditTarget(o.Sheet, null),
        SetSheetVisibilityOp o => new CellsEditTarget(o.Sheet, null),
        MoveSheetOp o => new CellsEditTarget(o.Sheet, null),
        FreezePanesOp o => new CellsEditTarget(o.Sheet, null),
        ProtectSheetOp o => new CellsEditTarget(o.Sheet, null),
        UnprotectSheetOp o => new CellsEditTarget(o.Sheet, null),
        SetPageSetupOp o => new CellsEditTarget(o.Sheet, null),
        SetTabColorOp o => new CellsEditTarget(o.Sheet, null),
        SetSheetViewOp o => new CellsEditTarget(o.Sheet, null),
        SetActiveSheetOp o => new CellsEditTarget(o.Sheet, null),
        _ => throw new InvalidOperationException(),
    };

    private static CellsEditTarget? ObjectOrWorkbookTarget(CellsOp op) => op switch
    {
        // Embedded objects land on their sheet; without an explicit sheet
        // there is nothing precise enough to point at.
        CreateChartOp o => SheetLevelOrNone(o),
        UpdateChartOp o => SheetLevelOrNone(o),
        DeleteChartOp o => SheetLevelOrNone(o),
        CreatePivotOp o => SheetLevelOrNone(o),
        RefreshPivotOp o => SheetLevelOrNone(o),
        InsertImageOp o => SheetLevelOrNone(o),

        // Workbook-level ops have no visible spot to focus. set_default_font
        // is workbook-scoped (its sheet is ignored), so it reports none even
        // when a sheet is set.
        DefineNameOp => null,
        DeleteNameOp => null,
        ProtectWorkbookOp => null,
        UnprotectWorkbookOp => null,
        SetDefaultFontOp => null,

        // An op without an entry here yields a sheet-level hint when it names a sheet.
        _ => SheetLevelOrNone(op),
    };

    private static CellsEditTarget? SheetLevelOrNone(CellsOp op) =>
        op.Sheet is null ? null : new CellsEditTarget(op.Sheet, null);

    /// <summary>
    /// The destination of a copy: its anchor, or the source range's size at the anchor when
    /// <paramref name="from"/> is given, clipped to the grid.
    /// </summary>
    private static CellsEditTarget CopyTarget(string? sheet, string to, string? from)
    {
        try
        {
            RangeSpec destination = A1.ParseRange(to);
            RangeRef landed = destination.Range;
            if (from is not null)
            {
                RangeRef source = A1.ParseRange(from).Range;
                CellRef anchor = landed.Start;
                landed = new RangeRef(anchor, new CellRef(
                    Math.Min(anchor.Row + source.RowCount, A1.MaxRows) - 1,
                    Math.Min(anchor.Column + source.ColumnCount, A1.MaxColumns) - 1));
            }

            return new CellsEditTarget(destination.SheetName ?? sheet, A1.FormatRange(landed));
        }
        catch (CliException)
        {
            // Best effort: an unparsable destination (conceivable under
            // --best-effort) still hints at the op's sheet.
            return new CellsEditTarget(sheet, null);
        }
    }

    /// <summary>
    /// The range <paramref name="values"/> fill from the top-left cell of <paramref name="range"/>,
    /// clipped to the grid; the range as given when it is unparsable or the matrix is empty, which
    /// only a failed operation under --best-effort can carry.
    /// </summary>
    private static string MatrixRange(string range, IReadOnlyList<IReadOnlyList<object?>> values)
    {
        if (values is not [{ Count: > 0 } first, ..])
        {
            return range;
        }

        try
        {
            CellRef anchor = A1.ParseRange(range).Range.Start;
            return A1.FormatRange(new RangeRef(anchor, new CellRef(
                Math.Min(anchor.Row + values.Count, A1.MaxRows) - 1,
                Math.Min(anchor.Column + first.Count, A1.MaxColumns) - 1)));
        }
        catch (CliException)
        {
            return range;
        }
    }

    private static string? Fold(string? value) => value?.ToUpperInvariant();
}
