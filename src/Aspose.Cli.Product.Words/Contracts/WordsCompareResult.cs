namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>
/// Result of <c>words compare</c>: the revisions a semantic comparison of two documents found,
/// and the redline document when one is written.
/// </summary>
public sealed record WordsCompareResult() : EngineResultEnvelope("compare-result")
{
    /// <summary>The original document.</summary>
    public required InputInfo Left { get; init; }

    /// <summary>The changed document.</summary>
    public required InputInfo Right { get; init; }

    /// <summary>Whether the comparison found no revisions.</summary>
    public required bool Identical { get; init; }

    /// <summary>The revisions the comparison found, counted by kind.</summary>
    public required WordsRevisionCounts Revisions { get; init; }

    /// <summary>The first revisions the comparison found.</summary>
    public required IReadOnlyList<WordsRevisionSample> Samples { get; init; }

    /// <summary>The redline document that tracks every revision; absent when none is written.</summary>
    public OutputInfo? Output { get; init; }
}

/// <summary>Counts by revision kind.</summary>
public sealed record WordsRevisionCounts
{
    /// <summary>The number of insertions.</summary>
    [Minimum(0)]
    public required int InsertionCount { get; init; }

    /// <summary>The number of deletions.</summary>
    [Minimum(0)]
    public required int DeletionCount { get; init; }

    /// <summary>The number of format changes.</summary>
    [Minimum(0)]
    public required int FormatChangeCount { get; init; }

    /// <summary>The number of moves.</summary>
    [Minimum(0)]
    public required int MoveCount { get; init; }
}

/// <summary>One revision the comparison found.</summary>
public sealed record WordsRevisionSample
{
    /// <summary>The kind of change.</summary>
    [AllowedValues(typeof(WordsRevisionKinds))]
    public required string Kind { get; init; }

    /// <summary>The text of the revision's run, cut to 300 characters and an ellipsis; absent for a paragraph mark.</summary>
    public string? Text { get; init; }
}
