using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words create</c>: the document written and what it was created from.</summary>
public sealed record WordsCreateResult() : EngineResultEnvelope("create-result")
{
    /// <summary>The created document.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The template the document was saved from; empty for the engine's new document.</summary>
    public required IReadOnlyList<SourceInput> Inputs { get; init; }

    /// <summary>The warnings Aspose.Words reported, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}
