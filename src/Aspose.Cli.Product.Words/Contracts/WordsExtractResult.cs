namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words extract</c>: the files written for the images, comments, text or tables extracted.</summary>
public sealed record WordsExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The document extracted from.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted, as <c>--what</c> named it.</summary>
    [AllowedValues(typeof(WordsExtractTargets))]
    public required string What { get; init; }

    /// <summary>The written files.</summary>
    public required IReadOnlyList<WordsExtractedItem> Items { get; init; }
}

/// <summary>One extracted file.</summary>
public sealed record WordsExtractedItem
{
    /// <summary>The written file's path.</summary>
    public required string Path { get; init; }

    /// <summary>What the file holds.</summary>
    [AllowedValues("image", "comments", "text", "table")]
    public required string Kind { get; init; }

    /// <summary>The file's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>The 1-based block the image or table comes from.</summary>
    [Minimum(1)]
    public int? Block { get; init; }
}
