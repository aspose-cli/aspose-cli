using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// Records what each applied operation of a batch should leave in the output, then reads the
/// staged output back and reports every expectation it does not meet. Only effects with a
/// reliable read-back are recorded: form field values, flattened fields, redacted text, bookmarks, document
/// information, attachments and the page count. The batch is checked as a whole: an
/// expectation a later operation supersedes (the same field, key or attachment set again, the
/// outline or the pages renumbered) is dropped, and an operation is reported as checked only
/// when every effect it recorded was read back.
/// </summary>
internal sealed class PdfEditVerifier
{
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

    internal PdfEditVerifier(Document document)
    {
        _expectedPages = document.Pages.Count;
        _expectedBookmarks = PdfMutationSupport.CountOutline(document.Outlines);
    }

    /// <summary>
    /// Records the effect of one operation that applied, against the document it left. It never
    /// throws for the document's content: an effect it cannot record is left unchecked, so it
    /// cannot turn an applied operation into a failed one.
    /// </summary>
    internal void Record(PdfOp operation, string id, long affected, Document document)
    {
        _recorded.Add((id, PdfOp.Catalog.NameOf(operation)));
        try
        {
            RecordEffect(operation, id, affected, document);
        }
        catch (Exception exception) when (
            exception.GetType().Assembly == typeof(Document).Assembly
            || exception is InvalidOperationException or ArgumentException or IOException)
        {
            _incomplete.Add(id);
        }
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
                    TextPattern(redact.Pattern, redact.Regex, caseSensitive: true),
                    redact.Pages is null ? null : PdfMutationSupport.Resolve(document, redact.Pages)));
                break;
            case SetMetadataOp metadata:
                Expect(metadata.Title, "Title");
                Expect(metadata.Author, "Author");
                Expect(metadata.Subject, "Subject");
                Expect(metadata.Keywords, "Keywords");
                foreach ((string key, string value) in metadata.Custom ?? new Dictionary<string, string>())
                {
                    Expect(value, key);
                }

                void Expect(string? value, string key)
                {
                    if (value is not null)
                    {
                        Replace(_metadata, key, new Expected<string>(id, value));
                    }
                }

                break;
            case RemoveMetadataOp { DocumentInfo: true }:
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
                Replace(_addedAttachments, added, new Expected<long?>(id, document.EmbeddedFiles
                    .LastOrDefault(file => (file.UnicodeName ?? file.Name) == added)?.Contents is { CanSeek: true } contents
                        ? contents.Length
                        : null));
                break;
            case RemoveAttachmentOp attachment:
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
            case InsertBlankPageOp:
            case InsertPagesFromOp:
            case DeletePagesOp:
            case MovePagesOp:
                _expectedPages += operation switch
                {
                    InsertBlankPageOp => 1,
                    InsertPagesFromOp => (int)affected,
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

                if (operation is InsertPagesFromOp)
                {
                    // The inserted pages may bring form fields of their own.
                    _incomplete.UnionWith(_flattened.Select(static flatten => flatten.Id));
                    _flattened.Clear();
                }

                break;
        }
    }

    /// <summary>Reads the reopened output back against every recorded expectation.</summary>
    internal PdfEditVerification Verify(Document output, LicenseState license, OperationDeadline? deadline)
    {
        var issues = new List<VerificationIssue>();
        VerifyPages(output, issues);
        VerifyFields(output, issues);
        VerifyFlattened(output, issues);
        VerifyRedactions(output, license, deadline, issues);
        VerifyBookmarks(output, issues);
        VerifyMetadata(output, issues);
        VerifyAttachments(output, issues);
        return new PdfEditVerification
        {
            Ok = issues.Count == 0,
            Issues = issues,
            CheckedOps = [.. _recorded
                .Select(static item => item.Id)
                .Where(id => _checked.Contains(id) && !_incomplete.Contains(id))
                .Distinct(StringComparer.Ordinal)],
        };
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
                location: "pdf/pages",
                hint: "Inspect the output with 'pdf inspect' and repeat the page operations on the original."));
        }
    }

    private void VerifyFields(Document output, List<VerificationIssue> issues)
    {
        foreach ((string name, Expected<string> expected) in _fields)
        {
            _checked.Add(expected.Id);
            Field? field = output.Form.Fields.FirstOrDefault(item => string.Equals(item.FullName, name, StringComparison.Ordinal));
            string? actual = field is null ? null : (PdfFormService.RadioGroup(field) ?? field).Value;
            if (!string.Equals(actual, expected.Value, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    PdfDiagnostics.FieldValueMismatch,
                    [expected.Id],
                    field is null
                        ? $"form field '{name}' is not in the output"
                        : $"form field '{name}' reads '{actual}' in the output, not '{expected.Value}'",
                    location: $"pdf/form/{name}",
                    hint: "Read the field with 'pdf query forms' and check the value against the field's options or states."));
            }
        }
    }

    /// <summary>A flattened field leaves no field of its name, and flattening every field leaves none.</summary>
    private void VerifyFlattened(Document output, List<VerificationIssue> issues)
    {
        foreach (Expected<IReadOnlyList<string>?> flatten in _flattened)
        {
            _checked.Add(flatten.Id);
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
                    location: "pdf/form",
                    hint: "List the remaining fields with 'pdf query forms' and flatten them again."));
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
            string text = ExtractText(output.Pages[number], PdfReadModes.Plain);
            string? watermark = license == LicenseState.Evaluation ? Watermark(output.Pages[number]) : null;
            string location = string.Create(CultureInfo.InvariantCulture, $"pdf/page/{number}");
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
                        "Simplify the expression, or search the page with 'pdf query search'."));
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
                        "Check whether a later operation added the text again, or redact the area with redact_area, then search the output with 'pdf query search'."));
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
                location: "pdf/bookmark",
                hint: "Compare 'pdf inspect --detail outline' of the input and the output."));
        }

        foreach (BookmarkExpectation bookmark in _bookmarks)
        {
            OutlineItemCollection? item = Find(output.Outlines, bookmark.Index);
            int? page = item is null ? null : PdfNavigationCensus.DestinationPage(item);
            if (item is null || !string.Equals(item.Title, bookmark.Title, StringComparison.Ordinal)
                || (bookmark.Page is { } expected && page != expected))
            {
                issues.Add(Issue(
                    PdfDiagnostics.BookmarkMismatch,
                    [bookmark.Id],
                    item is null
                        ? $"the output has no bookmark {bookmark.Index}, where it added '{bookmark.Title}'"
                        : $"bookmark {bookmark.Index} of the output is '{item.Title}' on page {page}, not '{bookmark.Title}' on page {bookmark.Page}",
                    location: $"pdf/bookmark/{bookmark.Index}",
                    hint: "Compare 'pdf inspect --detail outline' of the output with the batch."));
            }
        }
    }

    private void VerifyMetadata(Document output, List<VerificationIssue> issues)
    {
        foreach ((string key, Expected<string> expected) in _metadata)
        {
            _checked.Add(expected.Id);
            string? actual = output.Info.TryGetValue(key, out string? stored) ? stored : null;
            if (!string.Equals(actual ?? string.Empty, expected.Value, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    PdfDiagnostics.MetadataMismatch,
                    [expected.Id],
                    $"document information '{key}' reads '{actual}' in the output, not '{expected.Value}'",
                    location: $"pdf/metadata/{key}",
                    hint: "Inspect the output with 'pdf inspect --detail metadata'."));
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

        ILookup<string, FileSpecification> present = output.EmbeddedFiles
            .Where(static file => !string.IsNullOrEmpty(file.UnicodeName ?? file.Name))
            .ToLookup(static file => (file.UnicodeName ?? file.Name)!, StringComparer.Ordinal);
        foreach ((string name, Expected<long?> expected) in _addedAttachments)
        {
            _checked.Add(expected.Id);
            FileSpecification[] files = [.. present[name]];
            string? problem = files.Length == 0
                ? $"attachment '{name}' is not in the output"
                : expected.Value is long size && !files.Any(file => Length(file) is not long actual || actual == size)
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
                    location: $"pdf/attachment/{name}",
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
    private static string NewBookmarkIndex(Document document, string? parent) =>
        parent is null || Find(document.Outlines, parent) is not null
            ? PdfMutationSupport.NewBookmarkIndex(document, parent)
            : throw new InvalidOperationException($"Bookmark {parent} was not found.");

    private static OutlineItemCollection? Find(IEnumerable<OutlineItemCollection> items, string index)
    {
        OutlineItemCollection? found = null;
        IEnumerable<OutlineItemCollection> level = items;
        foreach (string part in index.Split('/'))
        {
            found = level.Skip(int.Parse(part, CultureInfo.InvariantCulture) - 1).FirstOrDefault();
            if (found is null)
            {
                return null;
            }

            level = found;
        }

        return found;
    }

    private static int Count(Regex pattern, string text) => pattern.Matches(text).Count(static match => match.Length > 0);

    /// <summary>The evaluation watermark sentence the page carries, or null.</summary>
    private static string? Watermark(Page page)
    {
        var absorber = new TextFragmentAbsorber(PdfEvaluation.Watermark, new TextSearchOptions(true));
        page.Accept(absorber);
        return absorber.TextFragments.Count == 0 ? null : absorber.TextFragments[1].Text;
    }

    /// <summary>The attachment's length when its stream can tell it without being read.</summary>
    private static long? Length(FileSpecification file) =>
        file.Contents is { CanSeek: true } contents ? contents.Length : null;

    private sealed record Expected<T>(string Id, T Value);

    private sealed record RedactionExpectation(string Id, Regex Pattern, IReadOnlyList<int>? Pages);

    private sealed record BookmarkExpectation(string Id, string Index, string Title, int? Page);
}
