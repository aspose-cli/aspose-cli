using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf create</c>.</summary>
public sealed record PdfCreateResult() : EngineResultEnvelope("create-result", 2)
{
    /// <summary>The command that wrote the PDF: always <c>create</c>.</summary>
    [JsonPropertyOrder(-50)]
    [AllowedValues("create")]
    public required string Action { get; init; }

    /// <summary>The PDF written.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The source files the PDF was created from, in order.</summary>
    public required IReadOnlyList<InputInfo> Inputs { get; init; }
}
