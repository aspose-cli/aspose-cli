namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Extracts the visible footprint of an ops batch — where in the workbook the
/// edits landed — as neutral <see cref="CellsEditTarget"/>s. Verification
/// reads it to check that the edits landed where they were asked to. The
/// footprint is a summary, not an audit: targets are de-duplicated, capped at sixteen, and each is
/// the target <see cref="CellsOpFootprints"/> maps its operation to.
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
            if (CellsOpFootprints.Of(op) is not { Target: { } found } traits || (cellsOnly && !traits.Diffed))
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
    /// operation that writes cell values or formulas or adds, removes or renames a sheet, which
    /// the diff of the staged output compares, and whose target names a sheet and is one of the
    /// <paramref name="footprint"/> targets, so the diff lists its cells as direct changes.
    /// Formatting, view, object and workbook-level operations and one beyond the sixteen targets
    /// are not checked; an operation that names no sheet counts on <paramref name="activeSheet"/>.
    /// </summary>
    /// <param name="batch">The ops batch that was applied.</param>
    /// <param name="footprint">The targets <see cref="Collect"/> returned for the batch.</param>
    /// <param name="activeSheet">The sheet an operation that names none ran on, as the batch found it.</param>
    internal static IReadOnlyList<string> Checked(
        CellsOpsBatch batch,
        IReadOnlyList<CellsEditTarget> footprint,
        string? activeSheet = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return [.. batch.Ops
            .Where(op => DiffedTarget(op, activeSheet) is { Sheet: not null } target && Contains(footprint, target))
            .Select(static op => op.Id!)];
    }

    /// <summary>
    /// Why each operation <see cref="Checked"/> leaves out was not read back: what it changes the
    /// diff does not compare, it is beyond the sixteen targets, or it changed a sheet the output
    /// does not hold.
    /// </summary>
    /// <param name="batch">The ops batch that was applied.</param>
    /// <param name="footprint">The targets <see cref="Collect"/> returned for the batch.</param>
    /// <param name="held">Those of the targets on the sheets the output holds.</param>
    /// <param name="activeSheet">The sheet an operation that names none ran on, as the batch found it.</param>
    internal static IReadOnlyDictionary<string, string> UncheckedReasons(
        CellsOpsBatch batch,
        IReadOnlyList<CellsEditTarget> footprint,
        IReadOnlyList<CellsEditTarget> held,
        string? activeSheet)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (CellsOp op in batch.Ops)
        {
            CellsEditTarget? target = DiffedTarget(op, activeSheet);
            string? reason = target is null ? "The comparison reads back cell values, formulas and sheets; this operation changes something else, so read the output to confirm it."
                : !Contains(footprint, target) ? $"The comparison reads back the first {MaxTargets} targets of a batch, and this operation's target is beyond them."
                : !Contains(held, target) ? "The output holds one sheet and this operation changed another, so its effect is not in the output."
                : null;
            if (reason is not null)
            {
                reasons[op.Id!] = reason;
            }
        }

        return reasons;
    }

    /// <summary>The target of an operation the diff compares, with the sheet it ran on when it names none; null for any other.</summary>
    private static CellsEditTarget? DiffedTarget(CellsOp op, string? activeSheet) =>
        CellsOpFootprints.Of(op) is { Diffed: true, Target: { } found }
            ? found.Sheet is null && op is CellsSheetOp ? found with { Sheet = activeSheet } : found
            : null;

    private static bool Contains(IReadOnlyList<CellsEditTarget> targets, CellsEditTarget target) =>
        targets.Any(item => Fold(item.Sheet) == Fold(target.Sheet) && Fold(item.Range) == Fold(target.Range));

    /// <summary>
    /// Cells an operation writes, charged against the cells budget before it runs: the
    /// target range, or the source range of a copy.
    /// </summary>
    internal static long CellCost(CellsOp op)
    {
        CellsOpTraits traits = CellsOpFootprints.Of(op);
        return (traits.Source ?? traits.Target?.Range) is { } range ? A1.ParseRange(range).Range.CellCount : 0;
    }

    private static string? Fold(string? value) => value?.ToUpperInvariant();
}
