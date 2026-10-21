using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using static Aspose.Cli.Product.Pdf.Engine.PdfTextMatch;

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
    private readonly Expectations<string> _fields;
    private readonly Expectations<string> _metadata;
    private readonly Expectations<long?> _addedAttachments;
    // A deletion expects only that no attachment of its name is left, so it keeps no value.
    private readonly Expectations<ValueTuple> _removedAttachments;
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
        _fields = new(_incomplete);
        _metadata = new(_incomplete);
        _addedAttachments = new(_incomplete);
        _removedAttachments = new(_incomplete);
        _expectedPages = document.Pages.Count;
        _expectedBookmarks = PdfOutlineAddresses.CountOutline(document.Outlines);
    }

    /// <summary>
    /// Records the effect of one operation that applied, against the document it left. An effect
    /// it cannot read throws, and the SDK leaves the operation unchecked.
    /// </summary>
    public void Record(PdfOp operation, AppliedOperation applied)
    {
        string id = operation.Id!;
        _recorded.Add((id, PdfOp.Catalog.NameOf(operation)));
        operation.Accept(new Recording(this, id, applied.ItemsAffected));
    }

    /// <summary>
    /// Counts the pages a page operation leaves. Page numbers recorded before it name other pages
    /// now, so a redaction limited to pages and a bookmark's page can no longer be checked.
    /// </summary>
    private void InvalidatePositions(string id, int pages)
    {
        _expectedPages += pages;
        _pageOps.Add(id);
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
    /// matches each pattern still has, without those of the evaluation watermark
    /// (<see cref="PdfRedactionAudit.Remaining"/>). Before publishing, every edit has already
    /// refused an output that still holds a redaction's text (<see cref="PdfRedactionAudit"/>);
    /// this records which redactions were read back.
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
            (string text, string? watermark) = PdfRedactionAudit.PageText(output.Pages[number], license);
            string location = string.Create(CultureInfo.InvariantCulture, $"page/{number}");
            foreach (RedactionExpectation redaction in _redactions.Where(redaction => redaction.Pages?.Contains(number) ?? true))
            {
                int remaining;
                try
                {
                    remaining = PdfRedactionAudit.Remaining(redaction.Pattern, text, watermark);
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
        int count = PdfOutlineAddresses.CountOutline(output.Outlines);
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

        foreach ((string name, Expected<ValueTuple> removal) in _removedAttachments)
        {
            _checked.Add(removal.Id);
            Report(name, removal.Id, present[name].Any() ? $"attachment '{name}' is still in the output" : null);
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

    private sealed record Expected<T>(string Id, T Value);

    private sealed record RedactionExpectation(string Id, Regex Pattern, IReadOnlyList<int>? Pages);

    private sealed record BookmarkExpectation(string Id, string Index, string Title, int? Page);

    /// <summary>
    /// The expectations of one kind, one per key: setting a key again, or dropping it, leaves the
    /// operation that set it before unchecked.
    /// </summary>
    /// <param name="incomplete">The operations left unchecked.</param>
    private sealed class Expectations<T>(HashSet<string> incomplete) : IEnumerable<KeyValuePair<string, Expected<T>>>
    {
        private readonly Dictionary<string, Expected<T>> _items = new(StringComparer.Ordinal);

        public int Count => _items.Count;

        public IReadOnlyCollection<string> Keys => _items.Keys;

        public void Set(string key, string id, T value)
        {
            Drop(key);
            _items[key] = new Expected<T>(id, value);
        }

        public void Drop(string key)
        {
            if (_items.Remove(key, out Expected<T>? earlier))
            {
                incomplete.Add(earlier.Id);
            }
        }

        public void DropAll()
        {
            foreach (string key in _items.Keys.ToArray())
            {
                Drop(key);
            }
        }

        public IEnumerator<KeyValuePair<string, Expected<T>>> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Records what one operation should leave in the output; an operation without a reliable
    /// read-back records nothing.
    /// </summary>
    /// <param name="verifier">The verifier that keeps the expectations.</param>
    /// <param name="id">The operation's id.</param>
    /// <param name="affected">The items the operation affected, as its snapshots counted them.</param>
    private sealed class Recording(PdfEditVerifier verifier, string id, long affected) : IPdfOpHandler<ValueTuple>
    {
        private Document Document => verifier._document;

        public ValueTuple Apply(SetFormFieldOp operation)
        {
            verifier._fields.Set(operation.Name, id,
                operation.Value ?? PdfMutationSupport.ClearedFieldValue(PdfMutationSupport.FormField(Document, operation.Name)));
            return default;
        }

        public ValueTuple Apply(FlattenFormsOp operation)
        {
            foreach (string name in operation.Fields ?? [.. verifier._fields.Keys])
            {
                verifier._fields.Drop(name);
            }

            verifier._flattened.Add(new Expected<IReadOnlyList<string>?>(id, operation.Fields));
            return default;
        }

        public ValueTuple Apply(RedactTextOp operation)
        {
            verifier._redactions.Add(new RedactionExpectation(
                id,
                TextPattern(operation.Find, operation.Regex, operation.CaseSensitive),
                operation.Pages is null ? null : PdfMutationSupport.Resolve(Document, operation.Pages)));
            return default;
        }

        public ValueTuple Apply(SetPropertiesOp operation)
        {
            foreach ((string key, string value) in PdfPropertyNames.Written(operation))
            {
                verifier._metadata.Set(key, id, value);
            }

            return default;
        }

        public ValueTuple Apply(ClearPropertiesOp operation)
        {
            if (operation.DocumentInfo)
            {
                verifier._metadata.DropAll();
            }

            return default;
        }

        public ValueTuple Apply(AddAttachmentOp operation)
        {
            string added = operation.Name ?? Path.GetFileName(operation.Path);
            verifier._removedAttachments.Drop(added);

            // The stream the handler embedded, never the source file again.
            verifier._addedAttachments.Set(added, id, PdfInfoProjection.AttachmentEntries(Document)
                .LastOrDefault(entry => entry.Info.Name == added).File is { } file ? PdfInfoProjection.Unreported.Length(file) : null);
            return default;
        }

        public ValueTuple Apply(DeleteAttachmentOp operation)
        {
            verifier._addedAttachments.Drop(operation.Name);
            verifier._removedAttachments.Set(operation.Name, id, default);
            return default;
        }

        public ValueTuple Apply(AddBookmarkOp operation)
        {
            verifier._expectedBookmarks++;
            verifier._bookmarkOps.Add(id);
            verifier._bookmarks.Add(new BookmarkExpectation(id, NewBookmarkIndex(Document, operation.Parent), operation.Title, operation.Page));
            return default;
        }

        public ValueTuple Apply(DeleteBookmarksOp operation)
        {
            verifier._expectedBookmarks -= (int)affected;
            verifier._bookmarkOps.Add(id);
            // The bookmarks after the deleted ones moved up.
            verifier._incomplete.UnionWith(verifier._bookmarks.Select(static bookmark => bookmark.Id));
            verifier._bookmarks.Clear();
            return default;
        }

        public ValueTuple Apply(InsertPageOp operation)
        {
            verifier.InvalidatePositions(id, pages: 1);
            return default;
        }

        public ValueTuple Apply(ImportPagesOp operation)
        {
            verifier.InvalidatePositions(id, pages: (int)affected);
            // The inserted pages may bring form fields of their own.
            verifier._incomplete.UnionWith(verifier._flattened.Select(static flatten => flatten.Id));
            verifier._flattened.Clear();
            return default;
        }

        public ValueTuple Apply(DeletePagesOp operation)
        {
            verifier.InvalidatePositions(id, pages: -(int)affected);
            verifier._fields.DropAll();
            return default;
        }

        public ValueTuple Apply(MovePagesOp operation)
        {
            verifier.InvalidatePositions(id, pages: 0);
            return default;
        }

        // No reliable read-back: these record nothing and stay unchecked.
        public ValueTuple Apply(RotatePagesOp operation) => default;

        public ValueTuple Apply(CropPagesOp operation) => default;

        public ValueTuple Apply(SetPageSizeOp operation) => default;

        public ValueTuple Apply(AddWatermarkTextOp operation) => default;

        public ValueTuple Apply(AddWatermarkImageOp operation) => default;

        public ValueTuple Apply(AddPageNumbersOp operation) => default;

        public ValueTuple Apply(AddHeaderTextOp operation) => default;

        public ValueTuple Apply(AddFooterTextOp operation) => default;

        public ValueTuple Apply(AddStampImageOp operation) => default;

        public ValueTuple Apply(AddHyperlinkOp operation) => default;

        public ValueTuple Apply(RedactAreaOp operation) => default;

        public ValueTuple Apply(SetPageLabelsOp operation) => default;

        public ValueTuple Apply(EncryptDocumentOp operation) => default;

        public ValueTuple Apply(DecryptDocumentOp operation) => default;

        public ValueTuple Apply(OptimizeDocumentOp operation) => default;
    }
}
