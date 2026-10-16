using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for creating one document.</summary>
public sealed record WordsCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    /// <summary>The Markdown the body is made from (<c>--from-markdown</c>), or null.</summary>
    public SourceFile? Markdown { get; init; }

    /// <summary>The UTF-8 text the body is made from, one paragraph per line (<c>--from-text</c>), or null.</summary>
    public SourceFile? Text { get; init; }

    /// <summary>The document whose styles, page setup, headers and footers are used (<c>--template</c>), or null.</summary>
    public SourceFile? Template { get; init; }

    public string? Title { get; init; }
    public Secret? EncryptPassword { get; init; }
}
