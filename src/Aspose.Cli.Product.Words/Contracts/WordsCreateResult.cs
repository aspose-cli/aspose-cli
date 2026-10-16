using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words create</c>: the document written and what it was created from.</summary>
public sealed record WordsCreateResult() : EngineResultEnvelope("create-result", 2)
{
    /// <summary>The created document.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The files the document was created from, the template and the Markdown or text, in command-line order; empty for a blank document.</summary>
    public required IReadOnlyList<SourceInput> Inputs { get; init; }
}
