using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// Result of <c>words edit</c>: one operation batch, applied or checked, and the checks run on
/// the written document.
/// </summary>
public sealed record WordsEditResult() : EngineResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The edited document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written document; absent for a dry run.</summary>
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }

    /// <summary>Whether the batch was only checked, writing nothing.</summary>
    public required bool DryRun { get; init; }

    /// <summary>What the dry run checked, in a fixed order; omitted when the edit was written.</summary>
    [MinItems(1), UniqueItems, AllowedValues(typeof(DryRunChecks))]
    public IReadOnlyList<string>? Checked { get; init; }

    /// <summary>The outcome of each operation, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>The backup of the replaced file, when the edit wrote over an existing file.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>
    /// The output pages that hold what the batch changed: the pages of each changed block, and
    /// for a removed block the page of the content that took its place; every page of a section
    /// whose page setup, headers or footers changed; every page for the watermark; and the pages
    /// of the text in a changed style. Absent when the batch changed nothing on a page, such as
    /// only document properties or the editing restriction.
    /// </summary>
    [Minimum(1, Depth = 1)]
    [UniqueItems]
    public IReadOnlyList<int>? PagesTouched { get; init; }

    /// <summary>The checks run on the written document after reopening it.</summary>
    public WordsEditVerification? Verification { get; init; }

    /// <inheritdoc />
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static item => item.Status == OpStatuses.Failed)
        || Verification is { Ok: false };
}

/// <summary>
/// Post-edit verification evidence: the written document reopened, its field count, revision
/// count and protection compared with the edited document, and its content compared with the
/// input's.
/// </summary>
public sealed record WordsEditVerification : EditVerification
{
    /// <summary>Whether the written document's content differs from the input's once both accept every tracked change.</summary>
    public bool? SemanticChangesDetected { get; init; }

    /// <summary>The number of fields in the written document.</summary>
    [Minimum(0)]
    public int? FieldCount { get; init; }

    /// <summary>The number of revisions the written document stores.</summary>
    [Minimum(0)]
    public int? RevisionCount { get; init; }

    /// <summary>
    /// The written document's editing restriction, as the protect operation mode that sets it;
    /// every SDK protection type maps to one of these. Absent for a document without one.
    /// </summary>
    [AllowedValues(typeof(WordsProtectionModes))]
    public string? Protection { get; init; }
}
