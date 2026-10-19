using Aspose.Cells;
using Aspose.Cells.Charts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The chart families and the insert_pivot functions as the engine names them, which a read
/// reports and the chart and pivot operations write.
/// </summary>
internal static class ChartPivotVocabulary
{
    /// <summary>
    /// Pie-family test (pie, 3-D/exploded/bar-of-pie variants, doughnut):
    /// the axis-less charts whose slices live in one series' points. Matched
    /// by enum-name family because set_chart can meet any of the ~75
    /// engine chart types in an existing workbook, not just the six the
    /// vocabulary creates.
    /// </summary>
    internal static bool IsPieFamily(ChartType type) =>
        IsFamily(type, "Pie") || IsFamily(type, "Doughnut");

    internal static bool IsFamily(ChartType type, string prefix) =>
        type.ToString().StartsWith(prefix, StringComparison.Ordinal);

    /// <summary>The insert_pivot function of an engine aggregation, or null for one outside the vocabulary.</summary>
    internal static string? FromFunction(ConsolidationFunction function) =>
        Functions.FirstOrDefault(name => ToFunction(name) == function);

    private static readonly string[] Functions =
        [CellsPivotFunctions.Sum, CellsPivotFunctions.Count, CellsPivotFunctions.Average, CellsPivotFunctions.Max, CellsPivotFunctions.Min];

    internal static ConsolidationFunction ToFunction(string function) => function switch
    {
        CellsPivotFunctions.Sum => ConsolidationFunction.Sum,
        CellsPivotFunctions.Count => ConsolidationFunction.Count,
        CellsPivotFunctions.Average => ConsolidationFunction.Average,
        CellsPivotFunctions.Max => ConsolidationFunction.Max,
        CellsPivotFunctions.Min => ConsolidationFunction.Min,
        _ => throw new ArgumentOutOfRangeException(
            nameof(function), function, "Pivot function is missing from the engine mapper."),
    };
}
