namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words extract</c>: the files written for the images, comments, text or tables extracted.</summary>
public sealed record WordsExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The document extracted from.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted, as <c>--what</c> named it.</summary>
    [AllowedValues(typeof(WordsExtractTargets))]
    public required string What { get; init; }

    /// <summary>Each file written into the output directory, in document order.</summary>
    public required IReadOnlyList<WordsExtractedOutput> Outputs { get; init; }
}

/// <summary>One extracted file and the block it comes from when it holds one image or table.</summary>
public sealed record WordsExtractedOutput : OutputItem
{
    /// <summary>Describes the extracted <paramref name="file"/>.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Companion</c> describes it.</param>
    public WordsExtractedOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The 1-based block the image or table comes from; absent for the comments or text of the whole document.</summary>
    [Minimum(1)]
    public int? Block { get; init; }
}
