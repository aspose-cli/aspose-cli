using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Windowed page text returned by <c>pdf query pages</c>.</summary>
public sealed record PdfReadResult() : WindowedResultEnvelope("read-result", 2)
{
    /// <summary>The PDF read.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>How the text was extracted: <c>plain</c> reading order, or <c>layout</c>, which keeps the page's spacing.</summary>
    [AllowedValues(typeof(PdfReadModes))]
    public required string Mode { get; init; }

    /// <summary>How many pages the document has, whatever <c>--pages</c> selected.</summary>
    [Minimum(0)]
    public required int PageCount { get; init; }

    /// <summary>The text of each page in the window, in page order.</summary>
    public required IReadOnlyList<PdfPageText> Pages { get; init; }

    /// <summary>
    /// The pages of the window that have no extractable text and appear image-dominated, so they
    /// are probably scanned; absent when there are none.
    /// </summary>
    [UniqueItems]
    [Minimum(1)]
    public IReadOnlyList<int>? ScannedPagesSuspected { get; init; }
}

/// <summary>The text of one PDF page.</summary>
public sealed record PdfPageText
{
    /// <summary>The 1-based page number that operations address.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The page's text.</summary>
    public required string Text { get; init; }

    /// <summary>Whether the text was cut to stay within <c>--max-chars</c>.</summary>
    public required bool Truncated { get; init; }
}
