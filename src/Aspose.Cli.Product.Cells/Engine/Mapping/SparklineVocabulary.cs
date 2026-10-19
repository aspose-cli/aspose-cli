using Aspose.Cells.Charts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The sparkline types as the engine names them, which a read reports and add_sparkline writes.
/// </summary>
internal static class SparklineVocabulary
{
    /// <summary>
    /// Wire name to the engine's <see cref="SparklineType"/>. Excel calls the
    /// third type "win/loss"; the engine exposes it as <c>WinLoss</c>.
    /// </summary>
    /// <summary>The wire name of an engine sparkline type, or null for one outside the vocabulary.</summary>
    internal static string? FromType(SparklineType type) =>
        Types.FirstOrDefault(name => ToType(name) == type);

    private static readonly string[] Types = [CellsSparklineTypes.Line, CellsSparklineTypes.Column, CellsSparklineTypes.WinLoss];

    internal static SparklineType ToType(string type) => type switch
    {
        CellsSparklineTypes.Line => SparklineType.Line,
        CellsSparklineTypes.Column => SparklineType.Column,
        CellsSparklineTypes.WinLoss => SparklineType.WinLoss,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Sparkline type is missing from the engine mapper."),
    };
}
