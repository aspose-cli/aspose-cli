using System.Text.Json.Serialization;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// Result of <c>aspose-cli review &lt;file&gt;</c>: a portable, static evidence bundle, and the
/// <c>review.json</c> manifest written into the new evidence directory.
/// </summary>
public sealed record ReviewResult() : EngineResultEnvelope("review", 2), IPartialOutcome
{
    /// <summary>Product that reviewed the document.</summary>
    [Pattern("^[a-z0-9][a-z0-9-]*$")]
    public required string Product { get; init; }

    /// <summary>Absolute path of the reviewed document.</summary>
    public required string Input { get; init; }

    /// <summary>Absolute path of the new evidence directory.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Absolute path of the bundle's <c>index.html</c>, which shows the evidence to people.</summary>
    public required string Index { get; init; }

    /// <summary>Absolute path of the bundle's <c>review.json</c> manifest.</summary>
    public required string Manifest { get; init; }

    /// <summary>View the evidence was rendered in.</summary>
    [MinLength(1)]
    public required string View { get; init; }

    /// <summary>Detected format id of the reviewed document.</summary>
    [MinLength(1)]
    public required string SourceFormat { get; init; }

    /// <summary>Size of the reviewed document in bytes.</summary>
    [Minimum(0)]
    public required long SourceSizeBytes { get; init; }

    /// <summary>Whether the reviewed document is encrypted.</summary>
    public required bool SourceEncrypted { get; init; }

    /// <summary>Whether the rendered evidence must be looked at, because the findings alone cannot judge the document.</summary>
    public required bool VisualInspectionRequired { get; init; }

    /// <summary>How much of the document the evidence covers.</summary>
    public required ReviewCoverage Coverage { get; init; }

    /// <summary>Every file of the bundle, in sequence order.</summary>
    public required IReadOnlyList<ReviewArtifact> Artifacts { get; init; }

    /// <summary>What the product's checks found, ordered by code.</summary>
    public required IReadOnlyList<ReviewFinding> Findings { get; init; }

    /// <summary>
    /// The <c>--code</c> filter applied to <see cref="Findings"/>; omitted without one. Errors of
    /// checks outside the filter do not fail the review.
    /// </summary>
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
    /// <summary>The check codes <c>review --code</c> named.</summary>
    [UniqueItems]
    [MinItems(1)]
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public required IReadOnlyList<string> Codes { get; init; }

    /// <summary>How many findings of other checks the filter left out.</summary>
    [Minimum(0)]
    public required int OmittedFindingCount { get; init; }
}

/// <summary>Bounded coverage of the renderer-owned evidence inventory.</summary>
public sealed record ReviewCoverage
{
    /// <summary>Most items the review reports.</summary>
    [Minimum(1)]
    public required int MaxItemCount { get; init; }

    /// <summary>Items the product found in the document.</summary>
    [Minimum(0)]
    public required int DiscoveredItemCount { get; init; }

    /// <summary>Items the review reports, at most <c>maxItemCount</c>.</summary>
    [Minimum(0)]
    public required int ReportedItemCount { get; init; }

    /// <summary>Whether items were left out to stay within <c>maxItemCount</c>.</summary>
    public required bool Truncated { get; init; }

    /// <summary>Parts the view was expected to render.</summary>
    [Minimum(0)]
    public required int ExpectedItemCount { get; init; }

    /// <summary>Parts the view rendered.</summary>
    [Minimum(0)]
    public required int RenderedItemCount { get; init; }

    /// <summary>Parts the view left out.</summary>
    [Minimum(0)]
    public required int OmittedItemCount { get; init; }

    /// <summary>Whether the evidence covers the whole document; an incomplete review exits 8.</summary>
    public required bool Complete { get; init; }

    /// <summary>Product-owned coverage measurements.</summary>
    public required IReadOnlyList<ReviewCoverageMetric> Metrics { get; init; }
}

/// <summary>One product-owned structural or semantic review observation.</summary>
public sealed record ReviewFinding
{
    /// <summary>The code of the check that found it.</summary>
    [Pattern("^[A-Z][A-Z0-9_]*$")]
    public required string Code { get; init; }

    /// <summary>The check's severity; <c>error</c> fails the review.</summary>
    [AllowedValues(typeof(ReviewSeverities))]
    public required string Severity { get; init; }

    /// <summary>What was found.</summary>
    [MinLength(1)]
    public required string Message { get; init; }

    /// <summary>Where, in words people read, for example <c>page 3</c>.</summary>
    [MinLength(1)]
    public string? Location { get; init; }

    /// <summary>How to resolve it.</summary>
    [MinLength(1)]
    public string? Hint { get; init; }

    /// <summary>Reported artifact paths that provide visual evidence for this finding.</summary>
    [UniqueItems]
    [MinItems(1)]
    [Pattern("^artifacts/.+")]
    public IReadOnlyList<string>? Evidence { get; init; }

    /// <summary>
    /// Id of the view part the finding is about, for example <c>page-3</c>; the review writer
    /// turns it into <see cref="Evidence"/>. Without one, the finding concerns the whole document.
    /// </summary>
    [JsonIgnore]
    public string? Part { get; init; }
}

/// <summary>One deterministic product-owned coverage measurement.</summary>
public sealed record ReviewCoverageMetric
{
    /// <summary>What is measured, e.g. <c>pages</c>.</summary>
    [Pattern("^[a-z][A-Za-z0-9.-]*$")]
    public required string Name { get; init; }

    /// <summary>The measured amount.</summary>
    [Minimum(0)]
    public required long Value { get; init; }

    /// <summary>The unit of the amount, when it has one.</summary>
    [MinLength(1)]
    public string? Unit { get; init; }

    /// <summary>A measurement of <paramref name="value"/> <paramref name="unit"/> named <paramref name="name"/>.</summary>
    public static ReviewCoverageMetric Of(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}

/// <summary>One safe relative path in a review evidence bundle.</summary>
public sealed record ReviewArtifact
{
    /// <summary>The artifact's position in the bundle.</summary>
    [Minimum(0)]
    public required int Sequence { get; init; }

    /// <summary>Path relative to the evidence directory.</summary>
    [Pattern("^(index\\.html|review\\.json|artifacts/.+)$")]
    public required string Path { get; init; }

    /// <summary>
    /// What the artifact is: the bundle's <c>index</c> page, its <c>manifest</c>, the view's
    /// <c>entry</c> manifest, or a rendered part that is <c>evidence</c>.
    /// </summary>
    [AllowedValues("index", "manifest", "evidence", "entry")]
    public required string Role { get; init; }

    /// <summary>The artifact's media type, e.g. <c>image/png</c>.</summary>
    [MinLength(1)]
    public required string MediaType { get; init; }

    /// <summary>The view the artifact belongs to; omitted for the bundle's own files.</summary>
    [MinLength(1)]
    public string? Scope { get; init; }

    /// <summary>Label people see for the artifact.</summary>
    [MinLength(1)]
    public string? Label { get; init; }

    /// <summary>Image width in pixels; omitted when the artifact is not an image.</summary>
    [Minimum(1)]
    public int? Width { get; init; }

    /// <summary>Image height in pixels; omitted when the artifact is not an image.</summary>
    [Minimum(1)]
    public int? Height { get; init; }
}
