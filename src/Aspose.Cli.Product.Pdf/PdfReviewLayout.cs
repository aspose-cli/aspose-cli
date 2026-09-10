namespace Aspose.Cli.Product.Pdf;

/// <summary>Bounded deterministic facts from the real PDF text layout.</summary>
internal sealed record PdfReviewLayout(
    IReadOnlyList<PdfReviewPageLayout> Pages);

/// <summary>Text boundary facts for one PDF page.</summary>
internal sealed record PdfReviewPageLayout(
    int Page,
    int TextFragments,
    int OutsideTextFragments);
