using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Cells.Operations;

/// <summary>
/// Extracts the visible footprint of an ops batch — where in the workbook the
/// edits landed — as neutral <see cref="CellsPreviewHint"/>s for the live-preview
/// spotlight (see <see cref="PreviewHintChannel"/>). The footprint is a hint,
/// not an audit: targets are de-duplicated, capped at sixteen, and mapped at
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
    public static IReadOnlyList<CellsPreviewHint> Collect(OpsBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var targets = new List<CellsPreviewHint>();
        var seen = new HashSet<(string? Sheet, string? Range)>();
        foreach (Op op in batch.Ops)
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

    internal static IReadOnlyList<string> OutcomeTargets(Op op) =>
        TargetOf(op) is { Sheet: not null } target
            ? [target.Range is null ? target.Sheet : $"{target.Sheet}!{target.Range}"]
            : [];

    /// <summary>Maps one op to its spotlight target, or null when it has none.</summary>
    internal static CellsPreviewHint? TargetOf(Op op) => op switch
    {
        SetValuesOp or SetFormulaOp or ClearRangeOp or FormatRangeOp or MergeCellsOp
            or UnmergeCellsOp or SortRangeOp or SetValidationOp or ClearValidationOp
            or RemoveDuplicatesOp or CreateTableOp or AddConditionalFormatOp
            or ClearConditionalFormatsOp or SetBordersOp or SetAutoFilterOp or SetPrintAreaOp
            or AddSparklineOp => RangeTarget(op),
        CopyRangeOp or AddCommentOp or EditCommentOp or DeleteCommentOp or SetHyperlinkOp
            or RemoveHyperlinkOp or InsertRowsOp or DeleteRowsOp or InsertColumnsOp or DeleteColumnsOp
            or ResizeRowsOp or ResizeColumnsOp or GroupRowsOp or UngroupRowsOp
            or GroupColumnsOp or UngroupColumnsOp => CellOrStructureTarget(op),
        AddSheetOp or RenameSheetOp or DeleteSheetOp or SetSheetVisibilityOp or MoveSheetOp
            or FreezePanesOp or ProtectSheetOp or UnprotectSheetOp or SetPageSetupOp
            or SetTabColorOp or SetSheetViewOp or SetActiveSheetOp => SheetTarget(op),
        _ => ObjectOrWorkbookTarget(op),
    };

    private static CellsPreviewHint RangeTarget(Op op) => op switch
    {
        // Range ops spotlight the range they edited. SetAutoFilter and
        // SetPrintArea carry an optional range (null clears/removes) and
        // degrade to the sheet as a whole when it is absent.
        SetValuesOp o => new CellsPreviewHint(o.Sheet, o.Range),
        SetFormulaOp o => new CellsPreviewHint(o.Sheet, o.Range),
        ClearRangeOp o => new CellsPreviewHint(o.Sheet, o.Range),
        FormatRangeOp o => new CellsPreviewHint(o.Sheet, o.Range),
        MergeCellsOp o => new CellsPreviewHint(o.Sheet, o.Range),
        UnmergeCellsOp o => new CellsPreviewHint(o.Sheet, o.Range),
        SortRangeOp o => new CellsPreviewHint(o.Sheet, o.Range),
        SetValidationOp o => new CellsPreviewHint(o.Sheet, o.Range),
        ClearValidationOp o => new CellsPreviewHint(o.Sheet, o.Range),
        RemoveDuplicatesOp o => new CellsPreviewHint(o.Sheet, o.Range),
        CreateTableOp o => new CellsPreviewHint(o.Sheet, o.Range),
        AddConditionalFormatOp o => new CellsPreviewHint(o.Sheet, o.Range),
        ClearConditionalFormatsOp o => new CellsPreviewHint(o.Sheet, o.Range),
        SetBordersOp o => new CellsPreviewHint(o.Sheet, o.Range),
        SetAutoFilterOp o => new CellsPreviewHint(o.Sheet, o.Range),
        SetPrintAreaOp o => new CellsPreviewHint(o.Sheet, o.Range),
        // A sparkline's visible change is the location strip it draws into,
        // not the data it reads.
        AddSparklineOp o => new CellsPreviewHint(o.Sheet, o.Location),
        _ => throw new InvalidOperationException(),
    };

    private static CellsPreviewHint CellOrStructureTarget(Op op) => op switch
    {
        // A copy changes its destination; the destination anchor may be
        // sheet-qualified and then overrides the op's sheet.
        CopyRangeOp o => CopyTarget(o),

        // Single-cell ops spotlight their cell.
        AddCommentOp o => new CellsPreviewHint(o.Sheet, o.Cell),
        EditCommentOp o => new CellsPreviewHint(o.Sheet, o.Cell),
        DeleteCommentOp o => new CellsPreviewHint(o.Sheet, o.Cell),
        SetHyperlinkOp o => new CellsPreviewHint(o.Sheet, o.Cell),
        RemoveHyperlinkOp o => new CellsPreviewHint(o.Sheet, o.Cell),

        // Row/column structure shifts everything below or to the right, and
        // A1 has no whole-row/column form — spotlight the sheet.
        InsertRowsOp o => new CellsPreviewHint(o.Sheet, null),
        DeleteRowsOp o => new CellsPreviewHint(o.Sheet, null),
        InsertColumnsOp o => new CellsPreviewHint(o.Sheet, null),
        DeleteColumnsOp o => new CellsPreviewHint(o.Sheet, null),
        ResizeRowsOp o => new CellsPreviewHint(o.Sheet, null),
        ResizeColumnsOp o => new CellsPreviewHint(o.Sheet, null),
        GroupRowsOp o => new CellsPreviewHint(o.Sheet, null),
        UngroupRowsOp o => new CellsPreviewHint(o.Sheet, null),
        GroupColumnsOp o => new CellsPreviewHint(o.Sheet, null),
        UngroupColumnsOp o => new CellsPreviewHint(o.Sheet, null),
        _ => throw new InvalidOperationException(),
    };

    private static CellsPreviewHint SheetTarget(Op op) => op switch
    {
        // Sheet-level ops spotlight the affected sheet as a whole. For
        // add/rename the sheet worth looking at is the one that exists after
        // the edit: the new sheet's name, the renamed sheet's new name.
        AddSheetOp o => new CellsPreviewHint(o.Name, null),
        RenameSheetOp o => new CellsPreviewHint(o.To, null),
        DeleteSheetOp o => new CellsPreviewHint(o.Sheet, null),
        SetSheetVisibilityOp o => new CellsPreviewHint(o.Sheet, null),
        MoveSheetOp o => new CellsPreviewHint(o.Sheet, null),
        FreezePanesOp o => new CellsPreviewHint(o.Sheet, null),
        ProtectSheetOp o => new CellsPreviewHint(o.Sheet, null),
        UnprotectSheetOp o => new CellsPreviewHint(o.Sheet, null),
        SetPageSetupOp o => new CellsPreviewHint(o.Sheet, null),
        SetTabColorOp o => new CellsPreviewHint(o.Sheet, null),
        SetSheetViewOp o => new CellsPreviewHint(o.Sheet, null),
        SetActiveSheetOp o => new CellsPreviewHint(o.Sheet, null),
        _ => throw new InvalidOperationException(),
    };

    private static CellsPreviewHint? ObjectOrWorkbookTarget(Op op) => op switch
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

        // Forward-compatibility: an op added after this mapping still yields
        // a sheet-level hint when it names a sheet.
        _ => SheetLevelOrNone(op),
    };

    private static CellsPreviewHint? SheetLevelOrNone(Op op) =>
        op.Sheet is null ? null : new CellsPreviewHint(op.Sheet, null);

    private static CellsPreviewHint CopyTarget(CopyRangeOp op)
    {
        try
        {
            RangeSpec destination = A1.ParseRange(op.To);
            return new CellsPreviewHint(destination.SheetName ?? op.Sheet, A1.FormatRange(destination.Range));
        }
        catch (CliException)
        {
            // Best effort: an unparsable destination (conceivable under
            // --best-effort) still hints at the op's sheet.
            return new CellsPreviewHint(op.Sheet, null);
        }
    }

    private static string? Fold(string? value) => value?.ToUpperInvariant();
}
