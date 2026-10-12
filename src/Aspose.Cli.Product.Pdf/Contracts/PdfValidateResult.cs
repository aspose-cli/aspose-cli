using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf validate</c>, a PDF/A conformance check.</summary>
public sealed record PdfValidateResult() : EngineResultEnvelope("validate-result", 2)
{
    /// <summary>The PDF checked.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The PDF/A profile checked against.</summary>
    [AllowedValues("pdfa-1b", "pdfa-2b", "pdfa-3b")]
    public required string Profile { get; init; }

    /// <summary>Whether the file conforms to the profile.</summary>
    public required bool Valid { get; init; }

    /// <summary>The first compliance issues; a <c>LIST_TRUNCATED</c> warning names the total when there are more.</summary>
    [MaxItems(100)]
    public required IReadOnlyList<string> Issues { get; init; }
}
