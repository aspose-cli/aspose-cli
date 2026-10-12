using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Structural information returned by <c>pdf inspect</c>.</summary>
public sealed record PdfInspectResult() : EngineResultEnvelope("inspect-result", 2)
{
    /// <summary>The most pages <see cref="Pages"/> lists.</summary>
    public const int PagePreviewLimit = 20;

    /// <summary>The most bookmarks <see cref="Outline"/> lists.</summary>
    public const int OutlineLimit = 200;

    /// <summary>The most links <see cref="Links"/> lists.</summary>
    public const int LinkLimit = 500;

    /// <summary>The most form fields <see cref="PdfFormInfo.Fields"/> lists.</summary>
    public const int FieldLimit = 500;

    /// <summary>The inspected PDF.</summary>
    [JsonPropertyOrder(-49)]
    public required InputInfo Input { get; init; }

    /// <summary>The document summary every inspection reports.</summary>
    public required PdfSummary Pdf { get; init; }

    /// <summary>
    /// The first pages' geometry with <c>--sample</c>; a <c>LIST_TRUNCATED</c> warning names the
    /// page count when there are more.
    /// </summary>
    [MaxItems(PagePreviewLimit)]
    public IReadOnlyList<PdfPageInfo>? Pages { get; init; }

    /// <summary>The page-label ranges, when the file has any.</summary>
    public IReadOnlyList<PdfPageLabelInfo>? PageLabels { get; init; }

    /// <summary>
    /// Bookmarks in reading order, depth first, with <c>--detail outline</c>; a
    /// <c>LIST_TRUNCATED</c> warning names the total when there are more.
    /// </summary>
    [MaxItems(OutlineLimit)]
    public IReadOnlyList<PdfOutlineItem>? Outline { get; init; }

    /// <summary>
    /// The links to web addresses in page order, with <c>--detail links</c>; a
    /// <c>LIST_TRUNCATED</c> warning names the total when there are more.
    /// </summary>
    [MaxItems(LinkLimit)]
    public IReadOnlyList<PdfLinkInfo>? Links { get; init; }

    /// <summary>The interactive form and its fields' values, with <c>--detail forms</c>.</summary>
    public PdfFormInfo? Forms { get; init; }

    /// <summary>The embedded files, with <c>--detail attachments</c>.</summary>
    public IReadOnlyList<PdfAttachmentInfo>? Attachments { get; init; }

    /// <summary>The distinct font resources, with <c>--detail fonts</c>.</summary>
    public IReadOnlyList<PdfFontInfo>? Fonts { get; init; }

    /// <summary>The passwords and permissions, with <c>--detail permissions</c>.</summary>
    public PdfPermissionInfo? Permissions { get; init; }

    /// <summary>The signature fields, with <c>--detail signatures</c>.</summary>
    public IReadOnlyList<PdfSignatureInfo>? Signatures { get; init; }

    /// <summary>The names of the optional content layers, with <c>--detail layers</c>.</summary>
    public IReadOnlyList<string>? Layers { get; init; }

    /// <summary>
    /// Document information and XMP properties as the file stores them, with
    /// <c>--detail properties</c>; a custom document information entry keeps its own key
    /// (<c>info:</c> before it when it spells a reported name), and XMP keys start with <c>xmp:</c>. <c>creationDate</c> and
    /// <c>modificationDate</c> are ISO 8601 with a fraction of a second only when it is not zero,
    /// as XMP writes dates (<c>2026-10-01T06:01:53Z</c>): in UTC ending in <c>Z</c> when the stored
    /// date states an offset, without <c>Z</c> when it states none, and the stored text unchanged
    /// when it is not a PDF date.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Properties { get; init; }
}

/// <summary>The PDF summary every inspection reports.</summary>
public sealed record PdfSummary
{
    /// <summary>How many pages the document has.</summary>
    [Minimum(0)]
    public required int PageCount { get; init; }

    /// <summary>Each distinct page size and how many pages have it.</summary>
    public required IReadOnlyList<PdfPageSizeSummary> DistinctPageSizes { get; init; }

    /// <summary>The PDF version the file declares, such as <c>1.7</c>.</summary>
    public required string Version { get; init; }

    /// <summary>Whether the file is encrypted.</summary>
    public required bool Encrypted { get; init; }

    /// <summary>Whether the file is linearized for fast web view.</summary>
    public required bool Linearized { get; init; }

    /// <summary>Whether the file is tagged, with a logical structure for accessibility.</summary>
    public required bool Tagged { get; init; }

    /// <summary>
    /// The PDF/A profile the file declares in its metadata, such as <c>pdfa-2b</c>; absent when it
    /// declares none. A declaration is not a check: <c>pdf validate --profile</c> verifies conformance.
    /// </summary>
    [Pattern("^pdfa-[1-4][abuef]?$")]
    public string? PdfaProfile { get; init; }

    /// <summary>The kind of interactive form the file has, an AcroForm or an XFA form; absent when it has none.</summary>
    [AllowedValues(typeof(PdfFormKinds))]
    public string? FormType { get; init; }

    /// <summary>How many files are embedded in the document.</summary>
    [Minimum(0)]
    public required int AttachmentCount { get; init; }

    /// <summary>Whether the file has a signed signature field.</summary>
    public required bool Signed { get; init; }

    /// <summary>
    /// The password this run opened the file with: <c>user</c> for the open password, <c>owner</c>
    /// for the owner password, also when it is the open password too; absent when it needed none
    /// (an unencrypted file, or one with only an owner password). <c>permissions.hasOpenPassword</c>
    /// and <c>hasOwnerPassword</c> state which passwords the file has.
    /// </summary>
    [AllowedValues("user", "owner")]
    public string? PasswordType { get; init; }
}

/// <summary>One distinct page size and how many pages of the document have it.</summary>
public sealed record PdfPageSizeSummary
{
    /// <summary>The size of the visible page.</summary>
    public required PageSizeInfo PageSize { get; init; }

    /// <summary>How many pages have this size.</summary>
    [Minimum(1)]
    public required int PageCount { get; init; }
}

/// <summary>One page's stable geometry in PDF points.</summary>
public sealed record PdfPageInfo
{
    /// <summary>The 1-based page number that operations address.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The size of the visible page, as <c>insert_page</c> and <c>set_page_size</c> set it.</summary>
    public required PageSizeInfo PageSize { get; init; }

    /// <summary>Clockwise page rotation in degrees, a quarter turn at a time: 0, 90, 180 or 270.</summary>
    [Minimum(0), Maximum(270), MultipleOf(90)]
    public required double Rotation { get; init; }

    /// <summary>
    /// The page's media box, the full physical page as it is seen, with the page's rotation: the
    /// frame the crop box is measured in, so it is always at 0,0.
    /// </summary>
    public required PdfRect MediaBox { get; init; }

    /// <summary>
    /// The page's crop box, the region a viewer shows, in points from the top-left corner of the
    /// media box as it is seen, with the page's rotation, as <c>crop_pages</c> measures.
    /// </summary>
    public required PdfRect CropBox { get; init; }
}

/// <summary>One page-label range, addressed by its 1-based starting page.</summary>
public sealed record PdfPageLabelInfo
{
    /// <summary>The 1-based page the range starts at.</summary>
    [Minimum(1)]
    public required int StartPage { get; init; }

    /// <summary>How the label numbers are written, one of the values <c>set_page_labels</c> accepts.</summary>
    [AllowedValues(typeof(PageNumberStyles))]
    public required string NumberStyle { get; init; }

    /// <summary>The text put before each label's number; absent when there is none.</summary>
    public string? Prefix { get; init; }

    /// <summary>The number the range's first page shows.</summary>
    [Minimum(0)]
    public required int StartingValue { get; init; }
}

/// <summary>One bookmark of the document's outline.</summary>
public sealed record PdfOutlineItem
{
    /// <summary>The bookmark's title.</summary>
    public required string Title { get; init; }

    /// <summary>The bookmark's depth: 1 for a top-level bookmark.</summary>
    [Minimum(1)]
    public required int Level { get; init; }

    /// <summary>
    /// The bookmark's address: its 1-based positions from the top level down, joined by '/', so
    /// <c>2/1</c> is the first child of the second top-level bookmark. <c>delete_bookmarks.bookmarks</c>
    /// and <c>add_bookmark.parent</c> take it.
    /// </summary>
    [Pattern("^[1-9][0-9]*(/[1-9][0-9]*)*$")]
    public required string Bookmark { get; init; }

    /// <summary>The page the bookmark opens; omitted when its destination is not a page of the document.</summary>
    [Minimum(1)]
    public int? Page { get; init; }
}

/// <summary>The document's interactive form and its fields.</summary>
public sealed record PdfFormInfo
{
    /// <summary>The kind of form, an AcroForm or an XFA form; absent when the file has none.</summary>
    [AllowedValues(typeof(PdfFormKinds))]
    public string? Kind { get; init; }

    /// <summary>
    /// The number of field names, so a radio group is one field however many buttons it has;
    /// review counts fields the same way.
    /// </summary>
    [Minimum(0)]
    public required int FieldCount { get; init; }

    /// <summary>Whether the form's fields cannot be filled.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>
    /// The first fields in order of their names, one per field name; a <c>LIST_TRUNCATED</c>
    /// warning names <see cref="FieldCount"/> when there are more.
    /// </summary>
    [MaxItems(PdfInspectResult.FieldLimit)]
    public required IReadOnlyList<PdfFormField> Fields { get; init; }
}

/// <summary>One form field; a radio group is one field, however many buttons it has.</summary>
public sealed record PdfFormField
{
    /// <summary>The field's full name, which <c>set_form_field</c> accepts.</summary>
    public required string Name { get; init; }

    /// <summary>The field kind, such as <c>text</c>, <c>checkbox</c> or <c>radio</c>.</summary>
    [AllowedValues(typeof(PdfFormFieldKinds))]
    public required string Kind { get; init; }

    /// <summary>The field's value; for a radio group, the value of its selected button.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// The values <c>set_form_field</c> accepts for the field: a choice field's or a radio
    /// group's values, or a check box's appearance states, <c>Off</c> first; absent for a field
    /// that takes any text.
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>Whether the field cannot be filled.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Whether the form requires a value for the field.</summary>
    public required bool Required { get; init; }

    /// <summary>
    /// The page of the field's widget; absent for a radio group, whose buttons each have their
    /// own, for a field without a widget, and in evaluation mode for a widget past the pages the
    /// engine shows, which EVALUATION_INPUT_TRUNCATED then discloses.
    /// </summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>
    /// The field's widget on that page, in points from its top-left corner. Present only with
    /// <see cref="Page"/>.
    /// </summary>
    public PdfRect? Rect { get; init; }
}

/// <summary>One link to a web address.</summary>
public sealed record PdfLinkInfo
{
    /// <summary>The 1-based page the link is on.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The link's area on its page, in points from the top-left corner of the visible page.</summary>
    public required PdfRect Rect { get; init; }

    /// <summary>The web address the link opens.</summary>
    public required string Url { get; init; }
}

/// <summary>One embedded file.</summary>
public sealed record PdfAttachmentInfo
{
    /// <summary>The attachment's file name, which <c>delete_attachment</c> accepts.</summary>
    public required string Name { get; init; }

    /// <summary>The attachment's media type; absent when the file does not state one.</summary>
    public string? MimeType { get; init; }

    /// <summary>The attachment's size in bytes; absent when the file does not state it.</summary>
    [Minimum(0)]
    public long? SizeBytes { get; init; }

    /// <summary>The attachment's description; absent when the file states none.</summary>
    public string? Description { get; init; }
}

/// <summary>One distinct PDF font resource.</summary>
public sealed record PdfFontInfo
{
    /// <summary>The font's name as the file stores it.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the font program is embedded in the file.</summary>
    public required bool Embedded { get; init; }

    /// <summary>Whether only the glyphs the document uses are embedded.</summary>
    public required bool Subset { get; init; }
}

/// <summary>The passwords and permissions of an opened PDF.</summary>
public sealed record PdfPermissionInfo
{
    /// <summary>Whether the file needs a password to open.</summary>
    public required bool HasOpenPassword { get; init; }

    /// <summary>
    /// Whether the file has an owner password, which lifts its permissions; it may also be the
    /// open password, as <c>--encrypt</c> writes it.
    /// </summary>
    public required bool HasOwnerPassword { get; init; }

    /// <summary>
    /// Whether this run may change the file regardless of its permissions: it is not encrypted or
    /// was opened with the owner password. The permissions below still state what the file grants readers.
    /// </summary>
    public required bool OwnerAccess { get; init; }

    /// <summary>
    /// Whether the file lets readers print it. Like every permission below, this is what the file
    /// grants anyone without the owner password, whatever password this run used; true for a file
    /// that is not encrypted.
    /// </summary>
    public required bool Print { get; init; }

    /// <summary>Whether the file lets readers copy or extract its text and images.</summary>
    public required bool Copy { get; init; }

    /// <summary>Whether the file lets readers change its content.</summary>
    public required bool Modify { get; init; }

    /// <summary>Whether the file lets readers add or change annotations and fill form fields.</summary>
    public required bool Annotate { get; init; }

    /// <summary>Whether the file lets readers fill its form fields, even when annotate is false.</summary>
    public required bool FillForms { get; init; }

    /// <summary>Whether the file lets accessibility tools extract its text and images.</summary>
    public required bool ExtractAccessibility { get; init; }

    /// <summary>Whether the file lets readers insert, delete, rotate and move pages and create bookmarks.</summary>
    public required bool Assemble { get; init; }

    /// <summary>Whether the file lets readers print it at full quality; when false and print is true, printing may be degraded.</summary>
    public required bool PrintHighResolution { get; init; }
}
