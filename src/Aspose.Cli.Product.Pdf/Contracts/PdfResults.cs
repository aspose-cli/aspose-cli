using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Structural information returned by <c>pdf inspect</c>.</summary>
public sealed record PdfInfoResult() : EngineResultEnvelope("pdf-info", 2)
{
    /// <summary>The kind of document described: always <c>pdf</c>.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "pdf";

    /// <summary>The inspected PDF.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Source { get; init; }

    /// <summary>The document summary every inspection reports.</summary>
    public required PdfSummary Pdf { get; init; }

    /// <summary>
    /// The first pages' geometry with <c>--preview</c>; a <c>LIST_TRUNCATED</c> warning names the
    /// page count when there are more.
    /// </summary>
    [MaxItems(20)]
    public IReadOnlyList<PdfPageInfo>? Pages { get; init; }

    /// <summary>The page-label ranges, when the file has any.</summary>
    public IReadOnlyList<PdfPageLabelInfo>? PageLabels { get; init; }

    /// <summary>
    /// Bookmarks in reading order, depth first, with <c>--detail outline</c>; a
    /// <c>LIST_TRUNCATED</c> warning names the total when there are more.
    /// </summary>
    [MaxItems(200)]
    public IReadOnlyList<PdfOutlineItem>? Outline { get; init; }

    /// <summary>The interactive form, with <c>--detail forms</c>.</summary>
    public PdfFormSummary? Forms { get; init; }

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
    /// <c>--detail metadata</c>; XMP keys start with <c>xmp:</c>. <c>creationDate</c> and
    /// <c>modificationDate</c> are ISO 8601 with a fraction of a second only when it is not zero,
    /// as XMP writes dates (<c>2026-10-01T06:01:53Z</c>): in UTC ending in <c>Z</c> when the stored
    /// date states an offset, without <c>Z</c> when it states none, and the stored text unchanged
    /// when it is not a PDF date.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Metadata { get; init; }
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

    /// <summary>The kind of interactive form the file has: none, an AcroForm or an XFA form.</summary>
    [AllowedValues(typeof(PdfFormKinds))]
    public required string FormType { get; init; }

    /// <summary>How many files are embedded in the document.</summary>
    [Minimum(0)]
    public required int AttachmentCount { get; init; }

    /// <summary>Whether the file has a signed signature field.</summary>
    public required bool Signed { get; init; }

    /// <summary>
    /// The password this run opened the file with: <c>none</c> when it needed none (an unencrypted
    /// file, or one with only an owner password), <c>user</c> for the open password, <c>owner</c>
    /// for the owner password. <c>permissions.hasOpenPassword</c> and <c>hasOwnerPassword</c> state
    /// which passwords the file has.
    /// </summary>
    [AllowedValues("none", "user", "owner")]
    public required string PasswordType { get; init; }
}

/// <summary>One distinct page size and how many pages of the document have it.</summary>
public sealed record PdfPageSizeSummary
{
    /// <summary>The page width in points.</summary>
    [ExclusiveMinimum(0)]
    public required double WidthPoints { get; init; }

    /// <summary>The page height in points.</summary>
    [ExclusiveMinimum(0)]
    public required double HeightPoints { get; init; }

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

    /// <summary>The width of the visible page in points.</summary>
    [ExclusiveMinimum(0)]
    public required double WidthPoints { get; init; }

    /// <summary>The height of the visible page in points.</summary>
    [ExclusiveMinimum(0)]
    public required double HeightPoints { get; init; }

    /// <summary>Clockwise page rotation in degrees.</summary>
    [AllowedValues(0, 90, 180, 270)]
    public required int Rotation { get; init; }

    /// <summary>The page's media box, the full physical page.</summary>
    public required PdfBox MediaBox { get; init; }

    /// <summary>The page's crop box, the region a viewer shows.</summary>
    public required PdfBox CropBox { get; init; }
}

/// <summary>One page-label range, addressed by its 1-based starting page.</summary>
public sealed record PdfPageLabelInfo
{
    /// <summary>The 1-based page the range starts at.</summary>
    [Minimum(1)]
    public required int StartPage { get; init; }

    /// <summary>The numbering style, one of the values <c>set_page_labels</c> accepts.</summary>
    [AllowedValues("arabic", "roman-upper", "roman-lower", "letters-upper", "letters-lower", "none")]
    public required string Style { get; init; }

    /// <summary>The text put before each label's number; absent when there is none.</summary>
    public string? Prefix { get; init; }

    /// <summary>The number the range's first page shows.</summary>
    [Minimum(0)]
    public required int StartingValue { get; init; }
}

/// <summary>A PDF rectangle in points, using left, bottom, right and top coordinates.</summary>
public sealed record PdfBox
{
    /// <summary>The left edge.</summary>
    public required double Left { get; init; }

    /// <summary>The bottom edge.</summary>
    public required double Bottom { get; init; }

    /// <summary>The right edge.</summary>
    public required double Right { get; init; }

    /// <summary>The top edge.</summary>
    public required double Top { get; init; }
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
    /// The bookmark's 1-based positions from the top level down, joined by '/': <c>2/1</c> is the
    /// first child of the second top-level bookmark. <c>delete_bookmarks.indexes</c> and
    /// <c>add_bookmark.parent</c> accept it.
    /// </summary>
    [Pattern("^[1-9][0-9]*(/[1-9][0-9]*)*$")]
    public required string Index { get; init; }

    /// <summary>The page the bookmark opens; omitted when its destination is not a page of the document.</summary>
    [Minimum(1)]
    public int? Page { get; init; }
}

/// <summary>The document's interactive form.</summary>
public sealed record PdfFormSummary
{
    /// <summary>The kind of form: none, an AcroForm or an XFA form.</summary>
    [AllowedValues(typeof(PdfFormKinds))]
    public required string Type { get; init; }

    /// <summary>
    /// The number of field names, so a radio group is one field however many buttons
    /// <c>pdf query forms</c> lists for it; review counts fields the same way.
    /// </summary>
    [Minimum(0)]
    public required int FieldCount { get; init; }

    /// <summary>Whether the form's fields cannot be filled.</summary>
    public required bool ReadOnly { get; init; }
}

/// <summary>The kinds of interactive form a PDF can have.</summary>
public static class PdfFormKinds
{
    /// <summary>No form.</summary>
    public const string None = "none";

    /// <summary>An AcroForm.</summary>
    public const string Acro = "acro";

    /// <summary>An XFA form.</summary>
    public const string Xfa = "xfa";
}

/// <summary>One embedded file.</summary>
public sealed record PdfAttachmentInfo
{
    /// <summary>The attachment's file name, which <c>remove_attachment</c> accepts.</summary>
    public required string Name { get; init; }

    /// <summary>The attachment's media type; absent when the file does not state one.</summary>
    public string? MimeType { get; init; }

    /// <summary>The attachment's size in bytes; absent when the file does not state it.</summary>
    [Minimum(0)]
    public long? SizeBytes { get; init; }
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

    /// <summary>Whether the file has an owner password, which lifts its permissions.</summary>
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

/// <summary>One signature field and the verification state the engine reports.</summary>
public sealed record PdfSignatureInfo
{
    /// <summary>The signature field's name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the field holds a signature.</summary>
    public required bool Signed { get; init; }

    /// <summary>Whether the signature verifies against the signed bytes; absent when it was not verified.</summary>
    public bool? Valid { get; init; }
}

/// <summary>Windowed page text returned by <c>pdf query pages</c>.</summary>
public sealed record PdfReadResult() : WindowedResultEnvelope("pdf-read", 2)
{
    /// <summary>The kind of document read: always <c>pdf</c>.</summary>
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "pdf";

    /// <summary>The PDF read.</summary>
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }

    /// <summary>How the text was extracted: <c>plain</c> reading order, or <c>layout</c>, which keeps the page's spacing.</summary>
    [AllowedValues(typeof(PdfReadModes))]
    public required string Mode { get; init; }

    /// <summary>How many pages the document has, whatever <c>--pages</c> selected.</summary>
    [Minimum(0)]
    public required int PageCount { get; init; }

    /// <summary>The text of each page in the window, in page order.</summary>
    public required IReadOnlyList<PdfPageText> Pages { get; init; }

    /// <summary>
    /// The pages of the window that have no extractable text and appear image-dominated, so they
    /// are probably scanned; absent when there are none.
    /// </summary>
    [UniqueItems]
    [Minimum(1)]
    public IReadOnlyList<int>? ScannedPagesSuspected { get; init; }
}

/// <summary>The text of one PDF page.</summary>
public sealed record PdfPageText
{
    /// <summary>The 1-based page number that operations address.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The page's text.</summary>
    public required string Text { get; init; }

    /// <summary>Whether the text was cut to stay within <c>--max-chars</c>.</summary>
    public required bool Truncated { get; init; }
}

/// <summary>Result of <c>pdf convert</c>.</summary>
public sealed record PdfConvertResult() : EngineResultEnvelope("convert-result", 2)
{
    /// <summary>The converted PDF.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The files written: one, or one per page for an image format.</summary>
    [JsonPropertyOrder(-49)]
    [MinItems(1)]
    [AlwaysPresent("format")]
    public required IReadOnlyList<OutputInfo> Outputs { get; init; }

    /// <summary>The page range converted, as <c>--pages</c> gave it; absent when every page was.</summary>
    public string? Pages { get; init; }
}

/// <summary>Result of <c>pdf render</c>.</summary>
public sealed record PdfRenderResult() : EngineResultEnvelope("render-result", 2)
{
    /// <summary>The rendered PDF.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>Each written image, in page order.</summary>
    public required IReadOnlyList<PdfPageOutput> Outputs { get; init; }

    /// <summary>The resolution raster images were rendered at; absent for SVG.</summary>
    [Minimum(36)]
    [Maximum(1200)]
    public int? Dpi { get; init; }

    /// <summary>The coordinate grid drawn on every output image, present only when one was requested.</summary>
    public PdfRenderGrid? Grid { get; init; }
}

/// <summary>
/// A coordinate grid drawn on rendered images, in the coordinates <c>redact_area</c> takes: points from the
/// top-left corner of the visible, rotated page box.
/// </summary>
public sealed record PdfRenderGrid
{
    /// <summary>Distance between grid lines, in points.</summary>
    [Minimum(10)]
    [Maximum(500)]
    public required int Spacing { get; init; }

    /// <summary>Distance between the heavier, labelled lines, in points.</summary>
    [Minimum(10)]
    public required int LabelSpacing { get; init; }

    /// <summary>The unit of the distances and labels: always <c>pt</c>.</summary>
    public string Unit { get; } = "pt";

    /// <summary>Where the coordinates start: always <c>top-left</c>.</summary>
    public string Origin { get; } = "top-left";
}

/// <summary>One rendered PDF page.</summary>
public sealed record PdfPageOutput
{
    /// <summary>The 1-based page rendered.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The image written for the page.</summary>
    [AlwaysPresent("format")]
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of <c>pdf create</c> or <c>pdf merge</c>.</summary>
public sealed record PdfWriteResult() : EngineResultEnvelope("write-result", 2)
{
    /// <summary>The command that wrote the PDF.</summary>
    [JsonPropertyOrder(-50)]
    [AllowedValues("create", "merge")]
    public required string Action { get; init; }

    /// <summary>The PDF written.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("format")]
    public required OutputInfo Output { get; init; }

    /// <summary>The documents merged, in order; absent for <c>create</c>.</summary>
    public IReadOnlyList<SourceInfo>? Inputs { get; init; }
}

/// <summary>Result of <c>pdf split</c>, which writes every part or none.</summary>
public sealed record PdfSplitResult() : EngineResultEnvelope("split-result", 2)
{
    /// <summary>The PDF split.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>Each part written, in order.</summary>
    public required IReadOnlyList<PdfSplitOutput> Outputs { get; init; }
}

/// <summary>One part of a split PDF.</summary>
public sealed record PdfSplitOutput
{
    /// <summary>The part's 1-based position.</summary>
    [Minimum(1)]
    public required int Index { get; init; }

    /// <summary>The part's pages as range text, such as <c>1-3</c> or <c>1-3,7</c>.</summary>
    public required string Pages { get; init; }

    /// <summary>The title of the bookmark the part starts at, with <c>--by-bookmarks</c>.</summary>
    public string? Bookmark { get; init; }

    /// <summary>The PDF written for the part.</summary>
    [AlwaysPresent("format")]
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of <c>pdf extract</c> for images, attachments, text or tables.</summary>
public sealed record PdfExtractResult() : EngineResultEnvelope("extract-result", 2)
{
    /// <summary>The PDF extracted from.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>What was extracted, as <c>--what</c> named it.</summary>
    [AllowedValues("images", "attachments", "text", "tables")]
    public required string What { get; init; }

    /// <summary>Each file written into the output directory.</summary>
    public required IReadOnlyList<PdfExtractedItem> Items { get; init; }
}

/// <summary>One extracted PDF asset or text file.</summary>
public sealed record PdfExtractedItem
{
    /// <summary>Absolute path of the file written.</summary>
    public required string Path { get; init; }

    /// <summary>What the file holds.</summary>
    [AllowedValues("image", "attachment", "text", "table")]
    public required string Kind { get; init; }

    /// <summary>The file's size in bytes.</summary>
    [Minimum(0)]
    public required long SizeBytes { get; init; }

    /// <summary>The 1-based page the item comes from; absent for an attachment.</summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>The attachment's name in the PDF; absent for other items.</summary>
    public string? Name { get; init; }

    /// <summary>Where a table lies on its page; absent for other items.</summary>
    public PdfRect? Rect { get; init; }

    /// <summary>How sure the engine is that a table was found, from 0 to 1; absent for other items.</summary>
    [Minimum(0)]
    [Maximum(1)]
    public double? Confidence { get; init; }
}

/// <summary>A rectangle in points from the top-left corner of the page.</summary>
public sealed record PdfRect
{
    /// <summary>Distance of the left edge from the page's left edge.</summary>
    public required double X { get; init; }

    /// <summary>Distance of the top edge from the page's top edge.</summary>
    public required double Y { get; init; }

    /// <summary>The width.</summary>
    [Minimum(0)]
    public required double Width { get; init; }

    /// <summary>The height.</summary>
    [Minimum(0)]
    public required double Height { get; init; }
}

/// <summary>Result of one <c>pdf edit</c> batch.</summary>
public sealed record PdfEditResult() : EngineResultEnvelope("edit-result", 2), IPartialOutcome
{
    /// <summary>The PDF edited.</summary>
    [JsonPropertyOrder(-50)]
    [AlwaysPresent("fingerprint")]
    public required SourceInfo Input { get; init; }

    /// <summary>The PDF written; absent for a dry run, which writes nothing.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("format", "fingerprint")]
    public OutputInfo? Output { get; init; }

    /// <summary>Whether the batch was only checked and applied in memory (<c>--dry-run</c>).</summary>
    public required bool DryRun { get; init; }

    /// <summary>The outcome of each operation, in batch order.</summary>
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }

    /// <summary>The copy of the input kept before it was replaced in place; absent otherwise.</summary>
    public BackupInfo? Backup { get; init; }

    /// <summary>The 1-based pages the operations changed; absent when they changed none.</summary>
    [Minimum(1)]
    public IReadOnlyList<int>? PagesTouched { get; init; }

    /// <summary>
    /// Present with <c>--verify</c>: the staged output read back against the effect of each form,
    /// redaction, bookmark, metadata, attachment and page operation.
    /// </summary>
    public PdfEditVerification? Verification { get; init; }

    /// <summary>Whether an operation failed or the read-back found a difference.</summary>
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static item => item.Status == OpStatuses.Failed)
        || Verification is { Ok: false };
}

/// <summary>What <c>--verify</c> read back from the staged output.</summary>
public sealed record PdfEditVerification
{
    /// <summary>Whether every effect read back as the operations recorded it.</summary>
    public required bool Ok { get; init; }

    /// <summary>Each difference found.</summary>
    public required IReadOnlyList<VerificationIssue> Issues { get; init; }

    /// <summary>
    /// In batch order, the ids of the operations whose every recorded effect was read back; an
    /// operation left out was not fully checked.
    /// </summary>
    public required IReadOnlyList<string> CheckedOps { get; init; }
}

/// <summary>The form fields <c>pdf query forms</c> lists.</summary>
public sealed record PdfFormResult() : EngineResultEnvelope("form-result", 2)
{
    /// <summary>The PDF read.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The kind of form: none, an AcroForm or an XFA form.</summary>
    [AllowedValues(typeof(PdfFormKinds))]
    public required string Type { get; init; }

    /// <summary>Whether the form's fields cannot be filled.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Each field, and each button of a radio group, in document order.</summary>
    public required IReadOnlyList<PdfFormField> Fields { get; init; }
}

/// <summary>One form field, or one button of a radio group.</summary>
public sealed record PdfFormField
{
    /// <summary>The field's full name, which <c>set_form_field</c> accepts.</summary>
    public required string Name { get; init; }

    /// <summary>The field kind; one of <see cref="PdfFormFieldTypes"/>.</summary>
    [AllowedValues(typeof(PdfFormFieldTypes))]
    public required string Type { get; init; }

    /// <summary>The field's value; for a radio button, the value its group has selected.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// The values a choice field accepts; for a radio button, the values of its group, which
    /// set_form_field accepts under the group's name.
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>A check box's appearance states, <c>Off</c> first; set_form_field accepts only these.</summary>
    public IReadOnlyList<string>? States { get; init; }

    /// <summary>
    /// The value that checks a check box, when it has exactly one state besides <c>Off</c>, or
    /// that selects a radio button.
    /// </summary>
    public string? OnValue { get; init; }

    /// <summary>Whether the field cannot be filled.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Whether the form requires a value for the field.</summary>
    public required bool Required { get; init; }

    /// <summary>
    /// The page of the field's widget; absent when it has none or, in evaluation mode, lies
    /// past the pages the engine shows, which EVAL_INPUT_TRUNCATED then names.
    /// </summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>
    /// The field's widget on that page, in points from its top-left corner; for a radio button,
    /// the button. Present only with <see cref="Page"/>.
    /// </summary>
    public PdfRect? Rect { get; init; }
}

/// <summary>The product vocabulary of form field kinds, independent of the engine's class names.</summary>
public static class PdfFormFieldTypes
{
    /// <summary>A text box.</summary>
    public const string Text = "text";

    /// <summary>A check box.</summary>
    public const string Checkbox = "checkbox";

    /// <summary>A radio group.</summary>
    public const string Radio = "radio";

    /// <summary>One button of a radio group.</summary>
    public const string RadioOption = "radio-option";

    /// <summary>A drop-down list.</summary>
    public const string ComboBox = "combobox";

    /// <summary>A list box.</summary>
    public const string ListBox = "listbox";

    /// <summary>A push button.</summary>
    public const string Button = "button";

    /// <summary>A signature field.</summary>
    public const string Signature = "signature";

    /// <summary>A field kind the vocabulary does not name.</summary>
    public const string Other = "other";
}

/// <summary>Result of <c>pdf extract --what forms</c>, which exports the form data to a file.</summary>
public sealed record PdfFormExportResult() : EngineResultEnvelope("form-export-result", 2)
{
    /// <summary>The PDF whose form data was exported.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The form-data file written: JSON, FDF or XFDF.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("format")]
    public required OutputInfo Output { get; init; }
}

/// <summary>The bounded text search of <c>pdf query search</c>.</summary>
public sealed record PdfSearchResult() : WindowedResultEnvelope("search-result", 2)
{
    /// <summary>The PDF searched.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Source { get; init; }

    /// <summary>The text or regular expression searched for.</summary>
    public required string Pattern { get; init; }

    /// <summary>Each match in the window, in page order.</summary>
    public required IReadOnlyList<PdfSearchHit> Hits { get; init; }
}

/// <summary>One match of a text search.</summary>
public sealed record PdfSearchHit
{
    /// <summary>The 1-based page of the match.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>The matched text.</summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// The page text around the match, up to 40 characters on each side, with line breaks as
    /// spaces and an ellipsis where it is cut; absent when the page's plain text does not show
    /// the hits the engine found, or when the text it pairs with this hit by order differs from
    /// the match (ignoring case).
    /// </summary>
    public string? Context { get; init; }

    /// <summary>Where the match lies on its page.</summary>
    public required PdfRect Rect { get; init; }

    /// <summary>The match's 1-based position among the matches of the document.</summary>
    [Minimum(1)]
    public required int Occurrence { get; init; }
}

/// <summary>Result of <c>pdf validate</c>, a PDF/A conformance check.</summary>
public sealed record PdfValidateResult() : EngineResultEnvelope("validate-result", 2)
{
    /// <summary>The PDF checked.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The PDF/A profile checked against.</summary>
    [AllowedValues("pdfa-1b", "pdfa-2b", "pdfa-3b")]
    public required string Profile { get; init; }

    /// <summary>Whether the file conforms to the profile.</summary>
    public required bool Valid { get; init; }

    /// <summary>The first compliance issues; a <c>LIST_TRUNCATED</c> warning names the total when there are more.</summary>
    [MaxItems(100)]
    public required IReadOnlyList<string> Issues { get; init; }
}

/// <summary>Result of <c>pdf sign</c>: the signature applied and read back from the saved file.</summary>
public sealed record PdfSignResult() : EngineResultEnvelope("sign-result", 2)
{
    /// <summary>The PDF signed.</summary>
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }

    /// <summary>The signed PDF written.</summary>
    [JsonPropertyOrder(-49)]
    [AlwaysPresent("format")]
    public required OutputInfo Output { get; init; }

    /// <summary>The signature field as the saved file reports it.</summary>
    public required PdfSignatureInfo Signature { get; init; }

    /// <summary>Whether the signature has a visible appearance on the page.</summary>
    public required bool Visible { get; init; }

    /// <summary>The 1-based page of the signature field.</summary>
    [Minimum(1)]
    public required int Page { get; init; }

    /// <summary>Where a visible signature lies on its page; absent for an invisible one.</summary>
    public PdfRect? Rect { get; init; }
}
