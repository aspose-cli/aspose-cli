using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// The review checks every product's review reports next to the product's own, each made in one
/// place for all of them. Like every review check their codes begin with the product's prefix, such
/// as <c>PDF_FONTS_MISSING_OR_SUBSTITUTED</c>.
/// </summary>
public static class ReviewChecks
{
    /// <summary>A font the document uses is unavailable or substituted: <c>&lt;PRODUCT&gt;_FONTS_MISSING_OR_SUBSTITUTED</c>.</summary>
    /// <param name="productId">The product whose review reports the check.</param>
    public static ReviewCheck FontsMissingOrSubstituted(string productId) => new(
        Prefix(productId) + "FONTS_MISSING_OR_SUBSTITUTED",
        ReviewSeverities.Error,
        "A font the document uses is unavailable or substituted, so the rendering may differ from the author's.");

    /// <summary>
    /// The review rendered only some parts of its view, as many as <c>--max-items</c> or the
    /// preview snapshot budget allowed: <c>&lt;PRODUCT&gt;_REVIEW_TRUNCATED</c>, which the review
    /// that sets those limits reports.
    /// </summary>
    /// <param name="productId">The product whose review reports the check.</param>
    public static ReviewCheck ReviewTruncated(string productId) => new(
        Prefix(productId) + "REVIEW_TRUNCATED",
        ReviewSeverities.Warning,
        "The review rendered only some parts, as many as --max-items or the preview snapshot budget allowed, so the rest have no image and were not checked.");

    /// <summary>The checks every product's review can report in addition to its own.</summary>
    internal static IReadOnlyList<ReviewCheck> Shared(string productId) =>
        [FontsMissingOrSubstituted(productId), ReviewTruncated(productId)];

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
