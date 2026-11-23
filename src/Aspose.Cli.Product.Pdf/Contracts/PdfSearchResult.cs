namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Result of <c>pdf search</c>: the matches the engine finds in the text of the document's pages.</summary>
public sealed record PdfSearchResult : TableSearchResult
{
    /// <summary>States what the search found.</summary>
    internal PdfSearchResult(TableSearchOutcome found)
        : base("search-result", found)
    {
    }
}
