using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of one <c>pdf edit</c> batch.</summary>
public sealed record PdfEditResult() : EngineResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The PDF edited.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The PDF written; absent for a dry run, which writes nothing.</summary>
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }

    /// <summary>Whether the batch was only checked and applied in memory (<c>--dry-run</c>).</summary>
    public required bool DryRun { get; init; }

    /// <summary>What the dry run checked, in a fixed order; omitted when the edit was written.</summary>
    [MinItems(1), UniqueItems, AllowedValues(typeof(DryRunChecks))]
    public IReadOnlyList<string>? Checked { get; init; }

    /// <summary>The outcome of each operation, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>The copy of the input kept before it was replaced in place; absent otherwise.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>The 1-based pages the operations changed; absent when they changed none.</summary>
    [Minimum(1)]
    public IReadOnlyList<int>? PagesTouched { get; init; }

    /// <summary>
    /// Present with <c>--verify</c>: the staged output read back against the effect of each form,
    /// redaction, bookmark, metadata, attachment and page operation.
    /// </summary>
    public PdfEditVerification? Verification { get; init; }

    /// <summary>Whether an operation failed or the read-back found a difference.</summary>
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static item => item.Status == OpStatuses.Failed)
        || Verification is { Ok: false };
}

/// <summary>
/// What <c>--verify</c> read back from the staged output: the effect of each form, redaction,
/// bookmark, metadata, attachment and page operation.
/// </summary>
public sealed record PdfEditVerification : EditVerification;
