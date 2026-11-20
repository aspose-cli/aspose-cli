using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf create</c>: the PDF written and what it was created from.</summary>
public sealed record PdfCreateResult() : EngineResultEnvelope("create-result")
{
    /// <summary>The created PDF.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The template the PDF was saved from; empty for the engine's new document.</summary>
    public required IReadOnlyList<SourceInput> Inputs { get; init; }
}
