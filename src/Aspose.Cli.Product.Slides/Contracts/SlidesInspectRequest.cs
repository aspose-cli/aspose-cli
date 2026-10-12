namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for presentation structure inspection.</summary>
public sealed record SlidesInspectRequest
{
    /// <summary>The presentation the command reads.</summary>
    public required string Input { get; init; }
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Accepted values of <c>slides inspect --detail</c>.</summary>
public static class SlidesInspectDetails
{
    /// <summary>The slide masters.</summary>
    public const string Masters = "masters";

    /// <summary>The layout slides.</summary>
    public const string Layouts = "layouts";

    /// <summary>The images, audio and video.</summary>
    public const string Media = "media";

    /// <summary>The fonts the presentation uses.</summary>
    public const string Fonts = "fonts";

    /// <summary>The presence and character count of each slide's notes.</summary>
    public const string Notes = "notes";

    /// <summary>The comments.</summary>
    public const string Comments = "comments";

    /// <summary>The sections.</summary>
    public const string Sections = "sections";

    /// <summary>The document properties.</summary>
    public const string Properties = "properties";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Masters, Layouts, Media, Fonts, Notes, Comments, Sections, Properties];
}
