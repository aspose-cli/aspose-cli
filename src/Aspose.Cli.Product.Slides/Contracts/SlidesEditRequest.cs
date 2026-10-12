using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for one atomic presentation edit batch.</summary>
public sealed record SlidesEditRequest
{
    /// <summary>The presentation the command edits.</summary>
    public required string Input { get; init; }
    /// <summary>The validated operation batch.</summary>
    public required SlidesOpsBatch Batch { get; init; }
    /// <summary>The resolved output: its format, path, overwrite permission and in-place backup.</summary>
    public required ResolvedOutput Output { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public Secret? Password { get; init; }
    public Secret? EncryptPassword { get; init; }
    /// <summary>The secrets the operations name by environment variable, by variable name.</summary>
    public IReadOnlyDictionary<string, Secret>? OpSecrets { get; init; }
}
