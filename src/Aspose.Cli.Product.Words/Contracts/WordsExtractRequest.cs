using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>One <c>words extract</c>: the bytes of the images a document stores.</summary>
public sealed record WordsExtractRequest
{
    /// <summary>The document.</summary>
    public required string Input { get; init; }

    /// <summary>What to extract, one of <see cref="WordsExtractKinds.All"/>.</summary>
    public required string What { get; init; }

    /// <summary>The directory the images go to.</summary>
    public required ResolvedDirectory Output { get; init; }

    /// <summary>The password that opens the input.</summary>
    public Secret? Password { get; init; }
}

/// <summary>What <c>words extract</c> writes.</summary>
public static class WordsExtractKinds
{
    /// <summary>The bytes of each image the document stores.</summary>
    public const string Images = "images";

    /// <summary>Every kind.</summary>
    public static IReadOnlyList<string> All { get; } = [Images];
}
