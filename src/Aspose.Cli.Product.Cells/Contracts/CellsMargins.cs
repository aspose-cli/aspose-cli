using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Page margins in points (72 per inch); only the sides present are applied, and at least one
/// is set. A read reports every side.
/// </summary>
[MinProperties(1)]
public sealed record CellsMargins
{
    /// <summary>The top margin.</summary>
    [Minimum(0)] public double? Top { get; init; }

    /// <summary>The bottom margin.</summary>
    [Minimum(0)] public double? Bottom { get; init; }

    /// <summary>The left margin.</summary>
    [Minimum(0)] public double? Left { get; init; }

    /// <summary>The right margin.</summary>
    [Minimum(0)] public double? Right { get; init; }

    /// <summary>The distance from the top of the page to the header.</summary>
    [Minimum(0)] public double? Header { get; init; }

    /// <summary>The distance from the bottom of the page to the footer.</summary>
    [Minimum(0)] public double? Footer { get; init; }
}
