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

    /// <summary>How a numbered list numbers the paragraph's level; absent for bullets and for a style set_list does not name.</summary>
    [AllowedValues(typeof(WordsListNumberStyles))]
    public string? NumberStyle { get; init; }

    /// <summary>The number of the first item at the paragraph's level of a numbered list; absent for bullets.</summary>
    [Minimum(0)]
    public int? Start { get; init; }
}
