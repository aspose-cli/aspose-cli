namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for a budgeted canonical block read.</summary>
public sealed record WordsReadRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public PageRange? Blocks { get; init; }
    public int? Section { get; init; }
    public string Scope { get; init; } = "text";
    /// <summary>Returned document-content character budget, including repeated projections.</summary>
    public int MaxCharacters { get; init; } = 20_000;
    public int MaxBlocks { get; init; } = 200;
    public Secret? Password { get; init; }
}

/// <summary>Words read scopes.</summary>
public static class WordsReadScopes
{
    public const string Text = "text";
    public const string Outline = "outline";
    public const string Full = "full";

    public static IReadOnlyList<string> All { get; } = [Text, Outline, Full];
}
