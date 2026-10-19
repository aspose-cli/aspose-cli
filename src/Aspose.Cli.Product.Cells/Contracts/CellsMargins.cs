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
    [Measured(MeasureUnit.Points), Minimum(0)] public double? Top { get; init; }

    /// <summary>The bottom margin.</summary>
    [Measured(MeasureUnit.Points), Minimum(0)] public double? Bottom { get; init; }

    /// <summary>The left margin.</summary>
    [Measured(MeasureUnit.Points), Minimum(0)] public double? Left { get; init; }

    /// <summary>The right margin.</summary>
    [Measured(MeasureUnit.Points), Minimum(0)] public double? Right { get; init; }
}
