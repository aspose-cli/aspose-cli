using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>One <c>words compare</c>: Document.Compare of two documents, every parameter stated.</summary>
public sealed record WordsCompareRequest
{
    /// <summary>The original document, which the redline is made from.</summary>
    public required string Left { get; init; }

    /// <summary>The changed document.</summary>
    public required string Right { get; init; }

    /// <summary>The author of the revisions.</summary>
    public required string Author { get; init; }

    /// <summary>The date and time of the revisions.</summary>
    public required DateTime DateTime { get; init; }

    /// <summary>CompareOptions.Granularity, by its member name, or null for the engine's default.</summary>
    public string? Granularity { get; init; }

    /// <summary>The CompareOptions members given as true, by their option names; the others keep the engine's defaults.</summary>
    public required IReadOnlyList<string> Ignore { get; init; }

    /// <summary>Where the redline goes.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The password that opens the original.</summary>
    public Secret? LeftPassword { get; init; }

    /// <summary>The password that opens the changed document.</summary>
    public Secret? RightPassword { get; init; }
}
