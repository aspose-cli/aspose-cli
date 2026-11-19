namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// The names of the table's kinds a command's help needs before any command runs, written
/// without the engine's types, so that defining the commands loads no engine; a product test
/// keeps them equal to the table.
/// </summary>
internal static class CellsKinds
{
    /// <summary>The kinds read lists only when --kind names them, whose count grows with the data.</summary>
    internal static IReadOnlyList<string> ListedWhenNamed { get; } = ["row", "column", "cell", "style", "point"];
}
