namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>Stable presentation search scopes.</summary>
public static class SlidesSearchScopes
{
    public const string Shapes = "shapes";
    public const string Notes = "notes";
    public const string All = "all";
    public static IReadOnlyList<string> Values { get; } = [Shapes, Notes, All];
}
