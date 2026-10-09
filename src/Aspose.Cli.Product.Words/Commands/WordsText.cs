using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

/// <summary>Table formatting several Words commands share.</summary>
internal static class WordsText
{
    /// <summary>A block number; an item without a body block, such as one in a header, shows a dash.</summary>
    public static string Block(int? block) => block is { } value ? TableText.Int(value) : "-";
}
