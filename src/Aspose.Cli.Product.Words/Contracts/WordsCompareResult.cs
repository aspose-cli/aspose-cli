using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words compare</c>: the redline Document.Compare made of the original, and its revision count.</summary>
public sealed record WordsCompareResult() : EngineResultEnvelope("compare-result")
{
    /// <summary>The original document.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Left { get; init; }

    /// <summary>The changed document.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Right { get; init; }

    /// <summary>The redline written.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>The redline's revision count, Document.Revisions.Count after the comparison.</summary>
    [Minimum(0)]
    public required int RevisionCount { get; init; }

    /// <summary>The warnings Aspose.Words reported, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
