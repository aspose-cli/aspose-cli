using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for extracting bounded presentation content.</summary>
public sealed record SlidesExtractRequest
{
    /// <summary>The presentation the command reads.</summary>
    public required string Input { get; init; }
    public required string What { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public PageRange? Slides { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Stable presentation extraction families.</summary>
public static class SlidesExtractKinds
{
    public const string Media = "media";
    public const string Notes = "notes";
    public const string Text = "text";
    public static IReadOnlyList<string> All { get; } = [Media, Notes, Text];
}
