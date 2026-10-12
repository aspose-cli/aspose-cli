namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>
/// The slide size presets, named by their aspect ratio: <c>16x9</c> is 720 by 405 points and
/// <c>4x3</c> 720 by 540 points, both landscape.
/// </summary>
public static class SlidesSlideSizePresets
{
    public const string Widescreen = "16x9";
    public const string Standard = "4x3";

    /// <summary>The shortest slide side in points an operation sets.</summary>
    internal const double MinimumSide = 72;

    /// <summary>The longest slide side in points an operation sets.</summary>
    internal const double MaximumSide = 7200;

    // How far, in points, a dimension may be from a preset's and still match it.
    private const double Tolerance = 1;

    /// <summary>Every preset, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Widescreen, Standard];

    /// <summary>The preset a slide <paramref name="width"/> by <paramref name="height"/> points matches, or null.</summary>
    internal static string? Matching(double width, double height) =>
        Math.Abs(width - 720) > Tolerance ? null
        : Math.Abs(height - 405) <= Tolerance ? Widescreen
        : Math.Abs(height - 540) <= Tolerance ? Standard
        : null;
}
