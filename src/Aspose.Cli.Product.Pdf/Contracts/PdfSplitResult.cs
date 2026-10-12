using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf split</c>, which writes every part or none.</summary>
public sealed record PdfSplitResult() : EngineResultEnvelope("split-result", 2)
{
    /// <summary>The PDF split.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>Each part written, in order.</summary>
    public required IReadOnlyList<PdfSplitOutput> Outputs { get; init; }
}

/// <summary>One part of a split PDF.</summary>
public sealed record PdfSplitOutput
{
    /// <summary>The part's 1-based position.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The part's pages as range text, such as <c>1-3</c> or <c>1-3,7</c>.</summary>
    public required string Pages { get; init; }

    /// <summary>The title of the bookmark the part starts at, with <c>--by bookmarks</c>.</summary>
    public string? Bookmark { get; init; }

    /// <summary>The PDF written for the part.</summary>
    public required OutputInfo Output { get; init; }
}
