using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>A validated atomic PDF edit batch.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Aspose.Cli.Sdk.Serialization.ProductJsonRoot]
public sealed record PdfOpsBatch : BoundedOperationEnvelope<PdfOp>;

/// <summary>Base of every PDF operation.</summary>
[JsonConverter(typeof(Serialization.PdfOpJsonConverter))]
public abstract record PdfOp : BoundedOperation;

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

public sealed record RotatePagesOp : PdfOp { public required string Pages { get; init; } public required int Angle { get; init; } }
public sealed record DeletePagesOp : PdfOp { public required string Pages { get; init; } }
public sealed record MovePagesOp : PdfOp { public required string Pages { get; init; } public required int To { get; init; } }
public sealed record InsertPagesFromOp : PdfOp { public required string Path { get; init; } public string? Pages { get; init; } public required int At { get; init; } public string? PasswordEnv { get; init; } }
public sealed record InsertBlankPageOp : PdfOp { public required int At { get; init; } public string Size { get; init; } = "A4"; }
public sealed record CropPagesOp : PdfOp { public required string Pages { get; init; } public string Box { get; init; } = "crop"; public required PdfRectInput Rect { get; init; } }
public sealed record SetPageSizeOp : PdfOp { public required string Pages { get; init; } public required string Size { get; init; } public bool ScaleContent { get; init; } }

public sealed record AddWatermarkTextOp : PdfOp { public string? Pages { get; init; } public required string Text { get; init; } public string? Font { get; init; } public double Size { get; init; } = 48; public string Color { get; init; } = "#808080"; public double Opacity { get; init; } = 0.35; public double Rotation { get; init; } = 45; public string Layer { get; init; } = "over"; }
public sealed record AddWatermarkImageOp : PdfOp { public string? Pages { get; init; } public required string Path { get; init; } public double Opacity { get; init; } = 0.35; public double Scale { get; init; } = 0.5; public string Layer { get; init; } = "over"; }
public sealed record AddPageNumbersOp : PdfOp { public string? Pages { get; init; } public string Format { get; init; } = "Page {n} of {N}"; public string Position { get; init; } = "bottom-center"; public int Start { get; init; } = 1; public string? Font { get; init; } }
public sealed record AddHeaderTextOp : PdfOp { public string? Pages { get; init; } public required string Text { get; init; } public string Position { get; init; } = "top-center"; public string? Font { get; init; } }
public sealed record AddFooterTextOp : PdfOp { public string? Pages { get; init; } public required string Text { get; init; } public string Position { get; init; } = "bottom-center"; public string? Font { get; init; } }
public sealed record AddStampImageOp : PdfOp { public required int Page { get; init; } public required string Path { get; init; } public required PdfRectInput Rect { get; init; } }
public sealed record AddLinkOp : PdfOp { public required int Page { get; init; } public required PdfRectInput Rect { get; init; } public required string Url { get; init; } }
public sealed record RedactTextOp : PdfOp { public required string Pattern { get; init; } public bool Regex { get; init; } public string? Pages { get; init; } public string FillColor { get; init; } = "#000000"; }
public sealed record RedactAreaOp : PdfOp { public required int Page { get; init; } public required PdfRectInput Rect { get; init; } public string FillColor { get; init; } = "#000000"; }

public sealed record SetMetadataOp : PdfOp { public string? Title { get; init; } public string? Author { get; init; } public string? Subject { get; init; } public string? Keywords { get; init; } public IReadOnlyDictionary<string, string>? Custom { get; init; } }
public sealed record RemoveMetadataOp : PdfOp { public bool Xmp { get; init; } = true; public bool DocumentInfo { get; init; } = true; }
public sealed record AddBookmarkOp : PdfOp { public required string Title { get; init; } public required int Page { get; init; } public string? Parent { get; init; } }
public sealed record DeleteBookmarksOp : PdfOp { public string? Path { get; init; } public bool All { get; init; } }
public sealed record AddAttachmentOp : PdfOp { public required string Path { get; init; } public string? Name { get; init; } public string? Description { get; init; } }
public sealed record RemoveAttachmentOp : PdfOp { public required string Name { get; init; } }
public sealed record SetPageLabelsOp : PdfOp { public required IReadOnlyList<PdfPageLabelRange> Ranges { get; init; } }
public sealed record SetFormFieldOp : PdfOp { public required string Name { get; init; } public required string Value { get; init; } }
public sealed record FlattenFormsOp : PdfOp { public IReadOnlyList<string>? Fields { get; init; } public bool All { get; init; } = true; }
public sealed record EncryptPdfOp : PdfOp { public string? UserPasswordEnv { get; init; } public required string OwnerPasswordEnv { get; init; } public PdfPermissionsInput Permissions { get; init; } = new(); }
public sealed record DecryptPdfOp : PdfOp;
public sealed record OptimizePdfOp : PdfOp { public int? DownsampleImagesDpi { get; init; } public int? ImageQuality { get; init; } public bool UnembedFonts { get; init; } public bool RemoveUnusedObjects { get; init; } = true; public bool CompressStreams { get; init; } = true; }

/// <summary>The PDF operation vocabulary, in published order.</summary>
public static class PdfOps
{
    public static OperationCatalog<PdfOp> Catalog { get; } = new OperationCatalog<PdfOp>(PdfSchemaIds.Ops, maximumOperations: 256)
        .Add<AddAttachmentOp>("add_attachment", PdfOpRules.Attachment)
        .Add<AddBookmarkOp>("add_bookmark", PdfOpRules.Bookmark)
        .Add<AddFooterTextOp>("add_footer_text", PdfOpRules.FooterText)
        .Add<AddHeaderTextOp>("add_header_text", PdfOpRules.HeaderText)
        .Add<AddLinkOp>("add_link", PdfOpRules.Link)
        .Add<AddPageNumbersOp>("add_page_numbers", PdfOpRules.PageNumbers)
        .Add<AddStampImageOp>("add_stamp_image", PdfOpRules.StampImage)
        .Add<AddWatermarkImageOp>("add_watermark_image", PdfOpRules.WatermarkImage)
        .Add<AddWatermarkTextOp>("add_watermark_text", PdfOpRules.WatermarkText)
        .Add<CropPagesOp>("crop_pages", PdfOpRules.CropPages)
        .Add<DecryptPdfOp>("decrypt")
        .Add<DeleteBookmarksOp>("delete_bookmarks", PdfOpRules.DeleteBookmarks)
        .Add<DeletePagesOp>("delete_pages", PdfOpRules.DeletePages)
        .Add<EncryptPdfOp>("encrypt", PdfOpRules.Encrypt)
        .Add<FlattenFormsOp>("flatten_forms", PdfOpRules.FlattenForms)
        .Add<InsertBlankPageOp>("insert_blank_page", PdfOpRules.InsertBlankPage)
        .Add<InsertPagesFromOp>("insert_pages_from", PdfOpRules.InsertPagesFrom)
        .Add<MovePagesOp>("move_pages", PdfOpRules.MovePages)
        .Add<OptimizePdfOp>("optimize", PdfOpRules.Optimize)
        .Add<RedactAreaOp>("redact_area", PdfOpRules.RedactArea)
        .Add<RedactTextOp>("redact_text", PdfOpRules.RedactText)
        .Add<RemoveAttachmentOp>("remove_attachment", PdfOpRules.RemoveAttachment)
        .Add<RemoveMetadataOp>("remove_metadata")
        .Add<RotatePagesOp>("rotate_pages", PdfOpRules.RotatePages)
        .Add<SetFormFieldOp>("set_form_field", PdfOpRules.FormField)
        .Add<SetMetadataOp>("set_metadata")
        .Add<SetPageLabelsOp>("set_page_labels", PdfOpRules.PageLabels)
        .Add<SetPageSizeOp>("set_page_size", PdfOpRules.SetPageSize);
}
