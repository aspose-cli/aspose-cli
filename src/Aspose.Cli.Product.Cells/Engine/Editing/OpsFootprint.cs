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
    /// order, capped at sixteen. A null range means the sheet as a whole.
    /// </summary>
    /// <param name="batch">The ops batch that was applied.</param>
    /// <param name="activeSheet">
    /// The sheet an operation that names none ran on, as the batch found it before its first
    /// operation; null leaves such a target's sheet null.
    /// </param>
    /// <param name="cellsOnly">
    /// Whether only the operations that write cell values or formulas, or add, remove or rename
    /// sheets, count: a change of a cell under any other target, such as frozen panes over the
    /// whole sheet, is a recalculated result, not one the batch asked for.
    /// </param>
    public static IReadOnlyList<CellsEditTarget> Collect(CellsOpsBatch batch, string? activeSheet = null, bool cellsOnly = false)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var targets = new List<CellsEditTarget>();
        var seen = new HashSet<(string? Sheet, string? Range)>();
        foreach (CellsOp op in batch.Ops)
        {
            if ((cellsOnly && !Diffed(op)) || TargetOf(op) is not { } found)
            {
                continue;
            }

            CellsEditTarget target = found.Sheet is null && op is CellsSheetOp ? found with { Sheet = activeSheet } : found;

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
    /// The ids, in batch order, of the operations whose changes verification reads back: each
    /// applied operation that writes cell values or formulas or adds, removes or renames a sheet,
    /// which the diff of the staged output compares, and whose target names a sheet and is one of
    /// the <paramref name="footprint"/> targets, so the diff lists its cells as direct changes.
    /// Formatting, view, object and workbook-level operations and one beyond the sixteen targets
    /// are not checked; an operation that names no sheet counts on <paramref name="activeSheet"/>.
    /// </summary>
    /// <param name="batch">The ops batch that was applied.</param>
    /// <param name="footprint">The targets <see cref="Collect"/> returned for the batch.</param>
    /// <param name="applied">The ids of the operations that were applied.</param>
    /// <param name="activeSheet">The sheet an operation that names none ran on, as the batch found it.</param>
    internal static IReadOnlyList<string> Checked(
        CellsOpsBatch batch,
        IReadOnlyList<CellsEditTarget> footprint,
        IReadOnlySet<string> applied,
        string? activeSheet = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var diffed = new HashSet<(string? Sheet, string? Range)>(
            footprint.Select(static target => (Fold(target.Sheet), Fold(target.Range))));
        return
        [
            .. batch.Ops
                .Where(op => op.Id is { } id && applied.Contains(id) && Diffed(op)
                    && TargetOf(op) is { } found
                    && (found.Sheet is null && op is CellsSheetOp ? found with { Sheet = activeSheet } : found) is { Sheet: not null } target
                    && diffed.Contains((Fold(target.Sheet), Fold(target.Range))))
                .Select(static op => op.Id!),
        ];
    }

    /// <summary>Whether the verification diff, which compares cell values, formulas and sheets, sees what the operation does.</summary>
    private static bool Diffed(CellsOp op) =>
        op is SetValuesOp or SetFormulaOp or ClearRangeOp or ReplaceTextOp or SortRangeOp or DeleteDuplicatesOp
            or CopyRangeOp or ImportRangeOp or InsertRowsOp or DeleteRowsOp or InsertColumnsOp or DeleteColumnsOp
            or InsertSheetOp or ImportSheetOp or RenameSheetOp or DeleteSheetOp;

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

    /// <summary>Maps one op to its spotlight target, or null when it has none.</summary>
    internal static CellsEditTarget? TargetOf(CellsOp op) => op switch
    {
        SetValuesOp or SetFormulaOp or ClearRangeOp or FormatRangeOp or MergeCellsOp
            or UnmergeCellsOp or SortRangeOp or SetValidationOp or ClearValidationOp
            or DeleteDuplicatesOp or DefineTableOp or ResizeTableOp or AddConditionalFormatOp
            or ClearConditionalFormatsOp or SetBordersOp or SetAutoFilterOp or SetPrintAreaOp
            or AddSparklineOp or ReplaceTextOp => RangeTarget(op),
        CopyRangeOp or ImportRangeOp or AddCommentOp or SetCommentOp or DeleteCommentOp or SetHyperlinkOp
            or DeleteHyperlinkOp or InsertRowsOp or DeleteRowsOp or InsertColumnsOp or DeleteColumnsOp
            or ResizeRowsOp or ResizeColumnsOp or GroupRowsOp or UngroupRowsOp
            or GroupColumnsOp or UngroupColumnsOp => CellOrStructureTarget(op),
        InsertSheetOp or ImportSheetOp or RenameSheetOp or DeleteSheetOp or SetSheetVisibilityOp or MoveSheetOp
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
        // Without a range it replaces anywhere in the sheet.
        ReplaceTextOp o => new CellsEditTarget(o.Sheet, o.Range),
        FormatRangeOp o => new CellsEditTarget(o.Sheet, o.Range),
        MergeCellsOp o => new CellsEditTarget(o.Sheet, o.Range),
        UnmergeCellsOp o => new CellsEditTarget(o.Sheet, o.Range),
        SortRangeOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetValidationOp o => new CellsEditTarget(o.Sheet, Bounds(o.Range)),
        ClearValidationOp o => new CellsEditTarget(o.Sheet, Bounds(o.Range)),
        DeleteDuplicatesOp o => new CellsEditTarget(o.Sheet, o.Range),
        DefineTableOp o => new CellsEditTarget(o.Sheet, o.Range),
        ResizeTableOp o => new CellsEditTarget(o.Sheet, o.Range),
        AddConditionalFormatOp o => new CellsEditTarget(o.Sheet, Bounds(o.Range)),
        ClearConditionalFormatsOp o => new CellsEditTarget(o.Sheet, Bounds(o.Range)),
        SetBordersOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetAutoFilterOp o => new CellsEditTarget(o.Sheet, o.Range),
        SetPrintAreaOp o => new CellsEditTarget(o.Sheet, Bounds(o.Range)),
        // A sparkline's visible change is the location strip it draws into,
        // not the data it reads.
        AddSparklineOp o => new CellsEditTarget(o.Sheet, Bounds(o.At)),
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
        SetCommentOp o => new CellsEditTarget(o.Sheet, o.Cell),
        DeleteCommentOp o => new CellsEditTarget(o.Sheet, o.Cell),
        SetHyperlinkOp o => new CellsEditTarget(o.Sheet, o.Cell),
        DeleteHyperlinkOp o => new CellsEditTarget(o.Sheet, o.Cell),

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
        InsertSheetOp o => new CellsEditTarget(o.Name, null),
        // An imported sheet is named after its source sheet unless it is given a name; the
        // source's first sheet is known only once the source is open.
        ImportSheetOp o => new CellsEditTarget(o.Name ?? o.SourceSheet, null),
        RenameSheetOp o => new CellsEditTarget(o.Name, null),
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
        InsertChartOp o => SheetLevelOrNone(o),
        SetChartOp o => SheetLevelOrNone(o),
        DeleteChartOp o => SheetLevelOrNone(o),
        InsertPivotOp o => SheetLevelOrNone(o),
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
        op.TargetSheet is null ? null : new CellsEditTarget(op.TargetSheet, null);

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

    /// <summary>
    /// The one range that covers a list of cells and ranges, such as D5:D8 for D5,D7,D8; the
    /// list as given when it is unparsable, which only a failed operation can carry.
    /// </summary>
    private static string Bounds(string list)
    {
        try
        {
            IReadOnlyList<RangeRef> parts = A1.ParseRanges(list);
            return A1.FormatRange(new RangeRef(
                new CellRef(parts.Min(static part => part.Start.Row), parts.Min(static part => part.Start.Column)),
                new CellRef(parts.Max(static part => part.End.Row), parts.Max(static part => part.End.Column))));
        }
        catch (CliException)
        {
            return list;
        }
    }

    private static string? Fold(string? value) => value?.ToUpperInvariant();
}
