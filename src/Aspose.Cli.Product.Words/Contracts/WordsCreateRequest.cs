using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for creating one document.</summary>
public sealed record WordsCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public string? MarkdownPath { get; init; }
    public string? TextPath { get; init; }
    public string? TemplatePath { get; init; }
    public string? Title { get; init; }
    public Secret? EncryptPassword { get; init; }
}
