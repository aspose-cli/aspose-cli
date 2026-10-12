using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>A validated atomic PDF edit batch.</summary>
[ProductJsonRoot]
public sealed record PdfOpsBatch : BoundedOperationEnvelope<PdfOp>;

/// <summary>
/// The operation document of <c>pdf edit --ops</c>. Page numbers and ranges are 1-based and
/// resolve against the document as the earlier operations left it.
/// </summary>
[OperationVocabulary("ops", JsonContext = typeof(PdfOpsJsonContext), Addresses = new[] { "page", "pages", "at", "path", "sourcePasswordEnv" })]
[JsonConverter(typeof(OperationJsonConverter<PdfOp>))]
public abstract partial record PdfOp : BoundedOperation;

/// <summary>A rectangle in points (72 per inch) with a top-left origin, relative to the visible, rotated page box.</summary>
public sealed record PdfRectInput
{
    /// <summary>Distance from the left edge.</summary>
    [Minimum(0)] public required double X { get; init; }

    /// <summary>Distance from the top edge.</summary>
    [Minimum(0)] public required double Y { get; init; }

    [ExclusiveMinimum(0)] public required double Width { get; init; }

    [ExclusiveMinimum(0)] public required double Height { get; init; }
}

/// <summary>What a reader may do without the owner password; everything omitted is denied.</summary>
public sealed record PdfPermissionsInput
{
    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/print")]
    public bool Print { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/copy")]
    public bool Copy { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/modify")]
    public bool Modify { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/annotate")]
    public bool Annotate { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/fillForms")]
    public bool FillForms { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/extractAccessibility")]
    public bool ExtractAccessibility { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/assemble")]
    public bool Assemble { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPermissionInfo/properties/printHighResolution")]
    public bool PrintHighResolution { get; init; }
}

/// <summary>One page-label range, which applies from its start page until the next range.</summary>
public sealed record PdfPageLabelRange
{
    /// <summary>The 1-based page where the range starts.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfPageLabelInfo/properties/startPage")]
    [Minimum(1)] public required int StartPage { get; init; }

    /// <summary>How the label numbers are written; <c>none</c> labels the pages with the prefix alone.</summary>
    [AllowedValues(typeof(PageNumberStyles))]
    [MistakenFor("style")]
    [ReadBack("inspect-result.schema.json#/$defs/pdfPageLabelInfo/properties/numberStyle")]
    public string NumberStyle { get; init; } = PageNumberStyles.Decimal;

    /// <summary>Text placed before each label value.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfPageLabelInfo/properties/prefix")]
    public string? Prefix { get; init; }

    /// <summary>The label value of the start page.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfPageLabelInfo/properties/startingValue")]
    [Minimum(0)] public int StartingValue { get; init; } = 1;
}

/// <summary>Where margin text sits on a page.</summary>
public static class PdfPlacements
{
    public const string TopLeft = "top-left";
    public const string TopCenter = "top-center";
    public const string TopRight = "top-right";
    public const string BottomLeft = "bottom-left";
    public const string BottomCenter = "bottom-center";
    public const string BottomRight = "bottom-right";
}

/// <summary>Whether a watermark is drawn over or under the page content.</summary>
public static class PdfLayers
{
    public const string Over = "over";
    public const string Under = "under";
}

/// <summary>Rotates pages to an absolute angle.</summary>
[Operation("rotate_pages")]
public sealed record RotatePagesOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    /// <summary>The rotation in degrees, clockwise, a quarter turn at a time: 0, 90, 180 or 270; 0 sets a page upright.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfPageInfo/properties/rotation")]
    [MistakenFor("angle")]
    [Minimum(0), Maximum(270), MultipleOf(90)] public required double Rotation { get; init; }
}

/// <summary>Deletes pages; a PDF must keep at least one page.</summary>
[Operation("delete_pages")]
public sealed record DeletePagesOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }
}

/// <summary>Moves pages so the first of them lands at a position of the current document.</summary>
[Operation("move_pages")]
public sealed record MovePagesOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    /// <summary>The 1-based position, counted before the move, at most the page count plus one.</summary>
    [NotState]
    [Minimum(1)] public required int To { get; init; }
}

/// <summary>Inserts pages of another PDF.</summary>
[Operation("import_pages")]
[MistakenFor("insert_pages_from")]
public sealed record ImportPagesOp : PdfOp
{
    /// <summary>The source PDF, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>The source pages; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    /// <summary>The 1-based position of the first inserted page, at most the page count plus one.</summary>
    [Minimum(1)] public required int At { get; init; }

    /// <summary>The environment variable that holds the source PDF's password.</summary>
    [MistakenFor("passwordEnv")]
    [SecretEnv] public string? SourcePasswordEnv { get; init; }
}

/// <summary>Inserts one blank page.</summary>
[Operation("insert_page")]
[MistakenFor("insert_blank_page")]
public sealed record InsertPageOp : PdfOp
{
    /// <summary>The 1-based position of the new page, at most the page count plus one.</summary>
    [Minimum(1)] public required int At { get; init; }

    /// <summary>The size of the new page; an a4 page when omitted.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfPageInfo/properties/pageSize")]
    [MistakenFor("size")]
    public PageSizeInput? PageSize { get; init; }
}

/// <summary>
/// Crops pages to a rectangle measured on each page's visible page box before the crop, from its
/// top-left corner; 'pdf inspect --sample' reports the resulting page size and the crop box,
/// measured from the top-left corner of the media box as it is seen.
/// </summary>
[Operation("crop_pages")]
public sealed record CropPagesOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    /// <summary>The page box to set: the visible crop box or the physical media box.</summary>
    [NotState]
    [AllowedValues("media", "crop")] public string Box { get; init; } = "crop";

    /// <summary>The part of the visible page before the crop to keep, from its top-left corner.</summary>
    [NotState]
    public required PdfRectInput Rect { get; init; }
}

/// <summary>Sets pages to a page size.</summary>
[Operation("set_page_size")]
public sealed record SetPageSizeOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfPageInfo/properties/pageSize")]
    [MistakenFor("size")]
    public required PageSizeInput PageSize { get; init; }

    /// <summary>Whether the content is scaled to the new size rather than kept at its position.</summary>
    [NotState]
    public bool ScaleContent { get; init; }
}

/// <summary>Stamps text across the middle of pages.</summary>
[Operation("add_watermark_text")]
public sealed record AddWatermarkTextOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [NotState]
    [MinLength(1)] public required string Text { get; init; }

    /// <summary>An installed font name; the default font when omitted.</summary>
    [NotState]
    public string? Font { get; init; }

    /// <summary>The font size in points.</summary>
    [NotState]
    [MistakenFor("size")]
    [ExclusiveMinimum(0)] public double FontSize { get; init; } = 48;

    [NotState]
    [HexColor] public string Color { get; init; } = "#808080";

    [NotState]
    [Minimum(0), Maximum(1)] public double Opacity { get; init; } = 0.35;

    /// <summary>The angle in degrees, clockwise; the default -45 rises from the bottom left to the top right.</summary>
    [NotState]
    public double Rotation { get; init; } = -45;

    [NotState]
    [AllowedValues(typeof(PdfLayers))] public string Layer { get; init; } = PdfLayers.Over;
}

/// <summary>Stamps an image across the middle of pages.</summary>
[Operation("add_watermark_image")]
public sealed record AddWatermarkImageOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    /// <summary>The image, relative to the working directory; an SVG must not reference network resources.</summary>
    [InputPath] public required string Path { get; init; }

    [NotState]
    [Minimum(0), Maximum(1)] public double Opacity { get; init; } = 0.35;

    /// <summary>The size of the image as a percentage of its own size.</summary>
    [NotState]
    [MistakenFor("scale")]
    [ExclusiveMinimum(0)] public double ScalePercent { get; init; } = 50;

    [NotState]
    [AllowedValues(typeof(PdfLayers))] public string Layer { get; init; } = PdfLayers.Over;
}

/// <summary>Stamps page numbers in a page margin.</summary>
[Operation("add_page_numbers")]
public sealed record AddPageNumbersOp : PdfOp
{
    /// <summary>The pages to number; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    /// <summary>The text; {n} is the number, counted over the selected pages from start, and {N} the document's page count.</summary>
    [NotState]
    [MistakenFor("format")]
    public string Text { get; init; } = "Page {n} of {N}";

    [NotState]
    [MistakenFor("position")]
    [AllowedValues(typeof(PdfPlacements))] public string Placement { get; init; } = PdfPlacements.BottomCenter;

    /// <summary>The number of the first selected page.</summary>
    [NotState]
    [Minimum(0)] public int Start { get; init; } = 1;

    /// <summary>An installed font name; the default font when omitted.</summary>
    [NotState]
    public string? Font { get; init; }
}

/// <summary>Stamps text in the top margin of pages.</summary>
[Operation("add_header_text")]
public sealed record AddHeaderTextOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [NotState]
    [MinLength(1)] public required string Text { get; init; }

    [NotState]
    [MistakenFor("position")]
    [AllowedValues(typeof(PdfPlacements))] public string Placement { get; init; } = PdfPlacements.TopCenter;

    /// <summary>An installed font name; the default font when omitted.</summary>
    [NotState]
    public string? Font { get; init; }
}

/// <summary>Stamps text in the bottom margin of pages.</summary>
[Operation("add_footer_text")]
public sealed record AddFooterTextOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [NotState]
    [MinLength(1)] public required string Text { get; init; }

    [NotState]
    [MistakenFor("position")]
    [AllowedValues(typeof(PdfPlacements))] public string Placement { get; init; } = PdfPlacements.BottomCenter;

    /// <summary>An installed font name; the default font when omitted.</summary>
    [NotState]
    public string? Font { get; init; }
}

/// <summary>Stamps an image into a rectangle of one page.</summary>
[Operation("add_stamp_image")]
public sealed record AddStampImageOp : PdfOp
{
    [Minimum(1)] public required int Page { get; init; }

    /// <summary>The image, relative to the working directory; an SVG must not reference network resources.</summary>
    [InputPath] public required string Path { get; init; }

    [NotState]
    public required PdfRectInput Rect { get; init; }
}

/// <summary>Adds a clickable link area to one page.</summary>
[Operation("add_hyperlink")]
[MistakenFor("add_link")]
public sealed record AddHyperlinkOp : PdfOp
{
    [Minimum(1)] public required int Page { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfLinkInfo/properties/rect")]
    public required PdfRectInput Rect { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfLinkInfo/properties/url")]
    [WebLink] public required string Url { get; init; }
}

/// <summary>
/// Redacts every match of a text pattern in the extractable text; it does not perform OCR. A
/// regular expression runs with a one-second timeout and must not match an empty string.
/// </summary>
[Operation("redact_text")]
public sealed record RedactTextOp : PdfOp
{
    /// <summary>
    /// The text to find, matched case-sensitively. Literal text also matches with up to two
    /// spaces, never a line break, where an East Asian character meets a character that is not
    /// East Asian, such as a digit or Latin letter, as extracted text can hold there.
    /// </summary>
    [NotState]
    [MistakenFor("pattern")]
    [MinLength(1)] public required string Find { get; init; }

    /// <summary>Whether find is a .NET regular expression rather than literal text.</summary>
    [NotState]
    public bool Regex { get; init; }

    /// <summary>The pages to search; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [NotState]
    [HexColor] public string FillColor { get; init; } = "#000000";

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Regex)
        {
            System.Text.RegularExpressions.Regex expression;
            try
            {
                expression = SafeRegex.Create(Find, caseSensitive: true);
            }
            catch (ArgumentException exception)
            {
                throw new OperationInvalidException($"find is not a valid regular expression: {exception.Message}");
            }

            // As in search: an expression that matches nothing redacts nothing, and the engine
            // rejects zero-width matches; say so before the document opens.
            OperationInvalidException.Require(!expression.IsMatch(string.Empty), "find must not match an empty string");
        }

        return this;
    }
}

/// <summary>Redacts a rectangle of one page.</summary>
[Operation("redact_area")]
public sealed record RedactAreaOp : PdfOp
{
    [Minimum(1)] public required int Page { get; init; }

    [NotState]
    public required PdfRectInput Rect { get; init; }

    [NotState]
    [HexColor] public string FillColor { get; init; } = "#000000";
}

/// <summary>Sets document information entries; omitted entries keep their values.</summary>
[Operation("set_properties")]
[MistakenFor("set_metadata")]
public sealed record SetPropertiesOp : PdfOp
{
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Title { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Author { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Subject { get; init; }

    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public string? Keywords { get; init; }

    /// <summary>
    /// Custom document information entries by name, which <c>pdf inspect --detail properties</c>
    /// reports under the same names. A name must not be one of the standard entries, which the
    /// fields above or the engine set, in any case, nor start with <c>info:</c> or <c>xmp:</c>.
    /// </summary>
    [ReadBack("inspect-result.schema.json#/properties/properties/additionalProperties")]
    public IReadOnlyDictionary<string, string>? Custom { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        foreach (string key in Custom?.Keys ?? [])
        {
            if (PdfPropertyNames.Standard(key) is { } standard)
            {
                throw new OperationInvalidException(
                    $"custom must not name the standard entry '{key}'",
                    standard.Field is { } field
                        ? $"Set it with the {field} field instead."
                        : $"The engine writes '{standard.Name}' into every PDF it saves; drop it from custom.");
            }

            OperationInvalidException.Require(
                !key.StartsWith("info:", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("xmp:", StringComparison.OrdinalIgnoreCase),
                $"custom must not name '{key}': names starting with info: or xmp: are reserved for how 'pdf inspect' reports entries",
                "Name the custom entry without the info: or xmp: prefix.");
        }

        return this;
    }
}

/// <summary>
/// The names <c>pdf inspect --detail properties</c> reports document information entries under:
/// a standard entry under its own name, such as <c>title</c> for <c>Title</c>, and a custom entry
/// under its key. Reads and the edit's targets name entries the same way.
/// </summary>
internal static class PdfPropertyNames
{
    private static readonly (string Key, string Name, string? Field)[] Entries =
    [
        ("Title", "title", "title"),
        ("Author", "author", "author"),
        ("Subject", "subject", "subject"),
        ("Keywords", "keywords", "keywords"),
        ("Creator", "creator", null),
        ("Producer", "producer", null),
        ("CreationDate", "creationDate", null),
        ("ModDate", "modificationDate", null),
    ];

    /// <summary>The standard entry a key or reported name spells in any case, or null for a custom key.</summary>
    internal static (string Key, string Name, string? Field)? Standard(string key) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase)) is { Key: not null } found ? found : null;

    /// <summary>
    /// The name an entry of <paramref name="key"/> is reported under, or null for a key a file
    /// wrote that spells a standard entry in another case, which only the standard entry reports.
    /// </summary>
    internal static string? Reported(string key) =>
        Standard(key) is { } standard ? (string.Equals(standard.Key, key, StringComparison.Ordinal) ? standard.Name : null) : key;
}

/// <summary>Clears the document properties: the document information entries and the XMP metadata.</summary>
[Operation("clear_properties")]
[MistakenFor("remove_metadata")]
public sealed record ClearPropertiesOp : PdfOp
{
    /// <summary>Whether the XMP metadata stream is removed.</summary>
    [NotState]
    public bool Xmp { get; init; } = true;

    /// <summary>Whether the document information dictionary is cleared.</summary>
    [NotState]
    public bool DocumentInfo { get; init; } = true;
}

/// <summary>Adds a bookmark that opens a page.</summary>
[Operation("add_bookmark")]
public sealed record AddBookmarkOp : PdfOp
{
    /// <summary>The bookmark's title; it must not contain '/', which joins the positions of a bookmark address.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfOutlineItem/properties/title")]
    [MinLength(1), Pattern("^[^/]*$", Meaning = "must not contain '/', which joins the positions of a bookmark address")]
    public required string Title { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfOutlineItem/properties/page")]
    [Minimum(1)] public required int Page { get; init; }

    /// <summary>
    /// The parent bookmark, as <c>pdf inspect --detail outline</c> reports it: 1-based positions
    /// joined by '/', so "2/1" is the first child of the second top-level bookmark. The new
    /// bookmark is appended after the parent's existing children, or after the existing
    /// top-level bookmarks when omitted, so no existing bookmark address changes.
    /// </summary>
    [NotState]
    [Pattern(BookmarkAddress.Pattern, Meaning = BookmarkAddress.Meaning)] public string? Parent { get; init; }
}

/// <summary>
/// Deletes the listed bookmarks with their children, or every bookmark when none are listed. A
/// bookmark must not repeat or lie inside another listed bookmark, which already deletes its
/// children.
/// </summary>
[Operation("delete_bookmarks")]
public sealed record DeleteBookmarksOp : PdfOp
{
    /// <summary>
    /// The bookmarks, as <c>pdf inspect --detail outline</c> reports them: 1-based positions
    /// joined by '/', so "2/1" is the first child of the second top-level bookmark; every
    /// bookmark when omitted. Every address resolves against the outline as it stands before
    /// this operation, so their order does not matter, and all of them are deleted together;
    /// later operations in the batch address the renumbered outline.
    /// </summary>
    [MinItems(1), Pattern(BookmarkAddress.Pattern, Meaning = BookmarkAddress.Meaning)]
    [MistakenFor("indexes")]
    [NotState]
    public IReadOnlyList<string>? Bookmarks { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Bookmarks is null)
        {
            return this;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string bookmark in Bookmarks)
        {
            OperationInvalidException.Require(
                seen.Add(bookmark), $"bookmarks must not repeat \"{bookmark}\"", $"Remove the repeated \"{bookmark}\".");
            if (Bookmarks.FirstOrDefault(other => bookmark.StartsWith(other + "/", StringComparison.Ordinal)) is { } parent)
            {
                throw new OperationInvalidException(
                    $"bookmarks must not list \"{bookmark}\" with \"{parent}\": deleting bookmark {parent} already deletes its children",
                    $"Remove \"{bookmark}\" from bookmarks.");
            }
        }

        return this;
    }
}

/// <summary>The bookmark address that <c>pdf inspect --detail outline</c> reports and the bookmark operations take.</summary>
internal static class BookmarkAddress
{
    internal const string Pattern = @"^[1-9][0-9]*(/[1-9][0-9]*)*$";

    internal const string Meaning =
        "must be a bookmark address as 'pdf inspect --detail outline' reports it, 1-based positions joined by '/' such as \"2/1\"";
}

/// <summary>Embeds a file as a document attachment.</summary>
[Operation("add_attachment")]
public sealed record AddAttachmentOp : PdfOp
{
    /// <summary>The file, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>The stable attachment name; the file name when omitted, never its local path.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfAttachmentInfo/properties/name")]
    [MinLength(1)] public string? Name { get; init; }

    [ReadBack("inspect-result.schema.json#/$defs/pdfAttachmentInfo/properties/description")]
    public string? Description { get; init; }

    /// <summary>
    /// The media type stored with the attachment, such as "image/png", which archives and viewers
    /// read to identify it and PDF/A-3 keeps; none when omitted.
    /// </summary>
    [Pattern(@"^[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*/[A-Za-z0-9][A-Za-z0-9!#$&^_.+-]*$", Meaning = "must be a media type, such as \"image/png\"")]
    [ReadBack("inspect-result.schema.json#/$defs/pdfAttachmentInfo/properties/mimeType")]
    public string? MimeType { get; init; }
}

/// <summary>Deletes a document attachment by name.</summary>
[Operation("delete_attachment")]
[MistakenFor("remove_attachment")]
public sealed record DeleteAttachmentOp : PdfOp
{
    [NotState]
    [MinLength(1)] public required string Name { get; init; }
}

/// <summary>Sets page-label ranges, which change navigation labels, not visible page text; each start page appears once.</summary>
[Operation("set_page_labels")]
public sealed record SetPageLabelsOp : PdfOp
{
    [MinItems(1)] public required IReadOnlyList<PdfPageLabelRange> Ranges { get; init; }

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        OperationInvalidException.Require(
            Ranges.Select(static range => range.StartPage).Distinct().Count() == Ranges.Count,
            "ranges must not repeat a startPage");
        return this;
    }
}

/// <summary>Sets the value of one form field, named by its full name.</summary>
[Operation("set_form_field")]
public sealed record SetFormFieldOp : PdfOp
{
    [NotState]
    [MinLength(1)] public required string Name { get; init; }

    /// <summary>
    /// The value to set; null clears the field: a radio group selects no button, a check box
    /// is unchecked and any other field is emptied.
    /// </summary>
    [ReadBack("inspect-result.schema.json#/$defs/pdfFormField/properties/value")]
    public required string? Value { get; init; }
}

/// <summary>Flattens form fields into page content: the named fields, or every field.</summary>
[Operation("flatten_forms")]
public sealed record FlattenFormsOp : PdfOp
{
    /// <summary>The full names of the fields to flatten, each with every widget of the name, such as all buttons of a radio group; every field when omitted.</summary>
    [NotState]
    [MinItems(1), MinLength(1)] public IReadOnlyList<string>? Fields { get; init; }
}

/// <summary>Encrypts the document with AES-256; the published file needs the user password to open when one is set.</summary>
[Operation("encrypt_document"), EncryptsOutput]
[MistakenFor("encrypt")]
public sealed record EncryptDocumentOp : PdfOp
{
    /// <summary>The environment variable that holds the password to open the document; anyone can open it when omitted.</summary>
    [NotState]
    [SecretEnv] public string? UserPasswordEnv { get; init; }

    /// <summary>The environment variable that holds the owner password, which grants every permission.</summary>
    [NotState]
    [SecretEnv] public required string OwnerPasswordEnv { get; init; }

    public PdfPermissionsInput Permissions { get; init; } = new();
}

/// <summary>Removes the document's encryption; an unencrypted document is unchanged.</summary>
[Operation("decrypt_document")]
[MistakenFor("decrypt")]
public sealed record DecryptDocumentOp : PdfOp;

/// <summary>Optimizes the document's resources; image options recompress images.</summary>
[Operation("optimize_document")]
[MistakenFor("optimize")]
public sealed record OptimizeDocumentOp : PdfOp
{
    /// <summary>The largest image resolution kept, in dots per inch.</summary>
    [NotState]
    [Minimum(36), Maximum(1200)] public int? DownsampleImagesDpi { get; init; }

    /// <summary>The JPEG quality of recompressed images.</summary>
    [NotState]
    [Minimum(1), Maximum(100)] public int? ImageQuality { get; init; }

    [NotState]
    public bool UnembedFonts { get; init; }

    [NotState]
    public bool RemoveUnusedObjects { get; init; } = true;

    [NotState]
    public bool CompressStreams { get; init; } = true;
}

/// <summary>Source-generated serialization metadata for PDF operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AddAttachmentOp))]
[JsonSerializable(typeof(AddBookmarkOp))]
[JsonSerializable(typeof(AddFooterTextOp))]
[JsonSerializable(typeof(AddHeaderTextOp))]
[JsonSerializable(typeof(AddHyperlinkOp))]
[JsonSerializable(typeof(AddPageNumbersOp))]
[JsonSerializable(typeof(AddStampImageOp))]
[JsonSerializable(typeof(AddWatermarkImageOp))]
[JsonSerializable(typeof(AddWatermarkTextOp))]
[JsonSerializable(typeof(ClearPropertiesOp))]
[JsonSerializable(typeof(CropPagesOp))]
[JsonSerializable(typeof(DecryptDocumentOp))]
[JsonSerializable(typeof(DeleteAttachmentOp))]
[JsonSerializable(typeof(DeleteBookmarksOp))]
[JsonSerializable(typeof(DeletePagesOp))]
[JsonSerializable(typeof(EncryptDocumentOp))]
[JsonSerializable(typeof(FlattenFormsOp))]
[JsonSerializable(typeof(ImportPagesOp))]
[JsonSerializable(typeof(InsertPageOp))]
[JsonSerializable(typeof(MovePagesOp))]
[JsonSerializable(typeof(OptimizeDocumentOp))]
[JsonSerializable(typeof(RedactAreaOp))]
[JsonSerializable(typeof(RedactTextOp))]
[JsonSerializable(typeof(RotatePagesOp))]
[JsonSerializable(typeof(SetFormFieldOp))]
[JsonSerializable(typeof(SetPageLabelsOp))]
[JsonSerializable(typeof(SetPageSizeOp))]
[JsonSerializable(typeof(SetPropertiesOp))]
internal sealed partial class PdfOpsJsonContext : JsonSerializerContext;
