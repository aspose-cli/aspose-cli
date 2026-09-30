using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;
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
[OperationVocabulary(PdfSchemaIds.Ops, MaximumOperations = 256, JsonContext = typeof(PdfOpsJsonContext))]
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
    public bool Print { get; init; }

    public bool Copy { get; init; }

    public bool Modify { get; init; }

    public bool Annotate { get; init; }

    public bool FillForms { get; init; }

    public bool ExtractAccessibility { get; init; }

    public bool Assemble { get; init; }

    public bool PrintHighResolution { get; init; }
}

/// <summary>One page-label range, which applies from its start page until the next range.</summary>
public sealed record PdfPageLabelRange
{
    /// <summary>The 1-based page where the range starts.</summary>
    [Minimum(1)] public required int StartPage { get; init; }

    [AllowedValues("arabic", "roman-upper", "roman-lower", "letters-upper", "letters-lower", "none")]
    public string Style { get; init; } = "arabic";

    /// <summary>Text placed before each label value.</summary>
    public string? Prefix { get; init; }

    /// <summary>The label value of the start page.</summary>
    [Minimum(0)] public int StartingValue { get; init; } = 1;
}

/// <summary>Where margin text sits on a page.</summary>
public static class PdfPositions
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

/// <summary>A named page size: <c>A3</c>, <c>A4</c>, <c>Letter</c> or <c>Legal</c>, matched exactly.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class PdfPageSizeAttribute() : AllowedValuesAttribute([.. PdfPageSizes.Names])
{
    public override string? Definition => "pageSize";
}

/// <summary>Rotates pages to an absolute angle.</summary>
[Operation("rotate_pages")]
public sealed record RotatePagesOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    /// <summary>The rotation in degrees, clockwise.</summary>
    [AllowedValues(90, 180, 270)] public required int Angle { get; init; }
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
    [Minimum(1)] public required int To { get; init; }
}

/// <summary>Inserts pages of another PDF.</summary>
[Operation("insert_pages_from")]
public sealed record InsertPagesFromOp : PdfOp
{
    /// <summary>The source PDF, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>The source pages; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    /// <summary>The 1-based position of the first inserted page, at most the page count plus one.</summary>
    [Minimum(1)] public required int At { get; init; }

    /// <summary>The environment variable that holds the source PDF's password.</summary>
    [SecretEnv] public string? PasswordEnv { get; init; }
}

/// <summary>Inserts one blank page.</summary>
[Operation("insert_blank_page")]
public sealed record InsertBlankPageOp : PdfOp
{
    /// <summary>The 1-based position of the new page, at most the page count plus one.</summary>
    [Minimum(1)] public required int At { get; init; }

    [PdfPageSize] public string Size { get; init; } = "A4";
}

/// <summary>Crops pages to a rectangle.</summary>
[Operation("crop_pages")]
public sealed record CropPagesOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    /// <summary>The page box to set: the visible crop box or the physical media box.</summary>
    [AllowedValues("media", "crop")] public string Box { get; init; } = "crop";

    public required PdfRectInput Rect { get; init; }
}

/// <summary>Sets pages to a named size.</summary>
[Operation("set_page_size")]
public sealed record SetPageSizeOp : PdfOp
{
    [PageRange] public required string Pages { get; init; }

    [PdfPageSize] public required string Size { get; init; }

    /// <summary>Whether the content is scaled to the new size rather than kept at its position.</summary>
    public bool ScaleContent { get; init; }
}

/// <summary>Stamps text across the middle of pages.</summary>
[Operation("add_watermark_text")]
public sealed record AddWatermarkTextOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [MinLength(1)] public required string Text { get; init; }

    /// <summary>An installed font name; the default font when omitted.</summary>
    public string? Font { get; init; }

    /// <summary>The font size in points.</summary>
    [ExclusiveMinimum(0)] public double Size { get; init; } = 48;

    [HexColor] public string Color { get; init; } = "#808080";

    [Minimum(0), Maximum(1)] public double Opacity { get; init; } = 0.35;

    /// <summary>The angle in degrees, counterclockwise.</summary>
    public double Rotation { get; init; } = 45;

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

    [Minimum(0), Maximum(1)] public double Opacity { get; init; } = 0.35;

    /// <summary>The zoom factor applied to the image.</summary>
    [ExclusiveMinimum(0)] public double Scale { get; init; } = 0.5;

    [AllowedValues(typeof(PdfLayers))] public string Layer { get; init; } = PdfLayers.Over;
}

/// <summary>Stamps page numbers in a page margin.</summary>
[Operation("add_page_numbers")]
public sealed record AddPageNumbersOp : PdfOp
{
    /// <summary>The pages to number; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    /// <summary>The text; {n} is the number, counted over the selected pages from start, and {N} the document's page count.</summary>
    public string Format { get; init; } = "Page {n} of {N}";

    [AllowedValues(typeof(PdfPositions))] public string Position { get; init; } = PdfPositions.BottomCenter;

    /// <summary>The number of the first selected page.</summary>
    [Minimum(0)] public int Start { get; init; } = 1;

    /// <summary>An installed font name; the default font when omitted.</summary>
    public string? Font { get; init; }
}

/// <summary>Stamps text in the top margin of pages.</summary>
[Operation("add_header_text")]
public sealed record AddHeaderTextOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [MinLength(1)] public required string Text { get; init; }

    [AllowedValues(typeof(PdfPositions))] public string Position { get; init; } = PdfPositions.TopCenter;

    /// <summary>An installed font name; the default font when omitted.</summary>
    public string? Font { get; init; }
}

/// <summary>Stamps text in the bottom margin of pages.</summary>
[Operation("add_footer_text")]
public sealed record AddFooterTextOp : PdfOp
{
    /// <summary>The pages to stamp; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [MinLength(1)] public required string Text { get; init; }

    [AllowedValues(typeof(PdfPositions))] public string Position { get; init; } = PdfPositions.BottomCenter;

    /// <summary>An installed font name; the default font when omitted.</summary>
    public string? Font { get; init; }
}

/// <summary>Stamps an image into a rectangle of one page.</summary>
[Operation("add_stamp_image")]
public sealed record AddStampImageOp : PdfOp
{
    [Minimum(1)] public required int Page { get; init; }

    /// <summary>The image, relative to the working directory; an SVG must not reference network resources.</summary>
    [InputPath] public required string Path { get; init; }

    public required PdfRectInput Rect { get; init; }
}

/// <summary>Adds a clickable link area to one page.</summary>
[Operation("add_link")]
public sealed record AddLinkOp : PdfOp
{
    [Minimum(1)] public required int Page { get; init; }

    public required PdfRectInput Rect { get; init; }

    [WebLink] public required string Url { get; init; }
}

/// <summary>
/// Redacts every match of a text pattern in the extractable text; it does not perform OCR. A
/// regular expression runs with a one-second timeout and must not match an empty string.
/// </summary>
[Operation("redact_text")]
public sealed record RedactTextOp : PdfOp
{
    /// <summary>The text to find, matched case-sensitively.</summary>
    [MinLength(1)] public required string Pattern { get; init; }

    /// <summary>Whether the pattern is a .NET regular expression rather than literal text.</summary>
    public bool Regex { get; init; }

    /// <summary>The pages to search; every page when omitted.</summary>
    [PageRange] public string? Pages { get; init; }

    [HexColor] public string FillColor { get; init; } = "#000000";

    /// <inheritdoc />
    protected override BoundedOperation Validated()
    {
        if (Regex)
        {
            System.Text.RegularExpressions.Regex expression;
            try
            {
                expression = SafeRegex.Create(Pattern, caseSensitive: true);
            }
            catch (ArgumentException exception)
            {
                throw new OperationInvalidException($"pattern is not a valid regular expression: {exception.Message}");
            }

            // As in search: an expression that matches nothing redacts nothing, and the engine
            // rejects zero-width matches; say so before the document opens.
            OperationInvalidException.Require(!expression.IsMatch(string.Empty), "pattern must not match an empty string");
        }

        return this;
    }
}

/// <summary>Redacts a rectangle of one page.</summary>
[Operation("redact_area")]
public sealed record RedactAreaOp : PdfOp
{
    [Minimum(1)] public required int Page { get; init; }

    public required PdfRectInput Rect { get; init; }

    [HexColor] public string FillColor { get; init; } = "#000000";
}

/// <summary>Sets document information entries; omitted entries keep their values.</summary>
[Operation("set_metadata")]
public sealed record SetMetadataOp : PdfOp
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Subject { get; init; }

    public string? Keywords { get; init; }

    /// <summary>Custom document information entries by name.</summary>
    public IReadOnlyDictionary<string, string>? Custom { get; init; }
}

/// <summary>Removes document metadata.</summary>
[Operation("remove_metadata")]
public sealed record RemoveMetadataOp : PdfOp
{
    /// <summary>Whether the XMP metadata stream is removed.</summary>
    public bool Xmp { get; init; } = true;

    /// <summary>Whether the document information dictionary is cleared.</summary>
    public bool DocumentInfo { get; init; } = true;
}

/// <summary>Adds a bookmark that opens a page.</summary>
[Operation("add_bookmark")]
public sealed record AddBookmarkOp : PdfOp
{
    [MinLength(1)] public required string Title { get; init; }

    [Minimum(1)] public required int Page { get; init; }

    /// <summary>The slash-separated title path of the parent bookmark; a top-level bookmark when omitted.</summary>
    [MinLength(1)] public string? Parent { get; init; }
}

/// <summary>Deletes one bookmark with its children, or every bookmark.</summary>
[Operation("delete_bookmarks")]
[ExactlyOneOf("path", "all")]
public sealed record DeleteBookmarksOp : PdfOp
{
    /// <summary>The slash-separated title path of the bookmark.</summary>
    [MinLength(1)] public string? Path { get; init; }

    /// <summary>Whether every bookmark is deleted.</summary>
    public bool All { get; init; }
}

/// <summary>Embeds a file as a document attachment.</summary>
[Operation("add_attachment")]
public sealed record AddAttachmentOp : PdfOp
{
    /// <summary>The file, relative to the working directory.</summary>
    [InputPath] public required string Path { get; init; }

    /// <summary>The stable attachment name; the file name when omitted, never its local path.</summary>
    [MinLength(1)] public string? Name { get; init; }

    public string? Description { get; init; }
}

/// <summary>Removes a document attachment by name.</summary>
[Operation("remove_attachment")]
public sealed record RemoveAttachmentOp : PdfOp
{
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
    [MinLength(1)] public required string Name { get; init; }

    public required string Value { get; init; }
}

/// <summary>Flattens form fields into page content: the named fields, or every field.</summary>
[Operation("flatten_forms")]
public sealed record FlattenFormsOp : PdfOp
{
    /// <summary>The full names of the fields to flatten; every field when omitted.</summary>
    [MinItems(1), MinLength(1)] public IReadOnlyList<string>? Fields { get; init; }
}

/// <summary>Encrypts the document with AES-256; the published file needs the user password to open when one is set.</summary>
[Operation("encrypt")]
public sealed record EncryptPdfOp : PdfOp
{
    /// <summary>The environment variable that holds the password to open the document; anyone can open it when omitted.</summary>
    [SecretEnv] public string? UserPasswordEnv { get; init; }

    /// <summary>The environment variable that holds the owner password, which grants every permission.</summary>
    [SecretEnv] public required string OwnerPasswordEnv { get; init; }

    public PdfPermissionsInput Permissions { get; init; } = new();
}

/// <summary>Removes the document's encryption; an unencrypted document is unchanged.</summary>
[Operation("decrypt")]
public sealed record DecryptPdfOp : PdfOp;

/// <summary>Optimizes the document's resources; image options recompress images.</summary>
[Operation("optimize")]
public sealed record OptimizePdfOp : PdfOp
{
    /// <summary>The largest image resolution kept, in dots per inch.</summary>
    [Minimum(36), Maximum(1200)] public int? DownsampleImagesDpi { get; init; }

    /// <summary>The JPEG quality of recompressed images.</summary>
    [Minimum(1), Maximum(100)] public int? ImageQuality { get; init; }

    public bool UnembedFonts { get; init; }

    public bool RemoveUnusedObjects { get; init; } = true;

    public bool CompressStreams { get; init; } = true;
}

/// <summary>Source-generated serialization metadata for PDF operations.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(AddAttachmentOp))]
[JsonSerializable(typeof(AddBookmarkOp))]
[JsonSerializable(typeof(AddFooterTextOp))]
[JsonSerializable(typeof(AddHeaderTextOp))]
[JsonSerializable(typeof(AddLinkOp))]
[JsonSerializable(typeof(AddPageNumbersOp))]
[JsonSerializable(typeof(AddStampImageOp))]
[JsonSerializable(typeof(AddWatermarkImageOp))]
[JsonSerializable(typeof(AddWatermarkTextOp))]
[JsonSerializable(typeof(CropPagesOp))]
[JsonSerializable(typeof(DecryptPdfOp))]
[JsonSerializable(typeof(DeleteBookmarksOp))]
[JsonSerializable(typeof(DeletePagesOp))]
[JsonSerializable(typeof(EncryptPdfOp))]
[JsonSerializable(typeof(FlattenFormsOp))]
[JsonSerializable(typeof(InsertBlankPageOp))]
[JsonSerializable(typeof(InsertPagesFromOp))]
[JsonSerializable(typeof(MovePagesOp))]
[JsonSerializable(typeof(OptimizePdfOp))]
[JsonSerializable(typeof(RedactAreaOp))]
[JsonSerializable(typeof(RedactTextOp))]
[JsonSerializable(typeof(RemoveAttachmentOp))]
[JsonSerializable(typeof(RemoveMetadataOp))]
[JsonSerializable(typeof(RotatePagesOp))]
[JsonSerializable(typeof(SetFormFieldOp))]
[JsonSerializable(typeof(SetMetadataOp))]
[JsonSerializable(typeof(SetPageLabelsOp))]
[JsonSerializable(typeof(SetPageSizeOp))]
internal sealed partial class PdfOpsJsonContext : JsonSerializerContext;
