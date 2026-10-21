using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfPages;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// The check of every <c>redact_text</c> before its output is published, with or without
/// <c>--verify</c>, because a redaction promises that its text is gone. The reopened output is
/// searched for each operation's pattern wherever a reader finds text: the text of the pages it
/// covered, the values of the form fields on them and the text of their annotations, and, for an
/// operation over every page, the bookmark titles, every document information entry and XMP
/// value that came from the document, and the names and descriptions of the attachments. A
/// property the save wrote or changed is the engine's, not the document's: the output's value
/// is checked only when the document held it before the save, whatever the key's spelling. A match anywhere is an issue, and an output
/// with an issue is not published. An issue never repeats the text, so an attachment whose name
/// matches is located as <c>attachment/all</c>.
/// </summary>
internal static class PdfRedactionAudit
{
    /// <summary>One applied <c>redact_text</c>.</summary>
    /// <param name="Id">The operation's id.</param>
    /// <param name="Pattern">The expression it matched.</param>
    /// <param name="Pages">The identities of the pages it covered, or null for every page.</param>
    internal sealed record Redaction(string Id, Regex Pattern, IReadOnlyList<PdfPageIdentity>? Pages);

    private const string Hint =
        "Nothing was written. Take the text out where the location names it, with set_form_field, set_properties or clear_properties, "
        + "delete_bookmarks or delete_attachment, then redact again; search the pages with 'pdf search'.";

    private const string PropertyHint =
        "Nothing was written. The document's own property still holds text the pattern matches: add clear_properties before "
        + "redact_text in the same batch, which also removes the XMP metadata, or redact with a more specific pattern.";

    /// <summary>Every place the output still matches a redaction's pattern.</summary>
    /// <param name="output">The reopened output.</param>
    /// <param name="properties">The document's properties before the save (<see cref="Properties"/>).</param>
    /// <param name="redactions">The redactions the batch applied, in batch order.</param>
    /// <param name="order">The identity of each page of the output, in page order.</param>
    /// <param name="license">The license state, whose evaluation watermark is not the document's text.</param>
    /// <param name="deadline">The deadline of the invocation.</param>
    internal static IReadOnlyList<VerificationIssue> Check(
        Document output, IReadOnlyDictionary<string, string> properties, IReadOnlyList<Redaction> redactions, PdfPageIdentity[] order, LicenseState license, OperationDeadline? deadline)
    {
        var issues = new List<VerificationIssue>();
        foreach (Redaction redaction in redactions)
        {
            int[] pages = redaction.Pages is null
                ? [.. Enumerable.Range(1, output.Pages.Count)]
                : [.. redaction.Pages.Select(page => Array.IndexOf(order, page) + 1).Where(number => number > 0 && number <= output.Pages.Count).Order()];
            try
            {
                Check(output, properties, redaction, pages, license, deadline, issues);
            }
            catch (RegexMatchTimeoutException)
            {
                issues.Add(VerificationIssues.Of(
                    PdfDiagnostics.VerificationIncomplete,
                    $"Operation '{redaction.Id}' (redact_text): its pattern exceeded its time budget, so the output could not be searched for it and was not written.",
                    location: null,
                    "Simplify the expression, then redact again."));
            }
        }

        return issues;
    }

    private static void Check(Document output, IReadOnlyDictionary<string, string> properties, Redaction redaction, int[] pages, LicenseState license, OperationDeadline? deadline, List<VerificationIssue> issues)
    {
        void Found(string place, string location, string hint) => issues.Add(VerificationIssues.Of(
            PdfDiagnostics.RedactedTextFound, $"Operation '{redaction.Id}' (redact_text): {place} of the output still matches its pattern.", location, hint));

        foreach (int number in pages)
        {
            deadline?.ThrowIfExpired("pdf-verify");
            Page page = output.Pages[number];
            string location = string.Create(CultureInfo.InvariantCulture, $"page/{number}");
            (string text, string? watermark) = PageText(page, license);
            int remaining = Remaining(redaction.Pattern, text, watermark);
            if (remaining > 0)
            {
                issues.Add(VerificationIssues.Of(
                    PdfDiagnostics.RedactedTextFound,
                    string.Create(CultureInfo.InvariantCulture, $"Operation '{redaction.Id}' (redact_text): page {number} of the output still has {remaining} match(es) of its pattern."),
                    location,
                    "Nothing was written. Check whether a later operation added the text again, or redact the area with redact_area, then search the output with 'pdf search'."));
            }

            if (page.Annotations.Where(static annotation => annotation is not WidgetAnnotation).Any(annotation => Matches(redaction.Pattern, Texts(annotation))))
            {
                Found(string.Create(CultureInfo.InvariantCulture, $"an annotation on page {number}"), location, Hint);
            }
        }

        foreach ((string name, Field field) in PdfForms.Named(output))
        {
            if ((redaction.Pages is null || PdfForms.PageOf(field) is int page && pages.Contains(page))
                && Matches(redaction.Pattern, [PdfForms.State(field).Value]))
            {
                Found($"the value of form field {PdfInfoProjection.Address("field", name)}", PdfInfoProjection.Address("field", name), Hint);
            }
        }

        if (redaction.Pages is not null)
        {
            return;
        }

        foreach ((PdfOutlineItem item, _) in PdfInfoProjection.Outline(output.Outlines, parent: null, int.MaxValue, int.MaxValue))
        {
            if (Matches(redaction.Pattern, [item.Title]))
            {
                Found($"the title of bookmark {item.Bookmark}", $"bookmark/{item.Bookmark}", Hint);
            }
        }

        foreach ((string key, string value) in Properties(output))
        {
            if (properties.TryGetValue(key, out string? held) && string.Equals(held, value, StringComparison.Ordinal)
                && Matches(redaction.Pattern, [value]))
            {
                string address = key.StartsWith(XmpPrefix, StringComparison.Ordinal)
                    ? $"property/xmp:{TargetName.Escape(key[XmpPrefix.Length..])}"
                    : PdfInfoProjection.PropertyAddress(key[InfoPrefix.Length..]);
                Found($"document property {address}", address, PropertyHint);
            }
        }

        foreach ((PdfAttachmentInfo attachment, FileSpecification file) in PdfInfoProjection.AttachmentEntries(output))
        {
            if (Matches(redaction.Pattern, [attachment.Name]))
            {
                Found("the name of an attachment", "attachment/all", Hint);
            }
            else if (Matches(redaction.Pattern, [file.Description]))
            {
                Found($"the description of attachment {PdfInfoProjection.Address("attachment", attachment.Name)}", PdfInfoProjection.Address("attachment", attachment.Name), Hint);
            }
        }
    }

    private const string InfoPrefix = "info:";

    private const string XmpPrefix = "xmp:";

    /// <summary>
    /// Every document information entry, under its key as stored, whatever its spelling, and every
    /// XMP value, as <c>info:KEY</c> and <c>xmp:KEY</c>.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Properties(Document document)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in document.Info.Keys)
        {
            properties[InfoPrefix + key] = document.Info[key] ?? string.Empty;
        }

        foreach ((string key, XmpValue value) in document.Metadata)
        {
            properties[XmpPrefix + key] = value.ToString() ?? string.Empty;
        }

        return properties;
    }

    /// <summary>The text of a page and, in evaluation mode, the watermark sentence the engine stamps on it, or null.</summary>
    internal static (string Text, string? Watermark) PageText(Page page, LicenseState license) =>
        (ExtractText(page, PdfReadContents.Text), license == LicenseState.Evaluation ? Watermark(page) : null);

    /// <summary>
    /// The matches a pattern has in a page's text. In evaluation mode the matches inside the
    /// watermark sentence the engine stamps on the page are the engine's, not the document's: they
    /// are subtracted once, so any other text, on the same line or not, still counts. The
    /// watermarks of earlier evaluation saves lie on the new one and extract as one line.
    /// </summary>
    internal static int Remaining(Regex pattern, string text, string? watermark) =>
        Count(pattern, text) - (watermark is null ? 0 : Count(pattern, watermark));

    /// <summary>Whether a pattern matches any of the texts.</summary>
    internal static bool Matches(Regex pattern, IEnumerable<string?> texts) =>
        texts.Any(text => !string.IsNullOrEmpty(text) && Count(pattern, text) > 0);

    /// <summary>The text an annotation shows or keeps: its contents and, for a markup annotation, its author, subject and rich text.</summary>
    internal static IEnumerable<string?> Texts(Annotation annotation) => annotation is MarkupAnnotation markup
        ? [annotation.Contents, markup.Title, markup.Subject, markup.RichText]
        : [annotation.Contents];

    private static int Count(Regex pattern, string text) => pattern.Matches(text).Count(static match => match.Length > 0);

    /// <summary>The evaluation watermark sentence the page carries, or null.</summary>
    private static string? Watermark(Page page)
    {
        var absorber = new TextFragmentAbsorber(PdfEvaluation.Watermark, new TextSearchOptions(true));
        page.Accept(absorber);
        return absorber.TextFragments.Count == 0 ? null : absorber.TextFragments[1].Text;
    }
}
