using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for semantic document comparison.</summary>
public sealed record WordsCompareRequest
{
    /// <summary>The unit a change is marked in, by default.</summary>
    internal const string DefaultGranularity = "word";

    /// <summary>The original document.</summary>
    public required string Left { get; init; }
    /// <summary>The changed document.</summary>
    public required string Right { get; init; }
    public bool IgnoreFormatting { get; init; }
    /// <summary>The unit a change is marked in: <c>word</c> or <c>char</c>.</summary>
    public string Granularity { get; init; } = DefaultGranularity;
    /// <summary>The author of the redline's revisions; null records <c>Aspose CLI</c>.</summary>
    public string? Author { get; init; }
    /// <summary>The resolved redline output, or null when the comparison writes none.</summary>
    public ResolvedOutput? Output { get; init; }
    public Secret? LeftPassword { get; init; }
    public Secret? RightPassword { get; init; }
}
