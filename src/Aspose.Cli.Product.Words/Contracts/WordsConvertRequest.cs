using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for document conversion.</summary>
public sealed record WordsConvertRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    /// <summary>The document as a command a hint names opens it, with the variable of its password; null names it by <see cref="Input"/>.</summary>
    public ContinuationInput? Continuation { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }
    public Secret? EncryptPassword { get; init; }
}
