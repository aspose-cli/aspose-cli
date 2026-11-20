namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Result of <c>words extract</c>: one file per image the document stores, in document order.</summary>
public sealed record WordsExtractResult() : EngineResultEnvelope("extract-result")
{
    /// <summary>The document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>What was extracted.</summary>
    [AllowedValues(typeof(WordsExtractKinds))]
    public required string What { get; init; }

    /// <summary>The image files, in the order of the document's shapes.</summary>
    public required IReadOnlyList<WordsExtractedOutput> Outputs { get; init; }

    /// <summary>The warnings Aspose.Words reported, as it reported them; omitted when there were none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }
}

/// <summary>One image file, named with the extension of its type, or none for a type the engine does not know.</summary>
public sealed record WordsExtractedOutput : OutputItem
{
    /// <summary>Wraps a written file.</summary>
    public WordsExtractedOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The image's ImageData.ImageType, a member name of the engine's ImageType, such as Png or Unknown.</summary>
    public required string ImageType { get; init; }
}
