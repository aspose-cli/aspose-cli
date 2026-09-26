using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for presentation structure inspection.</summary>
public sealed record PresentationInfoRequest
{
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for a bounded presentation read.</summary>
public sealed record PresentationReadRequest
{
    public PageRange? Slides { get; init; }
    public string Scope { get; init; } = PresentationReadScopes.Shapes;
    public bool IncludeNotes { get; init; }
    /// <summary>Returned document-content character budget, including repeated projections.</summary>
    public int MaxCharacters { get; init; } = 20_000;
    public string? Password { get; init; }
}

/// <summary>Options for presentation conversion.</summary>
public sealed record PresentationConvertRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Slides { get; init; }
    public string? Password { get; init; }
    public string? EncryptPassword { get; init; }
}

/// <summary>Options for rendering one or more slides.</summary>
public sealed record PresentationRenderRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Slides { get; init; }
    public bool AllSlides { get; init; }
    public int? Dpi { get; init; }
    public int? Width { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for creating a presentation from a bounded source.</summary>
public sealed record NewPresentationRequest
{
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? MarkdownPath { get; init; }
    public string? TemplatePath { get; init; }
    public string? Size { get; init; }
    public string? EncryptPassword { get; init; }
}

/// <summary>Options for extracting bounded presentation content.</summary>
public sealed record PresentationExtractRequest
{
    public required string What { get; init; }
    public required string OutputDirectory { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Slides { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for one atomic presentation edit batch.</summary>
public sealed record PresentationEditRequest
{
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? BackupPath { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public string? Password { get; init; }
    public string? EncryptPassword { get; init; }
}

/// <summary>Options for bounded shape and notes search.</summary>
public sealed record PresentationSearchRequest
{
    /// <summary>The pattern, hit window and scope (one of <see cref="PresentationSearchScopes.Values"/>).</summary>
    public required SearchQuery Query { get; init; }
    public string? Password { get; init; }
}

/// <summary>Stable projection scopes of <c>slides query slides</c>.</summary>
public static class PresentationReadScopes
{
    public const string Text = "text";
    public const string Shapes = "shapes";
    public const string Full = "full";
    public static IReadOnlyList<string> All { get; } = [Text, Shapes, Full];
}

/// <summary>Stable presentation extraction families.</summary>
public static class PresentationExtractKinds
{
    public const string Media = "media";
    public const string Notes = "notes";
    public const string Text = "text";
    public static IReadOnlyList<string> All { get; } = [Media, Notes, Text];
}

/// <summary>Stable presentation search scopes.</summary>
public static class PresentationSearchScopes
{
    public const string Shapes = "shapes";
    public const string Notes = "notes";
    public const string All = "all";
    public static IReadOnlyList<string> Values { get; } = [Shapes, Notes, All];
}
