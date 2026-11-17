using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for presentation conversion.</summary>
public sealed record SlidesConvertRequest
{
    /// <summary>The presentation the command reads.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Slides { get; init; }
    public Secret? Password { get; init; }
    public Secret? EncryptPassword { get; init; }
}
