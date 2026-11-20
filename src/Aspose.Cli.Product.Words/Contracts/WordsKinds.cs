namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>The kinds of the Words table that read lists only when --kind names them.</summary>
internal static class WordsKinds
{
    internal static IReadOnlyList<string> ListedWhenNamed { get; } =
        ["paragraph", "run", "field", "shape", "row", "cell", "bookmark", "style", "listLevel", "revision"];
}
