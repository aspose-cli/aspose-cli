namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Result of <c>slides search</c>: the matches in the text rows of the presentation's objects.</summary>
public sealed record SlidesSearchResult : TableSearchResult
{
    /// <summary>States what the search found.</summary>
    internal SlidesSearchResult(TableSearchOutcome found)
        : base("search-result", found)
    {
    }
}
