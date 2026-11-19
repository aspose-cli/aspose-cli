using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for creating a presentation.</summary>
public sealed record SlidesCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The presentation saved as the new one (<c>--template</c>), or null for the engine's new presentation.</summary>
    public SourceFile? Template { get; init; }

    public Secret? EncryptPassword { get; init; }
}
