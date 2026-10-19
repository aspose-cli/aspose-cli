using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for bounded asset extraction.</summary>
public sealed record WordsExtractRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public required string What { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    /// <summary>Whether each table's CSV starts with a UTF-8 byte order mark.</summary>
    public bool ByteOrderMark { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>What <c>words extract --what</c> extracts.</summary>
public static class WordsExtractTargets
{
    public const string Images = "images";
    public const string Comments = "comments";
    public const string Text = "text";
    public const string Tables = "tables";

    public static IReadOnlyList<string> Names { get; } = [Images, Comments, Text, Tables];
}
