using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Aspose.PDF in evaluation mode exposes only the first four pages of a document: reading a
/// later page throws, although the engine's message speaks of any collection. The outline, the
/// attachments and the form fields stay complete, but finding the page of a field on a later
/// page throws too: the field is listed without its page. Any other command that needs a later
/// page is refused as an evaluation limit, and a read that stays within the first pages says
/// that it saw only part of the document.
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

    // The SDK reports the limit only with this message on an IndexOutOfRangeException.
    private const string LimitMessage = "can be viewed in evaluation mode";

    /// <summary>
    /// Runs a PDF command, refusing it with EVALUATION_LIMIT when it reads past the limit. A
    /// command that has no <c>--pages</c> states why it needs a later page and what the user
    /// can do instead; an outer run keeps that refusal, as it keeps every error a command
    /// already explained.
    /// </summary>
    internal static T Run<T>(ILicenseState license, Func<T> command, string? cause = null, string? remedy = null)
    {
        try
        {
            return command();
        }
        catch (Exception exception) when (exception is not CliException && IsCollectionLimit(exception) && license.IsEvaluation)
        {
            throw CliErrors.EvaluationLimit(
                $"Evaluation mode lets the PDF engine read only the first {VisiblePages} pages of a document; {cause ?? "this command needs a later page"}, so no output was written.",
                "Aspose.PDF",
                remedy ?? $"limit a command that takes --pages to pages 1-{VisiblePages}",
                innerException: exception);
        }
    }

    /// <summary>The disclosure for a read of a document with more pages than evaluation mode shows.</summary>
    internal static Warning? InputTruncated(LicenseState state, int pages) =>
        state == LicenseState.Evaluation && pages > VisiblePages
            ? Truncated(
                pages,
                "page sizes and page content describe only those pages; bookmarks, attachments and the form field count are complete",
                affectsCompleteness: true)
            : null;

    /// <summary>The disclosure for form fields listed without the page evaluation mode hides.</summary>
    internal static Warning FieldsWithoutPage(int pages, IEnumerable<string> names) =>
        Truncated(pages, $"these fields have no page: {string.Join(", ", names)}", affectsCompleteness: false);

    private static Warning Truncated(int pages, string consequence, bool affectsCompleteness) => new(WarningCodes.EvalInputTruncated, string.Create(
            CultureInfo.InvariantCulture,
            $"Evaluation mode shows only the first {VisiblePages} of {pages} pages, so {consequence}."))
    {
        Hint = "Apply an Aspose.PDF license to read the whole document.",
        AffectsCompleteness = affectsCompleteness,
    };

    internal static bool IsCollectionLimit(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is IndexOutOfRangeException && current.Message.Contains(LimitMessage, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
