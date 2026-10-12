using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf split</c>, which writes every part or none.</summary>
public sealed record PdfSplitResult() : EngineResultEnvelope("split-result", 2)
{
    /// <summary>The PDF split.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>Each part written, in order; its number is the one in its file name.</summary>
    public required IReadOnlyList<PdfPartOutput> Outputs { get; init; }
}

/// <summary>The PDF written for one part of a split, and the pages of the source it holds.</summary>
public sealed record PdfPartOutput : OutputItem
{
    /// <summary>Copies the facts of the written <paramref name="file"/>.</summary>
    public PdfPartOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The source pages the part holds, as range text such as <c>1-3</c> or <c>1-3,7</c>.</summary>
    [Pattern("^[1-9][0-9]*(-[1-9][0-9]*)?(,[1-9][0-9]*(-[1-9][0-9]*)?)*$")]
    public required string Pages { get; init; }
}
