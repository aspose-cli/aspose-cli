using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>One <c>words create</c>: the engine's new document, or a template saved as a new one.</summary>
public sealed record WordsCreateRequest
{
    /// <summary>Where the document goes, in its format.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The template, or null for the engine's new document.</summary>
    public SourceFile? Template { get; init; }

    /// <summary>The password the output opens with, or null.</summary>
    public Secret? EncryptPassword { get; init; }
}
