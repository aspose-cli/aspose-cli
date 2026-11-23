namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words search</c>: the matches in the text rows of the document's objects.</summary>
public sealed record WordsSearchResult : TableSearchResult
{
    /// <summary>States what the search found.</summary>
    internal WordsSearchResult(TableSearchOutcome found)
        : base("search-result", found)
    {
    }
}
