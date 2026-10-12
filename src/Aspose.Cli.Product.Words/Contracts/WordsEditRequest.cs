using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Contracts;

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
