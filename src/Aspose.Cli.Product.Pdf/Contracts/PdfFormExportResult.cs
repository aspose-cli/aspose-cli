using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf extract --what forms</c>, which exports the form data to a file.</summary>
public sealed record PdfFormExportResult() : EngineResultEnvelope("form-export-result", 2)
{
    /// <summary>The PDF whose form data was exported.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The form-data file written: JSON, FDF or XFDF.</summary>
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
}
