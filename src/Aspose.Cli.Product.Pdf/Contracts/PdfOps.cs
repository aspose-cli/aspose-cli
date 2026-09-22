using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>A validated atomic PDF edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Aspose.Cli.Sdk.Serialization.ProductJsonRoot]
public sealed record PdfOpsBatch : BoundedOperationEnvelope<PdfOp>;

/// <summary>Base of every PDF operation.</summary>
[JsonConverter(typeof(Serialization.PdfOpJsonConverter))]
public abstract record PdfOp : BoundedOperation
{
    [JsonIgnore]
    public abstract string OpName { get; }
}

/// <summary>Points relative to the visible rotated page box, with a top-left origin.</summary>
public sealed record PdfRectInput
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}

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

public sealed record PdfPageLabelRange
{
    public required int StartPage { get; init; }
    public string Style { get; init; } = "arabic";
    public string? Prefix { get; init; }
    public int StartingValue { get; init; } = 1;
}

public sealed record RotatePagesOp : PdfOp { public override string OpName => "rotate_pages"; public required string Pages { get; init; } public required int Angle { get; init; } }
public sealed record DeletePagesOp : PdfOp { public override string OpName => "delete_pages"; public required string Pages { get; init; } }
public sealed record MovePagesOp : PdfOp { public override string OpName => "move_pages"; public required string Pages { get; init; } public required int To { get; init; } }
public sealed record InsertPagesFromOp : PdfOp { public override string OpName => "insert_pages_from"; public required string Path { get; init; } public string? Pages { get; init; } public required int At { get; init; } public string? PasswordEnv { get; init; } }
public sealed record InsertBlankPageOp : PdfOp { public override string OpName => "insert_blank_page"; public required int At { get; init; } public string Size { get; init; } = "A4"; }
public sealed record CropPagesOp : PdfOp { public override string OpName => "crop_pages"; public required string Pages { get; init; } public string Box { get; init; } = "crop"; public required PdfRectInput Rect { get; init; } }
public sealed record SetPageSizeOp : PdfOp { public override string OpName => "set_page_size"; public required string Pages { get; init; } public required string Size { get; init; } public bool ScaleContent { get; init; } }

public sealed record AddWatermarkTextOp : PdfOp { public override string OpName => "add_watermark_text"; public string? Pages { get; init; } public required string Text { get; init; } public string? Font { get; init; } public double Size { get; init; } = 48; public string Color { get; init; } = "#808080"; public double Opacity { get; init; } = 0.35; public double Rotation { get; init; } = 45; public string Layer { get; init; } = "over"; }
public sealed record AddWatermarkImageOp : PdfOp { public override string OpName => "add_watermark_image"; public string? Pages { get; init; } public required string Path { get; init; } public double Opacity { get; init; } = 0.35; public double Scale { get; init; } = 0.5; public string Layer { get; init; } = "over"; }
public sealed record AddPageNumbersOp : PdfOp { public override string OpName => "add_page_numbers"; public string? Pages { get; init; } public string Format { get; init; } = "Page {n} of {N}"; public string Position { get; init; } = "bottom-center"; public int Start { get; init; } = 1; public string? Font { get; init; } }
public sealed record AddHeaderTextOp : PdfOp { public override string OpName => "add_header_text"; public string? Pages { get; init; } public required string Text { get; init; } public string Position { get; init; } = "top-center"; public string? Font { get; init; } }
public sealed record AddFooterTextOp : PdfOp { public override string OpName => "add_footer_text"; public string? Pages { get; init; } public required string Text { get; init; } public string Position { get; init; } = "bottom-center"; public string? Font { get; init; } }
public sealed record AddStampImageOp : PdfOp { public override string OpName => "add_stamp_image"; public required int Page { get; init; } public required string Path { get; init; } public required PdfRectInput Rect { get; init; } }
public sealed record AddLinkOp : PdfOp { public override string OpName => "add_link"; public required int Page { get; init; } public required PdfRectInput Rect { get; init; } public required string Url { get; init; } }
public sealed record RedactTextOp : PdfOp { public override string OpName => "redact_text"; public required string Pattern { get; init; } public bool Regex { get; init; } public string? Pages { get; init; } public string FillColor { get; init; } = "#000000"; }
public sealed record RedactAreaOp : PdfOp { public override string OpName => "redact_area"; public required int Page { get; init; } public required PdfRectInput Rect { get; init; } public string FillColor { get; init; } = "#000000"; }

public sealed record SetMetadataOp : PdfOp { public override string OpName => "set_metadata"; public string? Title { get; init; } public string? Author { get; init; } public string? Subject { get; init; } public string? Keywords { get; init; } public IReadOnlyDictionary<string, string>? Custom { get; init; } }
public sealed record RemoveMetadataOp : PdfOp { public override string OpName => "remove_metadata"; public bool Xmp { get; init; } = true; public bool DocumentInfo { get; init; } = true; }
public sealed record AddBookmarkOp : PdfOp { public override string OpName => "add_bookmark"; public required string Title { get; init; } public required int Page { get; init; } public string? Parent { get; init; } }
public sealed record DeleteBookmarksOp : PdfOp { public override string OpName => "delete_bookmarks"; public string? Path { get; init; } public bool All { get; init; } }
public sealed record AddAttachmentOp : PdfOp { public override string OpName => "add_attachment"; public required string Path { get; init; } public string? Name { get; init; } public string? Description { get; init; } }
public sealed record RemoveAttachmentOp : PdfOp { public override string OpName => "remove_attachment"; public required string Name { get; init; } }
public sealed record SetPageLabelsOp : PdfOp { public override string OpName => "set_page_labels"; public required IReadOnlyList<PdfPageLabelRange> Ranges { get; init; } }
public sealed record SetFormFieldOp : PdfOp { public override string OpName => "set_form_field"; public required string Name { get; init; } public required string Value { get; init; } }
public sealed record FlattenFormsOp : PdfOp { public override string OpName => "flatten_forms"; public IReadOnlyList<string>? Fields { get; init; } public bool All { get; init; } = true; }
public sealed record EncryptPdfOp : PdfOp { public override string OpName => "encrypt"; public string? UserPasswordEnv { get; init; } public required string OwnerPasswordEnv { get; init; } public PdfPermissionsInput Permissions { get; init; } = new(); }
public sealed record DecryptPdfOp : PdfOp { public override string OpName => "decrypt"; }
public sealed record OptimizePdfOp : PdfOp { public override string OpName => "optimize"; public int? DownsampleImagesDpi { get; init; } public int? ImageQuality { get; init; } public bool UnembedFonts { get; init; } public bool RemoveUnusedObjects { get; init; } = true; public bool CompressStreams { get; init; } = true; }

/// <summary>Current PDF operation registry.</summary>
public static class PdfOps
{
    public static IReadOnlyDictionary<string, Type> Registry { get; } =
        new SortedDictionary<string, Type>(StringComparer.Ordinal)
        {
            ["add_attachment"] = typeof(AddAttachmentOp),
            ["add_bookmark"] = typeof(AddBookmarkOp),
            ["add_footer_text"] = typeof(AddFooterTextOp),
            ["add_header_text"] = typeof(AddHeaderTextOp),
            ["add_link"] = typeof(AddLinkOp),
            ["add_page_numbers"] = typeof(AddPageNumbersOp),
            ["add_stamp_image"] = typeof(AddStampImageOp),
            ["add_watermark_image"] = typeof(AddWatermarkImageOp),
            ["add_watermark_text"] = typeof(AddWatermarkTextOp),
            ["crop_pages"] = typeof(CropPagesOp),
            ["decrypt"] = typeof(DecryptPdfOp),
            ["delete_bookmarks"] = typeof(DeleteBookmarksOp),
            ["delete_pages"] = typeof(DeletePagesOp),
            ["encrypt"] = typeof(EncryptPdfOp),
            ["flatten_forms"] = typeof(FlattenFormsOp),
            ["insert_blank_page"] = typeof(InsertBlankPageOp),
            ["insert_pages_from"] = typeof(InsertPagesFromOp),
            ["move_pages"] = typeof(MovePagesOp),
            ["optimize"] = typeof(OptimizePdfOp),
            ["redact_area"] = typeof(RedactAreaOp),
            ["redact_text"] = typeof(RedactTextOp),
            ["remove_attachment"] = typeof(RemoveAttachmentOp),
            ["remove_metadata"] = typeof(RemoveMetadataOp),
            ["rotate_pages"] = typeof(RotatePagesOp),
            ["set_form_field"] = typeof(SetFormFieldOp),
            ["set_metadata"] = typeof(SetMetadataOp),
            ["set_page_labels"] = typeof(SetPageLabelsOp),
            ["set_page_size"] = typeof(SetPageSizeOp),
        };

    public static IReadOnlyList<string> Names { get; } = Registry.Keys.ToArray();
}
