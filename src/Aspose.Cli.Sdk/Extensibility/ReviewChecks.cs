using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>The review checks the SDK runs for every product, next to the product's own.</summary>
public static class ReviewChecks
{
    /// <summary>The product engine exposes no font diagnostics.</summary>
    public static ReviewCheck FontsNotChecked { get; } = new(
        "FONTS_NOT_CHECKED",
        ReviewSeverities.Warning,
        "The product engine exposes no font diagnostics, so the fonts the document uses were not checked.");

    /// <summary>A font the document uses is unavailable or substituted.</summary>
    public static ReviewCheck FontsMissingOrSubstituted { get; } = new(
        "FONTS_MISSING_OR_SUBSTITUTED",
        ReviewSeverities.Error,
        "A font the document uses is unavailable or substituted, so the rendering may differ from the author's.");

    /// <summary>The checks every product's review can report in addition to its own.</summary>
    internal static IReadOnlyList<ReviewCheck> Shared { get; } = [FontsNotChecked, FontsMissingOrSubstituted];
}
