using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells query search</c>.</summary>
public sealed record CellsSearchRequest
{
    /// <summary>The workbook to search.</summary>
    public required string Input { get; init; }

    /// <summary>The validated pattern, hit budget and paging of the search.</summary>
    public required SearchQuery Query { get; init; }

    /// <summary>Where to look.</summary>
    public CellsSearchIn In { get; init; } = CellsSearchIn.Values;

    /// <summary>Restrict to one sheet; all sheets when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }
}

/// <summary>Where <c>cells query search</c> looks for matches.</summary>
public enum CellsSearchIn
{
    /// <summary>Display values only.</summary>
    Values,

    /// <summary>Formulas only.</summary>
    Formulas,

    /// <summary>Values and formulas.</summary>
    Both,
}
