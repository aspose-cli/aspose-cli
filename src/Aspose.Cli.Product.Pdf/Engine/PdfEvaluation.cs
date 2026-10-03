using System.Globalization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Aspose.PDF in evaluation mode exposes only the first four pages of a document: reading a
/// later page throws, although the engine's message speaks of any collection. The outline, the
/// attachments and the count of form fields stay complete, but finding the page of a field on a
/// later page throws too. A command that needs a later page is refused as an evaluation limit,
/// and a read that stays within the first pages says that it saw only part of the document.
/// </summary>
internal static class PdfEvaluation
{
    internal const int VisiblePages = 4;

    // The SDK reports the limit only with this message on an IndexOutOfRangeException.
    private const string LimitMessage = "can be viewed in evaluation mode";

    /// <summary>Runs a PDF command, refusing it with EVALUATION_LIMIT when it reads past the limit.</summary>
    internal static T Run<T>(ILicenseGate licenseGate, Func<T> command)
    {
        try
        {
            return command();
        }
        catch (Exception exception) when (IsCollectionLimit(exception) && licenseGate.EnsureApplied() == LicenseState.Evaluation)
        {
            throw new CliException(
                ErrorCodes.EvaluationLimit,
                $"Evaluation mode lets the PDF engine read only the first {VisiblePages} pages of a document; this command needs a later page.",
                hint: "Apply an Aspose.PDF license. Without one, only pages 1-4 can be read: a command that takes --pages can be limited to them. No output was written.",
                docs: "licensing",
                innerException: exception);
        }
    }

    /// <summary>The disclosure for a read of a document with more pages than evaluation mode shows.</summary>
    internal static Warning? InputTruncated(LicenseState state, int pages) =>
        state == LicenseState.Evaluation && pages > VisiblePages
            ? new Warning
            {
                Code = WarningCodes.EvalInputTruncated,
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Evaluation mode shows only the first {VisiblePages} of {pages} pages, so page sizes and page content describe only those pages; bookmarks, attachments and the form field count are complete."),
                Hint = "Apply an Aspose.PDF license to read the whole document.",
                AffectsCompleteness = true,
            }
            : null;

    private static bool IsCollectionLimit(Exception exception)
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
