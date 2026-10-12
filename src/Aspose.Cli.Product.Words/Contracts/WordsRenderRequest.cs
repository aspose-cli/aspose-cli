using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Options for page rendering.</summary>
public sealed record WordsRenderRequest
{
    /// <summary>The document to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    /// <summary>The pages to render, or null for every page.</summary>
    public PageRange? Pages { get; init; }
    public int Dpi { get; init; } = RenderPixelGuard.DefaultDpi;
    public Secret? Password { get; init; }
}
