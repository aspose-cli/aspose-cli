namespace Aspose.Cli.Product.Words.Ports;

/// <summary>
/// Bounded deterministic facts from the real Words page-layout model, the number of paragraphs
/// that hold text evaluation mode saved into the file, and whether the file holds the notice
/// that evaluation mode cut it short; the last two are read only with a license.
/// </summary>
internal sealed record WordsReviewLayout(
    IReadOnlyList<WordsReviewPageLayout> Pages,
    IReadOnlyList<WordsReviewHeadingLayout> OrphanedHeadings,
    int EvaluationMarks,
    bool EvaluationTruncated);

/// <summary>Layout facts for one rendered page.</summary>
internal sealed record WordsReviewPageLayout
{
    public required int Page { get; init; }

    public required double WidthPoints { get; init; }

    public required double HeightPoints { get; init; }

    public required int VisibleCharacters { get; init; }

    public required int VisualObjects { get; init; }

    public required int ExplicitPageBreaks { get; init; }

    public required int OutsideObjects { get; init; }

    public required IReadOnlyList<string> OutsideObjectNames { get; init; }

    public double? ContentLeft { get; init; }

    public double? ContentTop { get; init; }

    public double? ContentRight { get; init; }

    public double? ContentBottom { get; init; }

    public double? MinimumFontSize { get; init; }

    public double? MaximumFontSize { get; init; }

    public double ContentAreaRatio => ContentLeft is null
        || ContentTop is null
        || ContentRight is null
        || ContentBottom is null
        || WidthPoints <= 0
        || HeightPoints <= 0
            ? 0
            : Math.Max(0, ContentRight.Value - ContentLeft.Value)
                * Math.Max(0, ContentBottom.Value - ContentTop.Value)
                / (WidthPoints * HeightPoints);
}

/// <summary>A heading laid out after prior page content without following body content.</summary>
internal sealed record WordsReviewHeadingLayout(
    int Block,
    int Page,
    int Level,
    string Text);
