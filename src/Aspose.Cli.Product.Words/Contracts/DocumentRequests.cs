using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for structural Words inspection.</summary>
public sealed record DocumentInfoRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for a budgeted canonical block read.</summary>
public sealed record DocumentReadRequest
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

/// <summary>Options for document conversion.</summary>
public sealed record WordsConvertRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }
    public Secret? EncryptPassword { get; init; }
}

/// <summary>Options for page rendering.</summary>
public sealed record WordsRenderRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Pages { get; init; }
    public bool AllPages { get; init; }
    public int Dpi { get; init; } = 192;
    public Secret? Password { get; init; }
}

/// <summary>Options for creating one document.</summary>
public sealed record NewDocumentRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public string? MarkdownPath { get; init; }
    public string? TextPath { get; init; }
    public string? TemplatePath { get; init; }
    public string? Title { get; init; }
    public Secret? EncryptPassword { get; init; }
}

/// <summary>Options for an atomic Words edit batch.</summary>
public sealed record WordsEditRequest
{
    /// <summary>The document to edit.</summary>
    public required string Input { get; init; }
    /// <summary>The validated operation batch.</summary>
    public required WordsOpsBatch Batch { get; init; }
    /// <summary>The resolved output: its format, path, overwrite permission and in-place backup.</summary>
    public required ResolvedOutput Output { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public bool Verify { get; init; }
    public bool TrackChanges { get; init; }
    public string? Author { get; init; }
    public Secret? Password { get; init; }
    public Secret? EncryptPassword { get; init; }
    public IReadOnlyDictionary<string, Secret>? OpSecrets { get; init; }
}

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

/// <summary>Options for bounded document search; the query's scope is one of <see cref="WordsTextScopes"/>.</summary>
public sealed record WordsSearchRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public required SearchQuery Query { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for transactional document splitting.</summary>
public sealed record WordsSplitRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public required string By { get; init; }
    public PageRange? Pages { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for bounded asset extraction.</summary>
public sealed record WordsExtractRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    public required string What { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Words read scopes.</summary>
public static class DocumentReadScopes
{
    public static IReadOnlyList<string> All { get; } = ["text", "outline", "full"];
}

/// <summary>
/// The text scopes <c>query search</c> and <c>replace_text</c> share: <c>body</c> is the main
/// text without the comments and footnotes it anchors, <c>headersFooters</c> every header and
/// footer, <c>footnotes</c> every footnote and endnote, <c>comments</c> every comment, and
/// <c>all</c> all of them.
/// </summary>
public static class WordsTextScopes
{
    public const string Body = "body";
    public const string HeadersFooters = "headersFooters";
    public const string Footnotes = "footnotes";
    public const string Comments = "comments";
    public const string All = "all";

    public static IReadOnlyList<string> Names { get; } = [Body, HeadersFooters, Footnotes, Comments, All];
}
