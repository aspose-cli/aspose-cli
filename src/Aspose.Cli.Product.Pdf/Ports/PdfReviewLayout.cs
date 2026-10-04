namespace Aspose.Cli.Product.Pdf.Ports;

/// <summary>Bounded deterministic facts from the real PDF text layout.</summary>
internal sealed record PdfReviewLayout(
    IReadOnlyList<PdfReviewPageLayout> Pages);

/// <summary>
/// Displayed size (rotation applied, in points), text boundary facts and the share of the page
/// its images cover, from 0 to 1, for one PDF page.
/// </summary>
internal sealed record PdfReviewPageLayout(
    int Page,
    double WidthPoints,
    double HeightPoints,
    int TextFragments,
    int OutsideTextFragments,
    double ImageCoverage);
