using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The icon sets as the engine names them, which a read reports and add_conditional_format writes.
/// </summary>
internal static class ConditionalFormatVocabulary
{
    /// <summary>
    /// Wire name to the engine's <see cref="IconSetType"/>. The engine enum
    /// carries digit suffixes and near-duplicates (<c>TrafficLights31</c> vs
    /// <c>TrafficLights32</c>, <c>Symbols3</c> vs <c>Symbols32</c>); the wire
    /// vocabulary picks the five canonical sets (probe-verified names).
    /// </summary>
    /// <summary>The wire name of an engine icon set, or null for one outside the vocabulary.</summary>
    internal static string? FromIconSetType(IconSetType type) =>
        IconSets.FirstOrDefault(name => ToIconSetType(name) == type);

    private static readonly string[] IconSets =
    [
        CellsIconSetNames.Arrows3, CellsIconSetNames.TrafficLights3, CellsIconSetNames.Symbols3,
        CellsIconSetNames.Rating4, CellsIconSetNames.Rating5,
    ];

    internal static IconSetType ToIconSetType(string name) => name switch
    {
        CellsIconSetNames.Arrows3 => IconSetType.Arrows3,
        CellsIconSetNames.TrafficLights3 => IconSetType.TrafficLights31,
        CellsIconSetNames.Symbols3 => IconSetType.Symbols3,
        CellsIconSetNames.Rating4 => IconSetType.Rating4,
        CellsIconSetNames.Rating5 => IconSetType.Rating5,
        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name, "Icon set is missing from the engine mapper."),
    };
}
