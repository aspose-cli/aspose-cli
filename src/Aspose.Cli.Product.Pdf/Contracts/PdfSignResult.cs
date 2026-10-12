using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf sign</c>: the signature applied and read back from the saved file.</summary>
public sealed record PdfSignResult() : EngineResultEnvelope("sign-result", 2)
{
    /// <summary>The PDF signed.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The signed PDF written.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }

    /// <summary>The signature field as the saved file reports it.</summary>
    public required PdfSignatureInfo Signature { get; init; }

    /// <summary>Whether the signature has a visible appearance on the page.</summary>
    public required bool Visible { get; init; }

    /// <summary>The 1-based page of the signature field.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>Where a visible signature lies on its page; absent for an invisible one.</summary>
    public PdfRect? Rect { get; init; }
}
