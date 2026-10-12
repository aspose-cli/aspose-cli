using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for rendering one or more slides.</summary>
public sealed record SlidesRenderRequest
{
    /// <summary>The presentation the command reads.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    /// <summary>The slides to render, or null for every slide.</summary>
    public PageRange? Slides { get; init; }
    public int? Dpi { get; init; }
    public int? Width { get; init; }
    public Secret? Password { get; init; }
}
