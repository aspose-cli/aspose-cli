using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>Portable, static evidence bundle produced by the root review command.</summary>
public sealed record ReviewResult() : ResultEnvelope(CommonSchemaIds.Review, 2), IPartialOutcome
{
    public required string Product { get; init; }

    public required string Input { get; init; }

    public required string OutputDirectory { get; init; }

    public required string Index { get; init; }

    public required string Manifest { get; init; }

    public required string View { get; init; }

    public required string SourceFormat { get; init; }

    public required long SourceSizeBytes { get; init; }

    public required bool VisualInspectionRequired { get; init; }

    public required ReviewCoverage Coverage { get; init; }

    public required IReadOnlyList<ReviewArtifact> Artifacts { get; init; }

    public required IReadOnlyList<ReviewFinding> Findings { get; init; }

    /// <summary>The <c>--code</c> filter applied to <see cref="Findings"/>; omitted without one.</summary>
    public ReviewFilter? Filter { get; init; }

    /// <summary>
    /// Whether coverage is incomplete or a reported finding is an error. Findings a filter left
    /// out do not count: the caller asked about the listed checks only.
    /// </summary>
    [JsonIgnore]
    public bool HasFailures => !Coverage.Complete || Findings.Any(static finding =>
        string.Equals(finding.Severity, ReviewSeverities.Error, StringComparison.Ordinal));
}

/// <summary>The check codes a review reported, and how many findings of other checks it left out.</summary>
public sealed record ReviewFilter
{
    public required IReadOnlyList<string> Codes { get; init; }

    public required int OmittedFindings { get; init; }
}

/// <summary>Bounded coverage of the renderer-owned evidence inventory.</summary>
public sealed record ReviewCoverage
{
    public required int MaxItems { get; init; }

    public required int DiscoveredItems { get; init; }

    public required int ReportedItems { get; init; }

    public required bool Truncated { get; init; }

    public required int ExpectedItems { get; init; }

    public required int RenderedItems { get; init; }

    public required int OmittedItems { get; init; }

    public required bool Complete { get; init; }

    public required IReadOnlyList<ReviewCoverageMetric> Metrics { get; init; }
}

/// <summary>One product-owned structural or semantic review observation.</summary>
public sealed record ReviewFinding
{
    public required string Code { get; init; }

    public required string Severity { get; init; }

    public required string Message { get; init; }

    public string? Location { get; init; }

    public string? Hint { get; init; }

    /// <summary>Reported artifact paths that provide visual evidence for this finding.</summary>
    public IReadOnlyList<string>? Evidence { get; init; }
}

/// <summary>One deterministic product-owned coverage measurement.</summary>
public sealed record ReviewCoverageMetric
{
    public required string Name { get; init; }

    public required long Value { get; init; }

    public string? Unit { get; init; }
}

/// <summary>One safe relative path in a review evidence bundle.</summary>
public sealed record ReviewArtifact
{
    public required int Sequence { get; init; }

    public required string Path { get; init; }

    public required string Role { get; init; }

    public required string MediaType { get; init; }

    public string? Scope { get; init; }

    public string? Label { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}
