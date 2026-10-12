namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells compare</c>.</summary>
public sealed record CellsCompareRequest
{
    /// <summary>The baseline workbook.</summary>
    public required string Left { get; init; }

    /// <summary>The candidate workbook compared against the baseline.</summary>
    public required string Right { get; init; }

    /// <summary>What to compare.</summary>
    public CellsCompareScope Scope { get; init; } = CellsCompareScope.Formulas;

    /// <summary>Maximum number of differing cells to list before truncating.</summary>
    public int MaxDiffs { get; init; } = 1000;

    /// <summary>Password for the left (baseline) file.</summary>
    public Secret? LeftPassword { get; init; }

    /// <summary>Password for the right (candidate) file.</summary>
    public Secret? RightPassword { get; init; }
}

/// <summary>What <c>cells compare</c> compares.</summary>
public enum CellsCompareScope
{
    /// <summary>Cell values only.</summary>
    Values,

    /// <summary>Cell values and formulas.</summary>
    Formulas,
}
