using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words create</c>: the document written.</summary>
public sealed record WordsCreateResult() : EngineResultEnvelope("create-result", 2)
{
    /// <summary>The created document.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
}
