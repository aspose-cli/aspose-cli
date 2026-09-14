using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for structural Words inspection.</summary>
public sealed record DocumentInfoRequest
{
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for a budgeted canonical block read.</summary>
public sealed record DocumentReadRequest
{
    public PageRange? Blocks { get; init; }
    public int? Section { get; init; }
    public string Scope { get; init; } = "text";
    public int MaxCharacters { get; init; } = 20_000;
    public int MaxBlocks { get; init; } = 200;
    public string? Password { get; init; }
}

/// <summary>Options for document conversion.</summary>
public sealed record WordsConvertRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Pages { get; init; }
    public string? Password { get; init; }
    public string? EncryptPassword { get; init; }
}

/// <summary>Options for page rendering.</summary>
public sealed record WordsRenderRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Pages { get; init; }
    public bool AllPages { get; init; }
    public int Dpi { get; init; } = 192;
    public string? Password { get; init; }
}

/// <summary>Options for an HTML Fixed preview snapshot.</summary>
public sealed record WordsPreviewRequest
{
    public string? Password { get; init; }
}

/// <summary>Options for creating one document.</summary>
public sealed record NewDocumentRequest
{
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? MarkdownPath { get; init; }
    public string? TextPath { get; init; }
    public string? TemplatePath { get; init; }
    public string? Title { get; init; }
    public string? EncryptPassword { get; init; }
}

/// <summary>Options for an atomic Words edit batch.</summary>
public sealed record WordsEditRequest
{
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public bool OverwriteArtifacts { get; init; }
    public string? BackupPath { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public bool Verify { get; init; }
    public bool TrackChanges { get; init; }
    public string? Author { get; init; }
    public string? Password { get; init; }
    public string? EncryptPassword { get; init; }
    public IReadOnlyDictionary<int, string>? OpSecrets { get; init; }
}

/// <summary>Options for semantic document comparison.</summary>
public sealed record WordsCompareRequest
{
    public bool IgnoreFormatting { get; init; }
    public string? OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? LeftPassword { get; init; }
    public string? RightPassword { get; init; }
}

/// <summary>Options for bounded document search.</summary>
public sealed record WordsSearchRequest
{
    public required string Pattern { get; init; }
    public bool Regex { get; init; }
    public bool CaseSensitive { get; init; }
    public string Scope { get; init; } = "body";
    public int MaxHits { get; init; } = 100;
    public string? Password { get; init; }
}

/// <summary>Options for transactional document splitting.</summary>
public sealed record WordsSplitRequest
{
    public required string By { get; init; }
    public PageRange? Pages { get; init; }
    public required string OutputDirectory { get; init; }
    public bool Overwrite { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for bounded asset extraction.</summary>
public sealed record WordsExtractRequest
{
    public required string What { get; init; }
    public required string OutputDirectory { get; init; }
    public string? Password { get; init; }
}

/// <summary>Words read scopes.</summary>
public static class DocumentReadScopes
{
    public static IReadOnlyList<string> All { get; } = ["text", "outline", "full"];
}
