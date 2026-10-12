namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words split</c>: the parts the document was split into, in document order.</summary>
public sealed record WordsSplitResult() : EngineResultEnvelope("split-result", 2)
{
    /// <summary>The split document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>The written parts, in document order.</summary>
    public required IReadOnlyList<WordsSplitOutput> Outputs { get; init; }
}

/// <summary>One split output.</summary>
public sealed record WordsSplitOutput
{
    /// <summary>The part's 1-based number.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The part's file.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>What of the document the part holds: <c>section-N</c>, <c>page-N</c>, <c>pages-R</c> for the range R of a part of several pages, or <c>blocks-S-E</c>.</summary>
    public string? Source { get; init; }
}
