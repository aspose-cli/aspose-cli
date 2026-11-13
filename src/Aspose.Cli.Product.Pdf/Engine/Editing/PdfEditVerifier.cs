using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Pdf;
using Aspose.Pdf.Forms;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// Records what each applied operation of a batch should leave in the output, then reads the
/// staged output back and reports every expectation it does not meet; the SDK runner records
/// each applied operation and turns the read-back into the verification. Only effects with a
/// reliable read-back are recorded: form field values, flattened fields, bookmarks, document
/// information, attachments, web links and the page count. Redacted text needs no record: before
/// publishing, every edit searched the output for it (<see cref="PdfRedactionAudit"/>) and would
/// have published nothing, so a redact_text that reaches this read-back is checked. The batch is checked as a whole: an
/// expectation a later operation supersedes (the same field, key or attachment set again, the
/// outline or the pages renumbered, a field flattened or covered by a redact_area, a link's page
/// changed or redacted) is dropped, a
/// field a later redact_text clears is expected clear, a field whose only widget a redact_area
/// covers is expected gone, and an operation is reported as checked only
/// when every effect it recorded was read back. The output is read as <c>pdf inspect</c> reads it
/// (<see cref="PdfInfoProjection"/>), and an issue is located under the object's address there.
/// </summary>
internal sealed class PdfEditVerifier : IEditVerifier<PdfOp>
{
    private const string LaterOperationChangedIt = "A later operation of the batch changed or renumbered what this one set, so its own effect could not be read back.";
    private const string CoverNotReadBack = "Only that the form fields an earlier set_form_field of the batch set and its area covers are gone was read back; render the page to confirm the cover.";

    private readonly Document _document;
    private readonly PdfPageIdentities _pages;
    private readonly List<(string Id, string Op)> _recorded = [];
    private readonly HashSet<string> _incomplete = new(StringComparer.Ordinal);
    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);
    private readonly Expectations<string> _fields;
    // Where the widgets of each field a set_form_field set lie, as it left them: on a page by its identity, which page operations keep.
    private readonly Dictionary<string, (PdfPageIdentity? Page, Rectangle Rect)[]> _widgets = new(StringComparer.Ordinal);
    // Each field a redact_area covers the only widget of, which the output must not hold, with the operation's id.
    private readonly List<(string Name, string Id)> _covered = [];
    private readonly Expectations<string> _metadata;
    private readonly Expectations<long?> _addedAttachments;
    // A deletion expects only that no attachment of its name is left, so it keeps no value.
    private readonly Expectations<ValueTuple> _removedAttachments;
    private readonly List<Expected<IReadOnlyList<string>?>> _flattened = [];
    private readonly List<BookmarkExpectation> _bookmarks = [];
    private readonly List<string> _pageOps = [];
    private readonly List<string> _bookmarkOps = [];
    // Each web link an add_hyperlink added, as its operation gave it, until a later operation moves or redacts its page.
    private readonly List<(string Id, AddHyperlinkOp Link)> _links = [];
    private int _expectedPages;
    private int _expectedBookmarks;

    /// <param name="document">The document the batch edits, which each record reads as the operation left it.</param>
    /// <param name="pages">The identities of its pages, which the batch's page operations keep; a set of its own when omitted.</param>
    internal PdfEditVerifier(Document document, PdfPageIdentities? pages = null)
    {
        _document = document;
        _pages = pages ?? new PdfPageIdentities();
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
    /// now, so a bookmark's page can no longer be checked; a widget's page is known by its identity.
    /// </summary>
    private void InvalidatePositions(string id, int pages)
    {
        _expectedPages += pages;
        _pageOps.Add(id);
        DropLinks();
        for (int index = 0; index < _bookmarks.Count; index++)
        {
            _incomplete.Add(_bookmarks[index].Id);
            _bookmarks[index] = _bookmarks[index] with { Page = null };
        }
    }

    /// <summary>Reads the reopened output back against every recorded expectation.</summary>
    internal VerificationFacts Verify(Document output)
    {
        var issues = new List<VerificationIssue>();
        VerifyPages(output, issues);
        VerifyFields(output, issues);
        VerifyFlattened(output, issues);
        VerifyBookmarks(output, issues);
        VerifyMetadata(output, issues);
        VerifyAttachments(output, issues);
        VerifyLinks(output, issues);
        Dictionary<string, string> reasons = _incomplete.ToDictionary(static id => id, static _ => LaterOperationChangedIt, StringComparer.Ordinal);
        foreach ((_, string id) in _covered)
        {
            reasons.TryAdd(id, CoverNotReadBack);
        }

        return new VerificationFacts(issues, [.. _checked.Where(id => !_incomplete.Contains(id))]) { UncheckedReasons = reasons };
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

        foreach ((string name, string id) in _covered.Where(cover => fields.ContainsKey(cover.Name)))
        {
            issues.Add(Issue(
                PdfDiagnostics.FieldValueMismatch,
                [id],
                $"form field '{name}' is still in the output, although the area covers its only widget",
                location: PdfInfoProjection.Address("field", name),
                hint: "Read the field with 'pdf inspect --detail forms' and clear its value with set_form_field."));
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

        Dictionary<string, (PdfOutlineItem Item, int Destination)> outline = PdfInfoProjection.Outline(output, output.Outlines, parent: null, int.MaxValue, int.MaxValue)
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

    /// <summary>
    /// Changing a page's geometry or covering an area of it moves or may remove the links on it, so
    /// no link of <paramref name="page"/>, or of any page when null, is expected.
    /// </summary>
    private void DropLinks(int? page = null)
    {
        _incomplete.UnionWith(_links.Where(link => page is null || link.Link.Page == page).Select(static link => link.Id));
        _links.RemoveAll(link => page is null || link.Link.Page == page);
    }

    /// <summary>Each added link is a web link of its page in the output, as <c>pdf inspect --detail links</c> reads it, with its address and area.</summary>
    private void VerifyLinks(Document output, List<VerificationIssue> issues)
    {
        ILookup<int, PdfLinkInfo> links = PdfInfoProjection.Links(output).ToLookup(static link => link.Page);
        foreach ((string id, AddHyperlinkOp expected) in _links)
        {
            _checked.Add(id);
            if (!links[expected.Page].Any(link => link.Url == expected.Url && Near(link.Rect, expected.Rect)))
            {
                issues.Add(Issue(
                    PdfDiagnostics.LinkMismatch,
                    [id],
                    $"page {expected.Page} of the output has no link to '{expected.Url}' over the area the operation gave",
                    location: $"page/{expected.Page}",
                    hint: "Compare 'pdf inspect --detail links' of the output with the batch."));
            }
        }

        // A read reports points to the hundredth.
        static bool Near(PdfRect read, PdfRectInput given) =>
            new[] { read.X - given.X, read.Y - given.Y, read.Width - given.Width, read.Height - given.Height }.All(static difference => Math.Abs(difference) <= 0.01);
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

        /// <summary>Changes the value a key is expected to hold, keeping the operation that set it.</summary>
        public void Expect(string key, T value) => _items[key] = _items[key] with { Value = value };

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
            // A field the document does not hold has no widget to place, so a later redact_area leaves its value unchecked.
            (PdfPageIdentity? Page, Rectangle Rect)[] widgets = [.. Document.Form.Fields
                .Where(field => string.Equals(field.FullName, operation.Name, StringComparison.Ordinal))
                .Select(field => (PdfForms.PageOf(field) is { } page ? verifier._pages.Of(Document.Pages[page]) : null, field.Rect))];
            if (widgets.Length > 0)
            {
                verifier._widgets[operation.Name] = widgets;
            }
            else
            {
                verifier._widgets.Remove(operation.Name);
            }

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

        /// <summary>
        /// A field it clears is expected clear, by the operation that set it; a field an earlier
        /// operation removed, such as a redact_area over it, is not there to clear.
        /// </summary>
        public ValueTuple Apply(RedactTextOp operation)
        {
            verifier._checked.Add(id);
            IReadOnlyList<int>? pages = operation.Pages is null ? null : PdfMutationSupport.ResolveOptional(Document, operation.Pages);
            Regex pattern = PdfTextMatch.TextPattern(operation.Find, operation.Regex, operation.CaseSensitive);
            foreach ((string name, Expected<string> expected) in verifier._fields.ToArray())
            {
                if (Document.Form.Fields.FirstOrDefault(field => string.Equals(field.FullName, name, StringComparison.Ordinal)
                    && PdfMutationHandlers.Clears(field, expected.Value, pages, pattern)) is { } cleared)
                {
                    verifier._fields.Expect(name, PdfMutationSupport.ClearedFieldValue(cleared));
                }
            }

            return default;
        }

        /// <summary>
        /// A field with a widget the area <see cref="PdfMutationHandlers.Covers"/> goes with it, so
        /// the operation that set it is left unchecked, as after flatten_forms, and a field whose
        /// only widget it covers must be gone from the output; a field whose widgets are not known,
        /// because the field was not found or pages were imported beside it, is left unchecked too. The cover itself has no reliable read-back, so the
        /// operation stays unchecked.
        /// </summary>
        public ValueTuple Apply(RedactAreaOp operation)
        {
            verifier.DropLinks(operation.Page);
            Page page = PdfPages.PageAt(Document, operation.Page);
            Rectangle area = PdfGeometry.ToPdfRect(page, operation.Rect);
            PdfPageIdentity identity = verifier._pages.Of(page);
            foreach (string name in verifier._fields.Keys.ToArray())
            {
                (PdfPageIdentity? Page, Rectangle Rect)[]? widgets = verifier._widgets.GetValueOrDefault(name);
                bool[] covered = [.. (widgets ?? []).Select(widget => widget.Page == identity && PdfMutationHandlers.Covers(area, widget.Rect))];
                if (widgets is null || covered.Contains(true))
                {
                    verifier._fields.Drop(name);
                }

                if (covered is [true])
                {
                    verifier._covered.Add((name, id));
                }
            }

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
            // The inserted pages may bring form fields of their own, with widgets of their names.
            verifier._widgets.Clear();
            verifier._incomplete.UnionWith(verifier._flattened.Select(static flatten => flatten.Id));
            verifier._flattened.Clear();
            verifier._covered.Clear();
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

        public ValueTuple Apply(AddHyperlinkOp operation)
        {
            verifier._links.Add((id, operation));
            return default;
        }

        // No reliable read-back: these record nothing and stay unchecked. A change of a page's
        // geometry moves the area of its links.
        public ValueTuple Apply(RotatePagesOp operation) => Drop();

        public ValueTuple Apply(CropPagesOp operation) => Drop();

        public ValueTuple Apply(SetPageSizeOp operation) => Drop();

        public ValueTuple Apply(AddWatermarkTextOp operation) => default;

        public ValueTuple Apply(AddWatermarkImageOp operation) => default;

        public ValueTuple Apply(AddPageNumbersOp operation) => default;

        public ValueTuple Apply(AddHeaderTextOp operation) => default;

        public ValueTuple Apply(AddFooterTextOp operation) => default;

        public ValueTuple Apply(AddStampImageOp operation) => default;

        public ValueTuple Apply(SetPageLabelsOp operation) => default;

        public ValueTuple Apply(EncryptDocumentOp operation) => default;

        public ValueTuple Apply(DecryptDocumentOp operation) => default;

        public ValueTuple Apply(OptimizeDocumentOp operation) => default;

        private ValueTuple Drop()
        {
            verifier.DropLinks();
            return default;
        }
    }
}
