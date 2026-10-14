namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>The list a paragraph belongs to, as set_list sets it.</summary>
public sealed record WordsListData
{
    /// <summary>Whether the list shows bullets or numbers.</summary>
    [AllowedValues("bullet", "number")]
    public required string Kind { get; init; }

    /// <summary>The paragraph's 1-based level in the list.</summary>
    [Minimum(1)]
    public required int Level { get; init; }
}
