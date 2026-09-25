namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>
/// The named page sizes, in points. A name matches exactly, the same way on the command
/// line, in the ops schema and rules, and in the engine.
/// </summary>
internal static class PdfPageSizes
{
    private static readonly (string Name, double Width, double Height)[] Sizes =
    [
        ("A3", 841.89, 1190.55),
        ("A4", 595.28, 841.89),
        ("Letter", 612, 792),
        ("Legal", 612, 1008),
    ];

    internal static System.Collections.Immutable.ImmutableArray<string> Names { get; } = [.. Sizes.Select(static size => size.Name)];

    /// <summary>Returns the size of a name the command line or ops validation already admitted.</summary>
    internal static (double Width, double Height) Dimensions(string name)
    {
        foreach ((string known, double width, double height) in Sizes)
        {
            if (string.Equals(known, name, StringComparison.Ordinal))
            {
                return (width, height);
            }
        }
        throw new ArgumentOutOfRangeException(nameof(name), name, "The page size was not validated.");
    }
}
