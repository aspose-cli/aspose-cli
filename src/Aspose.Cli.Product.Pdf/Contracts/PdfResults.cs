using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Structural information returned by <c>pdf inspect</c>.</summary>
public sealed record PdfInfoResult() : ResultEnvelope(PdfSchemaIds.PdfInfo, 2)
{
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "pdf";
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }
    public required PdfSummary Pdf { get; init; }
    public IReadOnlyList<PdfPageInfo>? Pages { get; init; }
    public IReadOnlyList<PdfPageLabelInfo>? PageLabels { get; init; }
    public IReadOnlyList<PdfOutlineItem>? Outline { get; init; }
    public PdfFormSummary? Forms { get; init; }
    public IReadOnlyList<PdfAttachmentInfo>? Attachments { get; init; }
    public IReadOnlyList<PdfFontInfo>? Fonts { get; init; }
    public PdfPermissionInfo? Permissions { get; init; }
    public IReadOnlyList<PdfSignatureInfo>? Signatures { get; init; }
    public IReadOnlyList<string>? Layers { get; init; }
    public IReadOnlyDictionary<string, string?>? Metadata { get; init; }
}

/// <summary>Always-on PDF summary.</summary>
public sealed record PdfSummary
{
    public required int PageCount { get; init; }
    public required IReadOnlyList<PdfPageSizeSummary> DistinctPageSizes { get; init; }
    public required string Version { get; init; }
    public required bool Encrypted { get; init; }
    public required bool Linearized { get; init; }
    public required bool Tagged { get; init; }
    /// <summary>
    /// The PDF/A profile the file declares, such as <c>pdfa-2b</c>, or null when it declares none.
    /// A declaration is not a check: <c>pdf validate --profile</c> verifies conformance.
    /// </summary>
    public string? PdfaProfile { get; init; }
    public required string FormType { get; init; }
    public required int AttachmentCount { get; init; }
    public required bool Signed { get; init; }
    public required string PasswordType { get; init; }
}

/// <summary>One distinct page size and how many pages of the document have it.</summary>
public sealed record PdfPageSizeSummary
{
    public required double WidthPoints { get; init; }
    public required double HeightPoints { get; init; }
    public required int PageCount { get; init; }
}

/// <summary>One page's stable geometry in PDF points.</summary>
public sealed record PdfPageInfo
{
    /// <summary>The 1-based page number that operations address.</summary>
    public required int Page { get; init; }
    public required double WidthPoints { get; init; }
    public required double HeightPoints { get; init; }

    /// <summary>Clockwise page rotation in degrees: 0, 90, 180 or 270.</summary>
    public required int Rotation { get; init; }
    public required PdfBox MediaBox { get; init; }
    public required PdfBox CropBox { get; init; }
}

/// <summary>One page-label range, addressed by its 1-based starting page.</summary>
public sealed record PdfPageLabelInfo
{
    public required int StartPage { get; init; }

    /// <summary>The numbering style, one of the values <c>set_page_labels</c> accepts.</summary>
    public required string Style { get; init; }
    public string? Prefix { get; init; }
    public required int StartingValue { get; init; }
}

/// <summary>A PDF rectangle using left, bottom, right and top coordinates.</summary>
public sealed record PdfBox
{
    public required double Left { get; init; }
    public required double Bottom { get; init; }
    public required double Right { get; init; }
    public required double Top { get; init; }
}

/// <summary>One bounded bookmark-tree item.</summary>
public sealed record PdfOutlineItem
{
    public required string Title { get; init; }
    public required int Level { get; init; }

    /// <summary>
    /// The bookmark's 1-based positions from the top level down, joined by '/': "2/1" is the
    /// first child of the second top-level bookmark. <c>delete_bookmarks.indexes</c> and
    /// <c>add_bookmark.parent</c> accept it.
    /// </summary>
    public required string Index { get; init; }

    /// <summary>The page the bookmark opens, or null when its destination is not a page of the document.</summary>
    public int? Page { get; init; }
}

/// <summary>AcroForm or XFA summary.</summary>
public sealed record PdfFormSummary
{
    public required string Type { get; init; }
    public required int FieldCount { get; init; }
    public required bool ReadOnly { get; init; }
}

/// <summary>One embedded file.</summary>
public sealed record PdfAttachmentInfo
{
    public required string Name { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
}

/// <summary>One distinct PDF font resource.</summary>
public sealed record PdfFontInfo
{
    public required string Name { get; init; }
    public required bool Embedded { get; init; }
    public required bool Subset { get; init; }
}

/// <summary>Password and permission state of an opened PDF.</summary>
public sealed record PdfPermissionInfo
{
    public required bool HasOpenPassword { get; init; }
    public required bool HasOwnerPassword { get; init; }
    public required bool OwnerAccess { get; init; }
    public required bool Print { get; init; }
    public required bool Copy { get; init; }
    public required bool Modify { get; init; }
    public required bool Annotate { get; init; }
    public required bool FillForms { get; init; }
    public required bool ExtractAccessibility { get; init; }
    public required bool Assemble { get; init; }
    public required bool PrintHighResolution { get; init; }
}

/// <summary>One signature field and the verification state exposed by the SDK.</summary>
public sealed record PdfSignatureInfo
{
    public required string Name { get; init; }
    public required bool Signed { get; init; }
    public bool? Valid { get; init; }
}

/// <summary>Windowed page-text projection returned by <c>pdf query pages</c>.</summary>
public sealed record PdfReadResult() : ResultEnvelope(PdfSchemaIds.PdfRead, 2)
{
    [JsonPropertyOrder(-50)]
    public string Kind { get; } = "pdf";
    [JsonPropertyOrder(-49)]
    public required SourceInfo Source { get; init; }
    public required string Mode { get; init; }

    /// <summary>How many pages the document has, whatever <c>--pages</c> selected.</summary>
    public required int PageCount { get; init; }
    public required IReadOnlyList<PdfPageText> Pages { get; init; }
    public IReadOnlyList<int>? ScannedPagesSuspected { get; init; }
}

/// <summary>Text projection of one PDF page.</summary>
public sealed record PdfPageText
{
    /// <summary>The 1-based page number that operations address.</summary>
    public required int Page { get; init; }
    public required string Text { get; init; }
    public required bool Truncated { get; init; }
}

/// <summary>Result of <c>pdf convert</c>.</summary>
public sealed record PdfConvertResult() : ResultEnvelope(PdfSchemaIds.ConvertResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    [JsonPropertyOrder(-49)]
    public required IReadOnlyList<OutputInfo> Outputs { get; init; }
    public string? Pages { get; init; }
}

/// <summary>Result of rendering PDF pages.</summary>
public sealed record PdfRenderResult() : ResultEnvelope(PdfSchemaIds.RenderResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    public required IReadOnlyList<PdfPageOutput> Outputs { get; init; }
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
    /// <summary>Distance between grid lines.</summary>
    public required int Spacing { get; init; }

    /// <summary>Distance between the heavier, labelled lines.</summary>
    public required int LabelSpacing { get; init; }

    public string Unit { get; init; } = "pt";

    public string Origin { get; init; } = "top-left";
}

/// <summary>One rendered PDF page.</summary>
public sealed record PdfPageOutput
{
    public required int Page { get; init; }
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of creating or merging a PDF.</summary>
public sealed record PdfWriteResult() : ResultEnvelope(PdfSchemaIds.WriteResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required string Action { get; init; }
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
    public IReadOnlyList<SourceInfo>? Inputs { get; init; }
}

/// <summary>Result of transactional PDF splitting.</summary>
public sealed record PdfSplitResult() : ResultEnvelope(PdfSchemaIds.SplitResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    public required IReadOnlyList<PdfSplitOutput> Outputs { get; init; }
}

/// <summary>One split PDF artifact.</summary>
public sealed record PdfSplitOutput
{
    public required int Index { get; init; }
    public required string Pages { get; init; }
    public string? Bookmark { get; init; }
    public required OutputInfo Output { get; init; }
}

/// <summary>Result of bounded PDF extraction.</summary>
public sealed record PdfExtractResult() : ResultEnvelope(PdfSchemaIds.ExtractResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    public required string What { get; init; }
    public required IReadOnlyList<PdfExtractedItem> Items { get; init; }
}

/// <summary>One extracted PDF asset or text artifact.</summary>
public sealed record PdfExtractedItem
{
    public required string Path { get; init; }
    public required string Kind { get; init; }
    public required long SizeBytes { get; init; }
    public int? Page { get; init; }
    public string? Name { get; init; }
    public PdfRect? Rect { get; init; }
    public double? Confidence { get; init; }
}

/// <summary>A rectangle in points using a top-left origin.</summary>
public sealed record PdfRect
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}

/// <summary>Result of one PDF edit batch.</summary>
public sealed record PdfEditResult() : ResultEnvelope(PdfSchemaIds.EditResult, 2), IPartialOutcome
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    [JsonPropertyOrder(-49)]
    public OutputInfo? Output { get; init; }
    public required bool DryRun { get; init; }
    public required IReadOnlyList<BoundedOperationOutcome> Applied { get; init; }
    public BackupInfo? Backup { get; init; }
    public IReadOnlyList<int>? PagesTouched { get; init; }
    public PdfEditVerification? Verification { get; init; }
    [JsonIgnore]
    public bool HasFailures => Applied.Any(static item => item.Status == OpStatuses.Failed)
        || Verification is { Ok: false };
}

/// <summary>What --verify read back from the staged output.</summary>
public sealed record PdfEditVerification
{
    public required bool Ok { get; init; }
    public required IReadOnlyList<VerificationIssue> Issues { get; init; }

    /// <summary>In batch order, the ids of the operations whose every recorded effect was read back.</summary>
    public required IReadOnlyList<string> CheckedOps { get; init; }
}

/// <summary>PDF form field inventory.</summary>
public sealed record PdfFormResult() : ResultEnvelope(PdfSchemaIds.FormResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    public required string Type { get; init; }
    public required bool ReadOnly { get; init; }
    public required IReadOnlyList<PdfFormField> Fields { get; init; }
}

public sealed record PdfFormField
{
    public required string Name { get; init; }

    /// <summary>The field kind; one of <see cref="PdfFormFieldTypes"/>.</summary>
    public required string Type { get; init; }
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

    public required bool ReadOnly { get; init; }
    public required bool Required { get; init; }

    /// <summary>
    /// The page of the field's widget; absent when it has none or, in evaluation mode, lies
    /// past the pages the engine shows, which EVAL_INPUT_TRUNCATED then names.
    /// </summary>
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
    public const string Text = "text";
    public const string Checkbox = "checkbox";
    public const string Radio = "radio";
    public const string RadioOption = "radio-option";
    public const string ComboBox = "combobox";
    public const string ListBox = "listbox";
    public const string Button = "button";
    public const string Signature = "signature";

    /// <summary>A field kind the vocabulary does not name.</summary>
    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } =
        [Text, Checkbox, Radio, RadioOption, ComboBox, ListBox, Button, Signature, Other];
}

/// <summary>Result of exporting PDF form data.</summary>
public sealed record PdfFormExportResult() : ResultEnvelope(PdfSchemaIds.FormExportResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
}

/// <summary>Bounded PDF text-search result.</summary>
public sealed record PdfSearchResult() : ResultEnvelope(PdfSchemaIds.SearchResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Source { get; init; }
    public required string Pattern { get; init; }
    public required IReadOnlyList<PdfSearchHit> Hits { get; init; }
}

public sealed record PdfSearchHit
{
    public required int Page { get; init; }

    /// <summary>The matched text.</summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// The page text around the match, up to 40 characters on each side, with line breaks as
    /// spaces and an ellipsis where it is cut; absent when the page's plain text does not show
    /// the hits the engine found.
    /// </summary>
    public string? Context { get; init; }

    public required PdfRect Rect { get; init; }
    public required int Occurrence { get; init; }
}

/// <summary>PDF/A validation result.</summary>
public sealed record PdfValidateResult() : ResultEnvelope(PdfSchemaIds.ValidateResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    public required string Profile { get; init; }
    public required bool Valid { get; init; }
    public required IReadOnlyList<string> Issues { get; init; }
}

/// <summary>Result of applying and reopening one PDF signature.</summary>
public sealed record PdfSignResult() : ResultEnvelope(PdfSchemaIds.SignResult, 2)
{
    [JsonPropertyOrder(-50)]
    public required SourceInfo Input { get; init; }
    [JsonPropertyOrder(-49)]
    public required OutputInfo Output { get; init; }
    public required PdfSignatureInfo Signature { get; init; }
    public required bool Visible { get; init; }
    public required int Page { get; init; }
    public PdfRect? Rect { get; init; }
}
