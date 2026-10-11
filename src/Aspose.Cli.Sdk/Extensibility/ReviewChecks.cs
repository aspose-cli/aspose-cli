using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// The review checks the SDK runs for every product, next to the product's own. Like every
/// review check their codes begin with the product's prefix, such as <c>PDF_FONTS_MISSING_OR_SUBSTITUTED</c>.
/// </summary>
public static class ReviewChecks
{
    /// <summary>A font the document uses is unavailable or substituted: <c>&lt;PRODUCT&gt;_FONTS_MISSING_OR_SUBSTITUTED</c>.</summary>
    /// <param name="productId">The product whose review reports the check.</param>
    public static ReviewCheck FontsMissingOrSubstituted(string productId) => new(
        Prefix(productId) + "FONTS_MISSING_OR_SUBSTITUTED",
        ReviewSeverities.Error,
        "A font the document uses is unavailable or substituted, so the rendering may differ from the author's.");

    /// <summary>The checks every product's review can report in addition to its own.</summary>
    internal static IReadOnlyList<ReviewCheck> Shared(string productId) =>
        [FontsMissingOrSubstituted(productId)];

    /// <summary>
    /// The prefix every review check code of a product begins with: its id in upper case, a hyphen
    /// written as an underscore, such as <c>PDF_</c>.
    /// </summary>
    internal static string Prefix(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return productId.ToUpperInvariant().Replace('-', '_') + "_";
    }
}
