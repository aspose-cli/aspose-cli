using System.Globalization;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Slides;

/// <summary>Every deterministic check the Slides review can report.</summary>
internal static class SlidesReviewChecks
{
    public static ReviewCheck SlideDuplicate { get; } = new(
        "SLIDES_SLIDE_DUPLICATE",
        ReviewSeverities.Warning,
        "A slide repeats the meaningful content and geometry of an earlier slide.");

    public static ReviewCheck SlideBlank { get; } = new(
        "SLIDES_SLIDE_BLANK",
        ReviewSeverities.Warning,
        "A slide has no visible authored content.");

    public static ReviewCheck ShapeOutsideSlide { get; } = new(
        "SLIDES_SHAPE_OUTSIDE_SLIDE",
        ReviewSeverities.Warning,
        "A shape extends beyond the slide boundaries.");

    public static ReviewCheck TextTooSmall { get; } = new(
        "SLIDES_TEXT_TOO_SMALL",
        ReviewSeverities.Warning,
        "A shape contains visible text smaller than 12 pt.");

    public static ReviewCheck ContentDensityHigh { get; } = new(
        "SLIDES_CONTENT_DENSITY_HIGH",
        ReviewSeverities.Warning,
        "A slide holds so many content objects or so much text that it may be hard to read.");

    public static ReviewCheck ContentDensityLow { get; } = new(
        "SLIDES_CONTENT_DENSITY_LOW",
        ReviewSeverities.Warning,
        "Several content objects with little text occupy only a small part of a slide.");

    public static ReviewCheck ChartCovered { get; } = new(
        "SLIDES_CHART_COVERED",
        ReviewSeverities.Warning,
        string.Create(CultureInfo.InvariantCulture, $"An opaque foreground shape covers at least {SlidesReviewAnalyzer.ChartCoverage * 100:0}% of a chart."));

    public static ReviewCheck ShapesOverlap { get; } = new(
        "SLIDES_SHAPES_OVERLAP",
        ReviewSeverities.Warning,
        string.Create(CultureInfo.InvariantCulture, $"An opaque foreground shape covers at least {SlidesReviewAnalyzer.SevereCoverage * 100:0}% of a content shape."));

    public static ReviewCheck TextOverlapsObject { get; } = new(
        "SLIDES_TEXT_OVERLAPS_OBJECT",
        ReviewSeverities.Warning,
        "Laid-out text runs into a table or chart on the same slide.");

    public static ReviewCheck PlaceholderEmpty { get; } = new(
        "SLIDES_PLACEHOLDER_EMPTY",
        ReviewSeverities.Info,
        "An empty placeholder is invisible in a slide show but shows its prompt text when the deck is edited.");

    public static IReadOnlyList<ReviewCheck> All { get; } =
    [
        SlideDuplicate,
        SlideBlank,
        ShapeOutsideSlide,
        TextTooSmall,
        ContentDensityHigh,
        ContentDensityLow,
        ChartCovered,
        ShapesOverlap,
        TextOverlapsObject,
        PlaceholderEmpty,
    ];
}
