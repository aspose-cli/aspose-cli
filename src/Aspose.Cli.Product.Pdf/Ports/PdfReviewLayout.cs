namespace Aspose.Cli.Product.Pdf.Ports;

/// <summary>Bounded deterministic facts from the real PDF text layout.</summary>
internal sealed record PdfReviewLayout(
    IReadOnlyList<PdfReviewPageLayout> Pages);

/// <summary>
/// Displayed size (rotation applied, in points), text boundary facts, the share of the page
/// its images cover, from 0 to 1, and whether its text holds the evaluation watermark a save
/// without a license stamped, for one PDF page.
/// </summary>
internal sealed record PdfReviewPageLayout(
    int Page,
    double WidthPoints,
    double HeightPoints,
    int TextFragments,
    int OutsideTextFragments,
    double ImageCoverage,
    bool EvaluationWatermark);
