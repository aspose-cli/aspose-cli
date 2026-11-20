using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf validate</c>: what Document.Validate returned for a PDF/A profile, and its log.</summary>
public sealed record PdfValidateResult() : EngineResultEnvelope("validate-result")
{
    /// <summary>The PDF checked.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The PDF/A profile checked against.</summary>
    [AllowedValues("pdfa-1b", "pdfa-2b", "pdfa-3b")]
    public required string Profile { get; init; }

    /// <summary>What Document.Validate returned.</summary>
    public required bool Valid { get; init; }

    /// <summary>The log Document.Validate wrote, as the engine wrote it; last, since it grows with the document.</summary>
    [JsonPropertyOrder(1000)]
    public required string Log { get; init; }
}
