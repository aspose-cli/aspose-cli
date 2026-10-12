using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for creating a presentation from a bounded source.</summary>
public sealed record SlidesCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public string? MarkdownPath { get; init; }
    public string? TemplatePath { get; init; }
    /// <summary>The slide size preset, one of <see cref="SlidesSlideSizePresets.All"/>; null keeps the template's size.</summary>
    public string? SlideSize { get; init; }
    public Secret? EncryptPassword { get; init; }
}
