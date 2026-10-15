using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// Records what each applied operation of a batch should leave in the output, then reads the
/// staged output back and reports every expectation it does not meet; the SDK runner records
/// each applied operation and turns the read-back into the verification. Only effects with a
/// reliable read-back are recorded: form field values, flattened fields, redacted text, bookmarks, document
/// information, attachments and the page count. The batch is checked as a whole: an
/// expectation a later operation supersedes (the same field, key or attachment set again, the
/// outline or the pages renumbered) is dropped, and an operation is reported as checked only
/// when every effect it recorded was read back. The output is read as <c>pdf inspect</c> reads it
/// (<see cref="PdfInfoProjection"/>), and an issue is located under the object's address there.
/// </summary>
internal sealed class PdfEditVerifier : IEditVerifier<PdfOp>
{
    private readonly Document _document;
    private readonly List<(string Id, string Op)> _recorded = [];
    private readonly HashSet<string> _incomplete = new(StringComparer.Ordinal);
    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Expected<string>> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Expected<string>> _metadata = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Expected<long?>> _addedAttachments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _removedAttachments = new(StringComparer.Ordinal);
    private readonly List<Expected<IReadOnlyList<string>?>> _flattened = [];
    private readonly List<RedactionExpectation> _redactions = [];
    private readonly List<BookmarkExpectation> _bookmarks = [];
    private readonly List<string> _pageOps = [];
    private readonly List<string> _bookmarkOps = [];
    private int _expectedPages;
    private int _expectedBookmarks;

    /// <param name="document">The document the batch edits, which each record reads as the operation left it.</param>
    internal PdfEditVerifier(Document document)
    {
        _document = document;
        _expectedPages = document.Pages.Count;
        _expectedBookmarks = PdfMutationSupport.CountOutline(document.Outlines);
    }

    /// <summary>
    /// Records the effect of one operation that applied, against the document it left. An effect
    /// it cannot read throws, and the SDK leaves the operation unchecked.
    /// </summary>
    public void Record(PdfOp operation, AppliedOperation applied)
    {
        string id = operation.Id!;
        _recorded.Add((id, PdfOp.Catalog.NameOf(operation)));
        RecordEffect(operation, id, applied.ItemsAffected, _document);
    }

    private void RecordEffect(PdfOp operation, string id, long affected, Document document)
    {
        switch (operation)
        {
            case SetFormFieldOp field:
                Replace(_fields, field.Name, new Expected<string>(
                    id,
                    field.Value ?? PdfMutationSupport.ClearedFieldValue(PdfMutationSupport.FormField(document, field.Name))));
                break;
            case FlattenFormsOp flatten:
                foreach (string name in flatten.Fields ?? [.. _fields.Keys])
                {
                    Drop(_fields, name);
                }

                _flattened.Add(new Expected<IReadOnlyList<string>?>(id, flatten.Fields));
                break;
            case RedactTextOp redact:
                _redactions.Add(new RedactionExpectation(
                    id,
                    TextPattern(redact.Find, redact.Regex, redact.CaseSensitive),
                    redact.Pages is null ? null : PdfMutationSupport.Resolve(document, redact.Pages)));
                break;
            case SetPropertiesOp metadata:
                foreach ((string key, string value) in PdfPropertyNames.Written(metadata))
                {
                    Replace(_metadata, key, new Expected<string>(id, value));
                }

                break;
            case ClearPropertiesOp { DocumentInfo: true }:
                foreach (string key in _metadata.Keys.ToArray())
                {
                    Drop(_metadata, key);
                }

                break;
            case AddAttachmentOp attachment:
                string added = attachment.Name ?? Path.GetFileName(attachment.Path);
                if (_removedAttachments.Remove(added, out string? removal))
                {
                    _incomplete.Add(removal);
                }

                // The stream the handler embedded, never the source file again.
                Replace(_addedAttachments, added, new Expected<long?>(id, PdfInfoProjection.AttachmentEntries(document)
                    .LastOrDefault(entry => entry.Info.Name == added).File is { } file ? PdfInfoProjection.Unreported.Length(file) : null));
                break;
            case DeleteAttachmentOp attachment:
                Drop(_addedAttachments, attachment.Name);
                _removedAttachments[attachment.Name] = id;
                break;
            case AddBookmarkOp bookmark:
                _expectedBookmarks++;
                _bookmarkOps.Add(id);
                _bookmarks.Add(new BookmarkExpectation(id, NewBookmarkIndex(document, bookmark.Parent), bookmark.Title, bookmark.Page));
                break;
            case DeleteBookmarksOp:
                _expectedBookmarks -= (int)affected;
                _bookmarkOps.Add(id);
                // The bookmarks after the deleted ones moved up.
                _incomplete.UnionWith(_bookmarks.Select(static bookmark => bookmark.Id));
                _bookmarks.Clear();
                break;
            case InsertPageOp:
            case ImportPagesOp:
            case DeletePagesOp:
            case MovePagesOp:
                _expectedPages += operation switch
                {
                    InsertPageOp => 1,
                    ImportPagesOp => (int)affected,
                    DeletePagesOp => -(int)affected,
                    _ => 0,
                };
                _pageOps.Add(id);
                // Page numbers recorded before name other pages now.
                foreach (RedactionExpectation redaction in _redactions.Where(static redaction => redaction.Pages is not null))
                {
                    _incomplete.Add(redaction.Id);
                }

                _redactions.RemoveAll(static redaction => redaction.Pages is not null);
                for (int index = 0; index < _bookmarks.Count; index++)
                {
                    _incomplete.Add(_bookmarks[index].Id);
                    _bookmarks[index] = _bookmarks[index] with { Page = null };
                }

                if (operation is DeletePagesOp)
                {
                    foreach (string name in _fields.Keys.ToArray())
                    {
                        Drop(_fields, name);
                    }
                }

                if (operation is ImportPagesOp)
                {
                    // The inserted pages may bring form fields of their own.
                    _incomplete.UnionWith(_flattened.Select(static flatten => flatten.Id));
                    _flattened.Clear();
                }

                break;
        }
    }

    /// <summary>Reads the reopened output back against every recorded expectation.</summary>
    internal VerificationFacts Verify(Document output, LicenseState license, OperationDeadline? deadline)
    {
        var issues = new List<VerificationIssue>();
        VerifyPages(output, issues);
        VerifyFields(output, issues);
        VerifyFlattened(output, issues);
        VerifyRedactions(output, license, deadline, issues);
        VerifyBookmarks(output, issues);
        VerifyMetadata(output, issues);
        VerifyAttachments(output, issues);
        return new VerificationFacts(issues, [.. _checked.Where(id => !_incomplete.Contains(id))]);
    }

    private void VerifyPages(Document output, List<VerificationIssue> issues)
    {
        if (_pageOps.Count == 0)
        {
            return;
        }

        _checked.UnionWith(_pageOps);
        if (output.Pages.Count != _expectedPages)
        {
            issues.Add(Issue(
                PdfDiagnostics.PageCountMismatch,
                _pageOps,
                $"the output has {output.Pages.Count} page(s); they should leave {_expectedPages}",
                location: "page/all",
                hint: "Inspect the output with 'pdf inspect' and repeat the page operations on the original."));
        }
    }

    private void VerifyFields(Document output, List<VerificationIssue> issues)
    {
        Dictionary<string, Field> fields = PdfForms.Named(output)
            .Where(static item => item.Name is not null)
            .ToDictionary(static item => item.Name, static item => item.Field, StringComparer.Ordinal);
        foreach ((string name, Expected<string> expected) in _fields)
        {
            _checked.Add(expected.Id);
            Field? field = fields.GetValueOrDefault(name);
            string? actual = field is null ? null : PdfForms.State(field).Value;
            if (!string.Equals(actual, expected.Value, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    PdfDiagnostics.FieldValueMismatch,
                    [expected.Id],
                    field is null
                        ? $"form field '{name}' is not in the output"
                        : $"form field '{name}' reads '{actual}' in the output, not '{expected.Value}'",
                    location: PdfInfoProjection.Address("field", name),
                    hint: "Read the field with 'pdf inspect --detail forms' and check the value against the field's options."));
            }
        }
    }

    /// <summary>A flattened field leaves no field of its name, and flattening every field leaves none.</summary>
    private void VerifyFlattened(Document output, List<VerificationIssue> issues)
    {
        foreach (Expected<IReadOnlyList<string>?> flatten in _flattened)
        {
            _checked.Add(flatten.Id);
            // In the order of the document's fields, which a read lists by name.
            string[] remaining = [.. output.Form.Fields
                .Select(static field => field.FullName)
                .Where(name => flatten.Value?.Contains(name, StringComparer.Ordinal) ?? true)
                .Distinct(StringComparer.Ordinal)];
            if (remaining.Length > 0)
            {
                issues.Add(Issue(
                    PdfDiagnostics.FieldNotFlattened,
                    [flatten.Id],
                    $"the output still has the form field(s) {string.Join(", ", remaining.Select(static name => $"'{name}'"))}",
                    location: "field/all",
                    hint: "List the remaining fields with 'pdf inspect --detail forms' and flatten them again."));
            }
        }
    }

    /// <summary>
    /// Extracts each page's text once for every redaction that covers it, and counts the
    /// matches each pattern still has. In evaluation mode the matches inside the watermark
    /// sentence the engine stamps on the page are the engine's, not the document's: they are
    /// subtracted once per page, so any other text, on the same line or not, still counts.
    /// The watermarks of earlier evaluation saves lie on the new one and extract as one line.
    /// </summary>
    private void VerifyRedactions(Document output, LicenseState license, OperationDeadline? deadline, List<VerificationIssue> issues)
    {
        if (_redactions.Count == 0)
        {
            return;
        }

        IEnumerable<int> pages = _redactions.Any(static redaction => redaction.Pages is null)
            ? Enumerable.Range(1, output.Pages.Count)
            : _redactions.SelectMany(static redaction => redaction.Pages!).Distinct().Order();
        foreach (RedactionExpectation redaction in _redactions)
        {
            _checked.Add(redaction.Id);
        }

        foreach (int number in pages.Where(number => number <= output.Pages.Count))
        {
            deadline?.ThrowIfExpired("pdf-verify");
            string text = ExtractText(output.Pages[number], PdfReadContents.Text);
            string? watermark = license == LicenseState.Evaluation ? Watermark(output.Pages[number]) : null;
            string location = string.Create(CultureInfo.InvariantCulture, $"page/{number}");
            foreach (RedactionExpectation redaction in _redactions.Where(redaction => redaction.Pages?.Contains(number) ?? true))
            {
                int remaining;
                try
                {
                    remaining = Count(redaction.Pattern, text) - (watermark is null ? 0 : Count(redaction.Pattern, watermark));
                }
                catch (RegexMatchTimeoutException)
                {
                    _incomplete.Add(redaction.Id);
                    issues.Add(Issue(
                        PdfDiagnostics.VerificationIncomplete,
                        [redaction.Id],
                        string.Create(CultureInfo.InvariantCulture, $"its pattern exceeded its time budget on page {number}, so the page was not checked"),
                        location,
                        "Simplify the expression, or search the page with 'pdf search'."));
                    continue;
                }

                if (remaining > 0)
                {
                    // The message never repeats the redacted text.
                    issues.Add(Issue(
                        PdfDiagnostics.RedactedTextFound,
                        [redaction.Id],
                        string.Create(CultureInfo.InvariantCulture, $"page {number} of the output still has {remaining} match(es) of its pattern"),
                        location,
                        "Check whether a later operation added the text again, or redact the area with redact_area, then search the output with 'pdf search'."));
                }
            }
        }
    }

    private void VerifyBookmarks(Document output, List<VerificationIssue> issues)
    {
        if (_bookmarkOps.Count == 0)
        {
            return;
        }

        _checked.UnionWith(_bookmarkOps);
        int count = PdfMutationSupport.CountOutline(output.Outlines);
        if (count != _expectedBookmarks)
        {
            issues.Add(Issue(
                PdfDiagnostics.BookmarkMismatch,
                _bookmarkOps,
                $"the output has {count} bookmark(s); they should leave {_expectedBookmarks}",
                location: "bookmark/all",
                hint: "Compare 'pdf inspect --detail outline' of the input and the output."));
        }

        Dictionary<string, (PdfOutlineItem Item, int Destination)> outline = PdfInfoProjection.Outline(output.Outlines, parent: null, int.MaxValue, int.MaxValue)
            .ToDictionary(static entry => entry.Item.Bookmark, StringComparer.Ordinal);
        foreach (BookmarkExpectation bookmark in _bookmarks)
        {
            (PdfOutlineItem? item, int page) = outline.GetValueOrDefault(bookmark.Index);
            if (item is null || !string.Equals(item.Title, bookmark.Title, StringComparison.Ordinal)
                || (bookmark.Page is { } expected && page != expected))
            {
                issues.Add(Issue(
                    PdfDiagnostics.BookmarkMismatch,
                    [bookmark.Id],
                    item is null
                        ? $"the output has no bookmark {bookmark.Index}, where it added '{bookmark.Title}'"
                        : $"bookmark {bookmark.Index} of the output is '{item.Title}' on page {page}, not '{bookmark.Title}' on page {bookmark.Page}",
                    location: $"bookmark/{bookmark.Index}",
                    hint: "Compare 'pdf inspect --detail outline' of the output with the batch."));
            }
        }
    }

    private void VerifyMetadata(Document output, List<VerificationIssue> issues)
    {
        foreach ((string key, Expected<string> expected) in _metadata)
        {
            _checked.Add(expected.Id);
            string? actual = PdfInfoProjection.Unreported.Info(output, key);
            if (!string.Equals(actual ?? string.Empty, expected.Value, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    PdfDiagnostics.PropertiesMismatch,
                    [expected.Id],
                    $"document information '{key}' reads '{actual}' in the output, not '{expected.Value}'",
                    location: PdfInfoProjection.PropertyAddress(key),
                    hint: "Inspect the output with 'pdf inspect --detail properties'."));
            }
        }
    }

    /// <summary>
    /// Attachments with the same name coexist (the engine adds rather than replaces), so an
    /// added one is found when any attachment of its name holds its length.
    /// </summary>
    private void VerifyAttachments(Document output, List<VerificationIssue> issues)
    {
        if (_addedAttachments.Count == 0 && _removedAttachments.Count == 0)
        {
            return;
        }

        ILookup<string, FileSpecification> present = PdfInfoProjection.AttachmentEntries(output)
            .Where(static entry => entry.Info.Name.Length > 0)
            .ToLookup(static entry => entry.Info.Name, static entry => entry.File, StringComparer.Ordinal);
        foreach ((string name, Expected<long?> expected) in _addedAttachments)
        {
            _checked.Add(expected.Id);
            FileSpecification[] files = [.. present[name]];
            string? problem = files.Length == 0
                ? $"attachment '{name}' is not in the output"
                : expected.Value is long size && !files.Any(file => PdfInfoProjection.Unreported.Length(file) is not long actual || actual == size)
                    ? string.Create(CultureInfo.InvariantCulture, $"no attachment named '{name}' in the output holds the {size} byte(s) it added")
                    : null;
            Report(name, expected.Id, problem);
        }

        foreach ((string name, string id) in _removedAttachments)
        {
            _checked.Add(id);
            Report(name, id, present[name].Any() ? $"attachment '{name}' is still in the output" : null);
        }

        void Report(string name, string id, string? problem)
        {
            if (problem is not null)
            {
                issues.Add(Issue(
                    PdfDiagnostics.AttachmentMismatch,
                    [id],
                    problem,
                    location: PdfInfoProjection.Address("attachment", name),
                    hint: "List the attachments with 'pdf inspect --detail attachments' and extract them with 'pdf extract --what attachments' to compare."));
            }
        }
    }

    /// <summary>An issue whose message names the operations it concerns, by id and name.</summary>
    private VerificationIssue Issue(DiagnosticDescriptor code, IReadOnlyCollection<string> ids, string problem, string location, string hint)
    {
        string operations = string.Join(", ", ids.Select(id => $"'{id}' ({_recorded.First(item => item.Id == id).Op})"));
        string subject = ids.Count == 1 ? $"Operation {operations}" : $"Operations {operations}";
        return VerificationIssues.Of(code, $"{subject}: {problem}.", location, hint);
    }

    private void Replace<T>(Dictionary<string, Expected<T>> expectations, string key, Expected<T> expected)
    {
        Drop(expectations, key);
        expectations[key] = expected;
    }

    private void Drop<T>(Dictionary<string, Expected<T>> expectations, string key)
    {
        if (expectations.Remove(key, out Expected<T>? earlier))
        {
            _incomplete.Add(earlier.Id);
        }
    }

    /// <summary>The index of the bookmark add_bookmark just appended below <paramref name="parent"/>.</summary>
    private static string NewBookmarkIndex(Document document, string? parent)
    {
        try
        {
            return PdfMutationSupport.NewBookmarkIndex(document, parent);
        }
        catch (CliException exception)
        {
            throw new InvalidOperationException($"Bookmark {parent} was not found.", exception);
        }
    }

    private static int Count(Regex pattern, string text) => pattern.Matches(text).Count(static match => match.Length > 0);

    /// <summary>The evaluation watermark sentence the page carries, or null.</summary>
    private static string? Watermark(Page page)
    {
        var absorber = new TextFragmentAbsorber(PdfEvaluation.Watermark, new TextSearchOptions(true));
        page.Accept(absorber);
        return absorber.TextFragments.Count == 0 ? null : absorber.TextFragments[1].Text;
    }

    private sealed record Expected<T>(string Id, T Value);

    private sealed record RedactionExpectation(string Id, Regex Pattern, IReadOnlyList<int>? Pages);

    private sealed record BookmarkExpectation(string Id, string Index, string Title, int? Page);
}
