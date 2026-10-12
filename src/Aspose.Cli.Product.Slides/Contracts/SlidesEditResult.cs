using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides edit</c>: one atomic operation batch, applied or checked.</summary>
public sealed record SlidesEditResult() : EngineResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The edited presentation.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The written presentation; omitted for a dry run.</summary>
    public OutputInfo? Output { get; init; }

    /// <summary>Whether the batch was only checked, without writing anything.</summary>
    public required bool DryRun { get; init; }

    /// <summary>What the dry run checked, in a fixed order; omitted when the edit was written.</summary>
    [MinItems(1), UniqueItems, AllowedValues(typeof(DryRunChecks))]
    public IReadOnlyList<string>? Checked { get; init; }

    /// <summary>The outcome of every operation, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>The backup of the replaced presentation; only for an in-place edit that kept one.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>The stable ids of the slides the batch changed, ascending; omitted when it changed none.</summary>
    [UniqueItems]
    [Minimum(1)]
    public IReadOnlyList<uint>? SlidesTouched { get; init; }

    /// <summary>Whether an operation failed.</summary>
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static op => op.Status == "failed");
}
