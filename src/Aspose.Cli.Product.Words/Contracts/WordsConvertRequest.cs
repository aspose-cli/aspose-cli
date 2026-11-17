using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

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
