using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells edit</c>. Batches are
/// atomic by default: the result exists only when every op applied (or on a dry
/// run). With <c>--best-effort</c> the batch is partial — failing ops
/// are recorded with <c>status: failed</c> and an error, the rest still apply,
/// and the command exits 8. A resource failure of the invocation aborts publication.
/// Verification describes the staged candidate and the evidence published with it.
/// </summary>
public sealed record CellsEditResult() : EngineResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The workbook that was edited.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The produced file; omitted for dry runs.</summary>
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }

    /// <summary><c>true</c> when nothing was written (<c>--dry-run</c>).</summary>
    public required bool DryRun { get; init; }

    /// <summary>What the dry run checked, in a fixed order; omitted when the edit was written.</summary>
    [MinItems(1), UniqueItems, AllowedValues(typeof(DryRunChecks))]
    public IReadOnlyList<string>? Checked { get; init; }

    /// <summary>
    /// <c>true</c> when formulas were recalculated before saving: a calculation runs after the
    /// complete batch unless <c>--no-recalc</c> is given, and this is true when it succeeded.
    /// </summary>
    public required bool Recalculated { get; init; }

    /// <summary>Per-op outcomes, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>Stable safety backup used for an in-place mutation, when requested.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>
    /// Present with <c>--verify</c>: the staged workbook compared cell by cell with the input, and
    /// scanned for formula errors.
    /// </summary>
    public CellsEditVerification? Verification { get; init; }

    /// <summary>Whether any op failed (best-effort mode). Not serialized.</summary>
    [JsonIgnore]
    public bool HasFailures
    {
        get
        {
            if (Verification is { Ok: false })
            {
                return true;
            }

            foreach (BoundedOperationOutcome op in Applied)
            {
                if (op.Status == OpStatuses.Failed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>Post-edit evidence produced by <c>cells edit --verify</c>.</summary>
public sealed record CellsEditVerification : EditVerification
{
    /// <summary>Targets requested by the operations in this invocation.</summary>
    public required IReadOnlyList<CellsVerificationTarget> RequestedTargets { get; init; }

    /// <summary>Changes inside a requested operation target.</summary>
    public required IReadOnlyList<CellsVerifiedCellChange> DirectChanges { get; init; }

    /// <summary>Cells whose formula text stayed the same while the calculated result changed.</summary>
    public required IReadOnlyList<CellsVerifiedCellChange> FormulaResultChanges { get; init; }

    /// <summary>Changes that could not be reliably attributed to a requested target or formula recalculation.</summary>
    public required IReadOnlyList<CellsVerificationOtherChange> OtherChanges { get; init; }

    /// <summary>Formula errors found anywhere in the edited workbook.</summary>
    public required IReadOnlyList<CellsCellError> FormulaErrors { get; init; }

    /// <summary>
    /// <c>true</c> when the edit changed more than 1000 cells, so the change lists hold only the
    /// first 1000; the checks behind <c>ok</c> still cover every cell.
    /// </summary>
    public required bool Truncated { get; init; }
}

/// <summary>An operation target included in post-edit verification.</summary>
public sealed record CellsVerificationTarget
{
    /// <summary>Worksheet name; omitted when the operation addressed the active sheet implicitly.</summary>
    public string? Sheet { get; init; }

    /// <summary>A1 range; omitted for a whole-sheet target.</summary>
    public string? Range { get; init; }
}

/// <summary>Before-and-after values for one verified cell.</summary>
public sealed record CellsVerifiedCellChange
{
    /// <summary>Worksheet name.</summary>
    public required string Sheet { get; init; }

    /// <summary>A1 cell address.</summary>
    public required string Cell { get; init; }

    /// <summary>Cell before the edit; omitted when it was empty.</summary>
    public CellsCellSide? Left { get; init; }

    /// <summary>Cell after the edit; omitted when it is empty.</summary>
    public CellsCellSide? Right { get; init; }
}

/// <summary>A changed cell or worksheet that could not be classified as a direct or formula-result change.</summary>
public sealed record CellsVerificationOtherChange
{
    /// <summary>Worksheet name.</summary>
    public required string Sheet { get; init; }

    /// <summary>Sheet status: <c>added</c>, <c>removed</c>, <c>modified</c> or <c>renamed</c>.</summary>
    [AllowedValues(typeof(CellsSheetChangeStatuses))]
    public required string Status { get; init; }

    /// <summary>A1 cell address for a cell-level change.</summary>
    public string? Cell { get; init; }

    /// <summary>Cell before the edit; omitted when absent or sheet-level.</summary>
    public CellsCellSide? Left { get; init; }

    /// <summary>Cell after the edit; omitted when absent or sheet-level.</summary>
    public CellsCellSide? Right { get; init; }
}
