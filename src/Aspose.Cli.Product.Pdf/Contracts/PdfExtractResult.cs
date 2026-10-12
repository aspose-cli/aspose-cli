using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf extract</c>: the files it wrote into the output directory.</summary>
public sealed record PdfExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The PDF extracted from.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted, as <c>--what</c> named it.</summary>
    [AllowedValues(typeof(PdfExtractKinds))]
    public required string What { get; init; }

    /// <summary>
    /// Each file written into the output directory, in order: an image or table with the page it
    /// comes from, the text of the selected pages, an attachment under its own file name, or the
    /// form data.
    /// </summary>
    public required IReadOnlyList<PdfPageOutput> Outputs { get; init; }
}
