using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Operators;
using static Aspose.Cli.Product.Pdf.Engine.Editing.PdfMutationSupport;
using static Aspose.Cli.Product.Pdf.Engine.Mapping.PdfOutlineAddresses;
using static Aspose.Cli.Product.Pdf.Engine.PdfPages;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

/// <summary>
/// The objects of a PDF each operation can change, as the SDK snapshots them around the
/// operation (<see cref="IOperationModel{TOp}.Snapshot"/>): the pages it selects (<c>page/3</c>,
/// by the rotation, boxes, annotations and resources, and the content for a redaction), the page order (by page
/// identity), the document information and XMP entries (<c>property/title</c>,
/// <c>property/xmp:dc:title</c>), the bookmarks (<c>bookmark/1/2</c>), the attachments
/// (<c>attachment/notes.txt</c>), the page labels (<c>page-label/1</c>), the form fields
/// (<c>field/name</c>), the encryption (<c>security/encryption</c>) and, for optimize, the
/// images and fonts of every page (<c>page/1/image/1</c>, <c>page/1/font/1</c>). What a read
/// reports of these objects is the state <see cref="PdfInfoProjection"/> reads, and an object
/// with a name is addressed by <see cref="PdfInfoProjection.Address"/>. A page, and an image or
/// font on it, names the page's identity as its owner, so the batch numbers it where its output
/// holds the page (<see cref="PdfEditModel.Positions"/>).
/// </summary>
internal sealed class PdfSnapshots(Document document, PdfPageIdentities pages, Func<string?> encryption) : IPdfOpHandler<PdfSnapshots.Scope>
{
    /// <summary>The unit of a page's address, whose changed ones are the pages the batch touched.</summary>
    internal const string PageUnit = "page";

    // The state of each page read since the last operation applied, without and with its
    // content stream: nothing changed it since, so the snapshot before an operation reuses what
    // the snapshot after the previous one read.
    private readonly Dictionary<Page, string> _pageStates = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Page, string> _pageContents = new(ReferenceEqualityComparer.Instance);

    /// <summary>Forgets the page states read so far; called before an operation applies.</summary>
    internal void Invalidate()
    {
        _pageStates.Clear();
        _pageContents.Clear();
    }

    private string State(Page page, bool content)
    {
        Dictionary<Page, string> states = content ? _pageContents : _pageStates;
        if (!states.TryGetValue(page, out string? state))
        {
            state = PageState(page, content);
            states[page] = state;
        }

        return state;
    }

    /// <summary>
    /// The names of the document information entries Aspose.PDF writes into every PDF it saves,
    /// licensed or not, whatever the document held: the library as creator and producer, and the
    /// time of the save. No operation removes or sets them in the output. A save while other
    /// documents of the process are saved leaves the date out (PDF-CONCURRENT-SAVE-MODDATE), which
    /// a command, run in a process of its own, never meets.
    /// </summary>
    internal static readonly IReadOnlySet<string> SaveStamped = new HashSet<string>(["creator", "producer", "modificationDate"], StringComparer.Ordinal);

    /// <summary>
    /// What one operation can change: the addresses it attempts to change, reported when it
    /// fails, and the snapshot of the objects it can change, taken when asked. The targets are a
    /// page selection as written (<c>page/2-4</c>, <c>page/all</c> without one) and the named
    /// objects of the operation, addressed by <see cref="PdfInfoProjection.Address"/>; reading
    /// them never touches the document.
    /// </summary>
    internal readonly record struct Scope(IReadOnlyList<string> Targets, Func<OperationSnapshot> Take);

    public Scope Apply(RotatePagesOp operation) => Pages(operation.Pages);

    public Scope Apply(DeletePagesOp operation) => PageOrder(Selection(operation.Pages));

    /// <summary>The page order, naming the moved pages, each of which counts once at its new number.</summary>
    public Scope Apply(MovePagesOp operation) => new(Selection(operation.Pages), () => PageIdentities(
        [.. Resolve(document, operation.Pages).Select(number => pages.Of(document.Pages[number]).Name)]));

    public Scope Apply(ImportPagesOp operation) => PageOrder(Numbered(operation.At));

    public Scope Apply(InsertPageOp operation) => PageOrder(Numbered(operation.At));

    public Scope Apply(CropPagesOp operation) => Pages(operation.Pages);

    public Scope Apply(SetPageSizeOp operation) => Pages(operation.Pages);

    public Scope Apply(AddWatermarkTextOp operation) => Pages(operation.Pages);

    public Scope Apply(AddWatermarkImageOp operation) => Pages(operation.Pages);

    public Scope Apply(AddPageNumbersOp operation) => Pages(operation.Pages);

    public Scope Apply(AddHeaderTextOp operation) => Pages(operation.Pages);

    public Scope Apply(AddFooterTextOp operation) => Pages(operation.Pages);

    public Scope Apply(AddStampImageOp operation) => Page(operation.Page);

    public Scope Apply(AddHyperlinkOp operation) => Page(operation.Page);

    public Scope Apply(RedactTextOp operation) => Pages(operation.Pages, content: true);

    public Scope Apply(RedactAreaOp operation) => Page(operation.Page, content: true);

    public Scope Apply(SetPropertiesOp operation) =>
        new([.. PdfPropertyNames.Written(operation).Select(static entry => PdfInfoProjection.PropertyAddress(entry.Key))], Metadata);

    public Scope Apply(ClearPropertiesOp operation) => new(["property/all"], Metadata);

    /// <summary>
    /// The bookmarks at the level the new one joins, with what lies below its parent: appending
    /// it changes nothing else of the outline.
    /// </summary>
    public Scope Apply(AddBookmarkOp operation) => new([$"bookmark/{operation.Parent ?? "all"}"], () => operation.Parent is { } parent
        ? Bookmarks(Outline(document.Outlines, parent), parent, int.MaxValue)
        : Bookmarks(document.Outlines, parent: null, levels: 1));

    public Scope Apply(DeleteBookmarksOp operation) => new(
        operation.Bookmarks is null ? ["bookmark/all"] : [.. operation.Bookmarks.Select(static address => $"bookmark/{address}")],
        () => Bookmarks(document.Outlines, parent: null, int.MaxValue));

    public Scope Apply(AddAttachmentOp operation) =>
        new([PdfInfoProjection.Address("attachment", operation.Name ?? Path.GetFileName(operation.Path))], Attachments);

    public Scope Apply(DeleteAttachmentOp operation) => new([PdfInfoProjection.Address("attachment", operation.Name)], Attachments);

    public Scope Apply(SetPageLabelsOp operation) => new([.. operation.Ranges.Select(static range => $"page-label/{range.StartPage}")], PageLabels);

    public Scope Apply(SetFormFieldOp operation) => new([PdfInfoProjection.Address("field", operation.Name)], () => Fields([operation.Name]));

    public Scope Apply(FlattenFormsOp operation) => new(
        operation.Fields is null ? ["field/all"] : [.. operation.Fields.Select(static name => PdfInfoProjection.Address("field", name))],
        () => Fields(operation.Fields));

    public Scope Apply(EncryptDocumentOp operation) => new([SecurityAddress], Security);

    public Scope Apply(DecryptDocumentOp operation) => new([SecurityAddress], Security);

    public Scope Apply(OptimizeDocumentOp operation) => new([$"{PageUnit}/all"], OptimizedObjects);

    private const string SecurityAddress = "security/encryption";

    /// <summary>The pages a selection names, or every page without one, each by its content.</summary>
    private Scope Pages(string? selection, bool content = false) => new(Selection(selection), () => SelectedPages(selection, content));

    private Scope Page(int number, bool content = false) => new(Numbered(number), () => SinglePage(number, content));

    /// <summary>The order of the pages, each by its identity: inserting, deleting or moving pages changes it.</summary>
    private Scope PageOrder(IReadOnlyList<string> targets) => new(targets, PageIdentities);

    private static string[] Selection(string? selection) => [$"{PageUnit}/{selection ?? "all"}"];

    private static string[] Numbered(int number) => [Address(PageUnit, number)];

    private OperationSnapshot PageLabels()
    {
        var snapshot = new OperationSnapshot();
        foreach (PdfPageLabelInfo label in PdfInfoProjection.PageLabels(document))
        {
            snapshot.Add(Address("page-label", label.StartPage), $"{label.NumberStyle}|{label.Prefix}|{label.StartingValue}");
        }

        return snapshot;
    }

    /// <summary>
    /// What optimizing changes that a reader of the file can see: each image a page draws,
    /// directly or through a form, by its size, compression and encoded data, and each font by
    /// whether it is embedded. Removing unused objects and compressing streams store the same
    /// content in fewer bytes and change no object.
    /// </summary>
    private OperationSnapshot OptimizedObjects()
    {
        var snapshot = new OperationSnapshot();
        for (int number = 1; number <= document.Pages.Count; number++)
        {
            int images = 0;
            int fonts = 0;
            PdfPageIdentity owner = pages.Of(document.Pages[number]);
            Add(document.Pages[number].Resources, depth: 0);

            void Add(Resources? resources, int depth)
            {
                // Forms nest; a form that draws itself must not recurse forever.
                if (resources is null || depth > 8)
                {
                    return;
                }

                foreach (XImage image in (IEnumerable<XImage>?)resources.Images ?? [])
                {
                    snapshot.Add(
                        string.Create(CultureInfo.InvariantCulture, $"page/{number}/image/{++images}"),
                        string.Create(CultureInfo.InvariantCulture,
                            $"{image.Width}x{image.Height}|{image.FilterType}|{ImageDigest(image)}"),
                        owner);
                }

                foreach (Aspose.Pdf.Text.Font font in (IEnumerable<Aspose.Pdf.Text.Font>?)resources.Fonts ?? [])
                {
                    snapshot.Add(
                        string.Create(CultureInfo.InvariantCulture, $"page/{number}/font/{++fonts}"),
                        $"{font.FontName}|{font.IsEmbedded}|{font.IsSubset}",
                        owner);
                }

                foreach (XForm form in (IEnumerable<XForm>?)resources.Forms ?? [])
                {
                    Add(form.Resources, depth + 1);
                }
            }
        }

        return snapshot;
    }

    private OperationSnapshot SelectedPages(string? selection, bool content)
    {
        var snapshot = new OperationSnapshot();
        foreach (int number in ResolveOptional(document, selection))
        {
            snapshot.Add(Address(PageUnit, number), State(document.Pages[number], content), pages.Of(document.Pages[number]));
        }

        return snapshot;
    }

    private OperationSnapshot SinglePage(int number, bool content)
    {
        Page page = PageAt(document, number);
        return new OperationSnapshot().Add(Address(PageUnit, number), State(page, content), pages.Of(page));
    }

    private OperationSnapshot PageIdentities() => PageIdentities(moved: null);

    // The SDK reads the moved pages from the snapshot before the operation, when the selection
    // still names them.
    private OperationSnapshot PageIdentities(IReadOnlyList<string>? moved)
    {
        PdfPageIdentity[] order = pages.Order(document);
        return new OperationSnapshot().AddSequence(PageUnit, order.Select(static page => page.Name), moved, order);
    }

    /// <summary>
    /// The document information and XMP entries as the output will hold them, by their stored
    /// text: an entry the engine writes on every save (<see cref="SaveStamped"/>) is not one an
    /// operation changes, and an empty entry is one the output does not report.
    /// </summary>
    private OperationSnapshot Metadata()
    {
        var snapshot = new OperationSnapshot();
        foreach ((string name, string address, string stored) in PdfInfoProjection.Unreported.Properties(document))
        {
            if (!SaveStamped.Contains(name))
            {
                snapshot.Add(address, stored);
            }
        }

        return snapshot;
    }

    /// <summary>
    /// The bookmarks of <paramref name="outline"/>, below the bookmark at <paramref name="parent"/>
    /// (the top level when null), in outline order down to <paramref name="levels"/> levels, each
    /// by its level, title and stored destination page.
    /// </summary>
    private static OperationSnapshot Bookmarks(IEnumerable<OutlineItemCollection> outline, string? parent, int levels) =>
        new OperationSnapshot().AddSequence("bookmark", PdfInfoProjection.Outline(outline, parent, levels, int.MaxValue)
            .Select(static entry => ($"bookmark/{entry.Item.Bookmark}",
                string.Create(CultureInfo.InvariantCulture, $"{entry.Item.Level}|{entry.Item.Title}|{entry.Destination}"))));

    /// <summary>The named attachments in the order the engine lists them, each by its stored description, media type and length.</summary>
    private OperationSnapshot Attachments() =>
        new OperationSnapshot().AddSequence("attachment", PdfInfoProjection.AttachmentEntries(document)
            .Where(static entry => entry.Info.Name.Length > 0)
            // The name is part of the state: the engine keeps attachments in name order, so two
            // with the same content are told apart by name, not by where they stand.
            .Select(static entry => (PdfInfoProjection.Address("attachment", entry.Info.Name), $"{entry.Info.Name}|{PdfInfoProjection.Unreported.Attachment(entry.File)}")));

    /// <summary>
    /// The form fields of the names an operation lists, or every field without a list, in order,
    /// each by the kind and value a read reports; a flattened field is gone.
    /// </summary>
    private OperationSnapshot Fields(IReadOnlyList<string>? names) =>
        new OperationSnapshot().AddSequence("field", document.Form.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.FullName) && (names is null || names.Contains(field.FullName, StringComparer.Ordinal)))
            .Select(static field => (PdfInfoProjection.Address("field", field.FullName), PdfForms.State(field).ToString())));

    /// <summary>
    /// Whether the document is encrypted and, when it is, with the encryption an operation set
    /// (its password variables and permissions) or the input's own.
    /// </summary>
    private OperationSnapshot Security() =>
        new OperationSnapshot().Add(SecurityAddress, document.IsEncrypted
            ? encryption() ?? string.Create(CultureInfo.InvariantCulture, $"input|{(int)document.Permissions}")
            : "none");

    /// <summary>A digest of an image's encoded data.</summary>
    private static string ImageDigest(XImage image)
    {
        using MemoryStream? data = image.GetRawImageData();
        // The stream the engine returns may stand at its end.
        return Convert.ToHexStringLower(SHA256.HashData(data?.ToArray() ?? []));
    }

    private static string Address(string unit, int number) => string.Create(CultureInfo.InvariantCulture, $"{unit}/{number}");

    /// <summary>
    /// A digest of what an operation can change on a page: its stored rotation and boxes, its annotations and the forms and images it draws, which a stamp adds to;
    /// with <paramref name="content"/>, also its content stream, which only a redaction rewrites
    /// in place. Reading the content stream parses it, which costs as much again after every
    /// change, so the others leave it out.
    /// </summary>
    private static string PageState(Page page, bool content)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, PdfInfoProjection.Unreported.Geometry(page));
        if (content)
        {
            // Searching a page's text can wrap its content in a saved graphics state
            // (PDF-TEXT-SEARCH-WRAPS-CONTENT), which draws nothing different.
            foreach (Operator operation in page.Contents.Where(static operation => operation is not (GSave or GRestore)))
            {
                Append(hash, operation.ToString() ?? string.Empty);
            }
        }

        foreach (Annotation annotation in page.Annotations)
        {
            Append(hash, $"{annotation.GetType().Name}|{PdfInfoProjection.Unreported.Box(annotation.Rect)}");
        }

        foreach (XForm form in (IEnumerable<XForm>?)page.Resources.Forms ?? [])
        {
            Append(hash, "form|" + form.Name);
        }

        foreach (XImage image in (IEnumerable<XImage>?)page.Resources.Images ?? [])
        {
            Append(hash, string.Create(CultureInfo.InvariantCulture, $"image|{image.Name}|{image.Width}x{image.Height}"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string text)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(text));
        hash.AppendData([0]);
    }
}

/// <summary>
/// The identity of each page of the document being edited, which survives inserting and
/// deleting other pages; a page the editor copies to move it takes its original's identity.
/// </summary>
internal sealed class PdfPageIdentities
{
    private readonly ConditionalWeakTable<Page, PdfPageIdentity> _identities = [];
    private int _next;

    /// <summary>The page's identity, given on first sight.</summary>
    internal PdfPageIdentity Of(Page page) =>
        _identities.GetValue(page, _ => new PdfPageIdentity("p" + (++_next).ToString(CultureInfo.InvariantCulture)));

    /// <summary>The identity of each page of <paramref name="document"/>, in page order.</summary>
    internal PdfPageIdentity[] Order(Document document) =>
        [.. Enumerable.Range(1, document.Pages.Count).Select(number => Of(document.Pages[number]))];

    /// <summary>Gives <paramref name="copy"/> the identity of <paramref name="original"/>.</summary>
    internal void Carry(Page original, Page copy) => _identities.AddOrUpdate(copy, Of(original));
}

/// <summary>One page's identity: the same instance wherever the editor moves the page, and a name that tells it from the others.</summary>
internal sealed class PdfPageIdentity(string name)
{
    internal string Name { get; } = name;
}
