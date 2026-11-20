using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf sign</c>: the signature fields the saved file holds signed that the input did not.</summary>
public sealed record PdfSignResult() : EngineResultEnvelope("sign-result")
{
    /// <summary>The PDF signed.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The signed PDF written.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The signature fields the saved file holds signed that the input did not, read back from it.</summary>
    public required IReadOnlyList<PdfSignatureInfo> Signatures { get; init; }
}
