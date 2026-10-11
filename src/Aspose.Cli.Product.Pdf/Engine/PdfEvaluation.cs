using System.Text.RegularExpressions;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Aspose.PDF in evaluation mode exposes only the first four pages of a document: reading a
/// later page throws, which the SDK reports as the evaluation limit its profile recognizes
/// (<see cref="PdfEvaluationProfile.RecognizeLimit"/>). The outline, the attachments and the
/// form fields stay complete, but finding the page of a field on a later page throws too: the
/// field is listed without its page. A read that stays within the first pages tells the SDK it
/// read the document, which then says that the result saw only part of it.
/// </summary>
internal static class PdfEvaluation
{
    internal const int VisiblePages = 4;

    /// <summary>The sentence Aspose.PDF in evaluation mode stamps on every page each time it saves.</summary>
    internal static readonly Regex Watermark = new(
        @"Evaluation Only\. Created with Aspose\.PDF\. Copyright \d{4}-\d{4} Aspose Pty Ltd\.",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The notice every Aspose product prints into a document it saves in evaluation mode,
    /// "Evaluation Only. Created with Aspose.PDF. Copyright 2002-2026 Aspose Pty Ltd.", and so
    /// on for Aspose.Cells, Aspose.Words and Aspose.Slides, matched from the product it names
    /// to its copyright, with the "Evaluation Only." before them when the text has it there.
    /// Between them a product may add its platform and version, the notices of two evaluation
    /// saves can overlap in the extracted text, and a page too narrow for the notice in its font
    /// wraps it between any two words.
    /// </summary>
    internal static readonly Regex Notice = new(
        @"(?:Evaluation\s{1,4}Only\.\s{1,4})?Created\s{1,4}with\s{1,4}(?<product>Aspose\.[A-Za-z]+)[\s\S]{0,200}?Copyright\s{1,4}\d{4}\s{0,4}-\s{0,4}\d{4}\s{0,4}Aspose\s{1,4}Pty\s{1,4}Ltd",
        RegexOptions.CultureInvariant);
}
