namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Options for a bounded presentation read.</summary>
public sealed record SlidesReadRequest
{
    /// <summary>The presentation the command reads.</summary>
    public required string Input { get; init; }
    public PageRange? Slides { get; init; }
    public string Content { get; init; } = SlidesReadContents.Shapes;
    public bool IncludeNotes { get; init; }
    /// <summary>Returned document-content character budget, including repeated projections.</summary>
    public int MaxCharacters { get; init; } = 20_000;
    public Secret? Password { get; init; }
}

/// <summary>The projection depths <c>slides read --content</c> selects.</summary>
public static class SlidesReadContents
{
    public const string Text = "text";
    public const string Shapes = "shapes";
    public const string Full = "full";
    public static IReadOnlyList<string> All { get; } = [Text, Shapes, Full];
}
