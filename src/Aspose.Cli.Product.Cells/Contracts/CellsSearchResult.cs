namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Result of <c>cells search</c>: the matches in the text rows of the workbook's objects.</summary>
public sealed record CellsSearchResult : TableSearchResult
{
    /// <summary>States what the search found.</summary>
    internal CellsSearchResult(TableSearchOutcome found)
        : base("search-result", found)
    {
    }
}
