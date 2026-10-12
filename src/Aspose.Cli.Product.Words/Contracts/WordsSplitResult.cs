namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words split</c>: the parts the document was split into, in document order.</summary>
public sealed record WordsSplitResult() : EngineResultEnvelope("split-result", 2)
{
    /// <summary>The split document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>The written parts, in document order.</summary>
    public required IReadOnlyList<WordsPartOutput> Outputs { get; init; }
}

/// <summary>
/// One written part and what of the document it holds, in the unit <c>--by</c> divides by: its
/// <c>section</c> by sections, its <c>pages</c> by pages, or its <c>blocks</c> at each Heading 1.
/// </summary>
public sealed record WordsPartOutput : OutputItem
{
    /// <summary>Describes the file of a part.</summary>
    /// <param name="file">The published part, as <c>FileObjects.Output</c> describes it.</param>
    public WordsPartOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The 1-based section the part holds; present with <c>--by sections</c>.</summary>
    [Minimum(1)]
    public int? Section { get; init; }

    /// <summary>The 1-based pages the part holds, such as <c>3</c> or <c>1-3,8</c>; present with <c>--by pages</c>.</summary>
    [MinLength(1)]
    public string? Pages { get; init; }

    /// <summary>The 1-based blocks the part holds, such as <c>1-12</c>; present with <c>--by heading1</c>.</summary>
    [MinLength(1)]
    public string? Blocks { get; init; }
}
