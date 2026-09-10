using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Result of <c>aspose-cli cells edit</c>. Batches are
/// atomic by default: the result exists only when every op applied (or on a dry
/// run). With <c>--best-effort</c> the batch is partial — failing ops
/// are recorded with <c>status: failed</c> and an error, the rest still apply,
/// and the command exits 8.
/// </summary>
public sealed record EditResult() : ResultEnvelope(CellsSchemaIds.EditResult, 2), IPartialOutcome
{
    /// <summary>The workbook that was edited.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The produced file; omitted for dry runs.</summary>
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }

    /// <summary><c>true</c> when nothing was written (<c>--dry-run</c>).</summary>
    public required bool DryRun { get; init; }

    /// <summary><c>true</c> when formulas were recalculated before saving.</summary>
    public required bool Recalculated { get; init; }

    /// <summary>Per-op outcomes, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>Stable safety backup used for an in-place mutation, when requested.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>Post-edit diff, formula, and render evidence produced by <c>--verify</c>.</summary>
    public EditVerification? Verification { get; init; }

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

/// <summary>Outcome of one op in a batch.</summary>

/// <summary>Visible Cells address associated with an operation result.</summary>
/// <summary>
/// Result of <c>aspose-cli cells create</c>.
/// </summary>
public sealed record CreateResult() : ResultEnvelope(CellsSchemaIds.CreateResult, 2)
{
    /// <summary>The produced file.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>Sheet names of a newly created workbook.</summary>
    public required IReadOnlyList<string> Sheets { get; init; }
}
