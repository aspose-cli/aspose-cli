using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Every check the PDF review can report; <see cref="PdfViewAdapter"/> builds its findings only from these.</summary>
internal static class PdfReviewChecks
{
    /// <summary>Text fragments extend beyond the page rectangle.</summary>
    public static ReviewCheck TextOutsidePage { get; } = new(
        "PDF_TEXT_OUTSIDE_PAGE",
        ReviewSeverities.Warning,
        "Text fragments extend beyond the page rectangle and may be clipped.");

    /// <summary>Text lies under an opaque box painted over it, such as a redaction cover.</summary>
    public static ReviewCheck TextCovered { get; } = new(
        "PDF_TEXT_COVERED",
        ReviewSeverities.Warning,
        "Text lies under an opaque box painted over it, such as a redaction cover, so it is in the file but not visible on the page.");

    /// <summary>A page is smaller than one inch or larger than 40 inches on a side.</summary>
    public static ReviewCheck PageSizeUnusual { get; } = new(
        "PDF_PAGE_SIZE_UNUSUAL",
        ReviewSeverities.Warning,
        "A page is narrower or shorter than 72 pt, or wider or taller than 2880 pt, and may preview poorly.");

    /// <summary>A page that does not look scanned has neither readable text nor images over a quarter of it.</summary>
    public static ReviewCheck PageWithoutReadableContent { get; } = new(
        "PDF_PAGE_WITHOUT_READABLE_CONTENT",
        ReviewSeverities.Warning,
        "A page has no readable text, no images covering a quarter of it and does not look scanned, so it may be unintentionally blank.");

    /// <summary>A page has no extractable text and an image over most of it, as a scan does.</summary>
    public static ReviewCheck PageWithoutTextLayer { get; } = new(
        "PDF_PAGE_WITHOUT_TEXT_LAYER",
        ReviewSeverities.Info,
        "A page has no extractable text and an image covers most of it, as on a scan, so search and redact_text do not reach its content.");

    /// <summary>A page without form fields or images over a quarter of it carries very little readable text.</summary>
    public static ReviewCheck PageUtilizationLow { get; } = new(
        "PDF_PAGE_UTILIZATION_LOW",
        ReviewSeverities.Warning,
        "A page carries very little readable text and no images covering a quarter of it, which may indicate an unintended sparse page or pagination break.");

    /// <summary>Text extraction for a page reached its budget.</summary>
    public static ReviewCheck TextAnalysisTruncated { get; } = new(
        "PDF_TEXT_ANALYSIS_TRUNCATED",
        ReviewSeverities.Warning,
        "Text extraction reached its budget, so the structural text checks of a page are incomplete.");

    /// <summary>The document has form fields whose rendered appearance needs inspection.</summary>
    public static ReviewCheck FormAppearanceReviewRequired { get; } = new(
        "PDF_FORM_APPEARANCE_REVIEW_REQUIRED",
        ReviewSeverities.Info,
        "The document contains form fields whose rendered appearances must be inspected for stale, clipped or missing values.");

    /// <summary>Form fields cannot be associated with a valid page.</summary>
    public static ReviewCheck FormFieldPageUnresolved { get; } = new(
        "PDF_FORM_FIELD_PAGE_UNRESOLVED",
        ReviewSeverities.Warning,
        "Form fields cannot be associated with a valid page of the document.");

    /// <summary>Font resources other than the standard 14 are not embedded.</summary>
    public static ReviewCheck FontsNotEmbedded { get; } = new(
        "PDF_FONTS_NOT_EMBEDDED",
        ReviewSeverities.Warning,
        "Font resources other than the standard 14 PDF fonts are not embedded, so rendering can vary between machines.");

    /// <summary>A page carries the watermark an Aspose product's save without a license stamped into the file.</summary>
    public static ReviewCheck EvaluationWatermark { get; } = new(
        "PDF_EVALUATION_WATERMARK",
        ReviewSeverities.Warning,
        "A page carries the evaluation watermark that an Aspose product (PDF, Cells, Words or Slides) saving without a license stamped into the file; a license does not remove it.");

    /// <summary>Every PDF review check.</summary>
    public static IReadOnlyList<ReviewCheck> All { get; } =
    [
        TextOutsidePage,
        TextCovered,
        PageSizeUnusual,
        PageWithoutReadableContent,
        PageWithoutTextLayer,
        PageUtilizationLow,
        TextAnalysisTruncated,
        FormAppearanceReviewRequired,
        FormFieldPageUnresolved,
        FontsNotEmbedded,
        EvaluationWatermark,
    ];
}
