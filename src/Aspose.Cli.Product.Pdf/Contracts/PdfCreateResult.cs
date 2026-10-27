using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf create</c>.</summary>
public sealed record PdfCreateResult() : EngineResultEnvelope("create-result")
{
    /// <summary>The PDF written.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The source files the PDF was created from, in command-line order: each image of <c>--from-images</c>, or the one HTML, Markdown or text file.</summary>
    [MinItems(1)]
    public required IReadOnlyList<SourceInput> Inputs { get; init; }
}
