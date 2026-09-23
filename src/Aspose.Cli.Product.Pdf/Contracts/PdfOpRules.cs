using System.Globalization;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Sdk.Operations.OperationInvalidException;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Semantic rules of PDF operations that the contract types cannot express.</summary>
internal static class PdfOpRules
{
    internal static void RotatePages(RotatePagesOp op)
    {
        Pages(op.Pages);
        Require(op.Angle is 90 or 180 or 270, "angle must be 90, 180 or 270");
    }

    internal static void DeletePages(DeletePagesOp op) => Pages(op.Pages);

    internal static void MovePages(MovePagesOp op)
    {
        Pages(op.Pages);
        Require(op.To > 0, "to is 1-based");
    }

    internal static void InsertPagesFrom(InsertPagesFromOp op)
    {
        Require(op.Path.Length > 0, "path is required");
        OptionalPages(op.Pages);
        Require(op.At > 0, "at is 1-based");
    }

    internal static void InsertBlankPage(InsertBlankPageOp op)
    {
        Require(op.At > 0, "at is 1-based");
        PageSize(op.Size);
    }

    internal static void CropPages(CropPagesOp op)
    {
        Pages(op.Pages);
        Require(op.Box is "media" or "crop", "box must be media or crop");
        Rect(op.Rect);
    }

    internal static void SetPageSize(SetPageSizeOp op)
    {
        Pages(op.Pages);
        PageSize(op.Size);
    }

    internal static void WatermarkText(AddWatermarkTextOp op)
    {
        OptionalPages(op.Pages);
        Require(op.Text.Length > 0, "text is required");
        Stamp(op.Opacity, op.Layer);
        Color(op.Color);
        Require(op.Size > 0, "size must be positive");
    }

    internal static void WatermarkImage(AddWatermarkImageOp op)
    {
        OptionalPages(op.Pages);
        Require(op.Path.Length > 0, "path is required");
        Stamp(op.Opacity, op.Layer);
        Require(op.Scale > 0, "scale must be positive");
    }

    internal static void PageNumbers(AddPageNumbersOp op)
    {
        OptionalPages(op.Pages);
        Require(op.Start >= 0, "start must not be negative");
        Position(op.Position);
    }

    internal static void HeaderText(AddHeaderTextOp op) => MarginText(op.Pages, op.Text, op.Position);

    internal static void FooterText(AddFooterTextOp op) => MarginText(op.Pages, op.Text, op.Position);

    internal static void StampImage(AddStampImageOp op)
    {
        Require(op.Page > 0, "page is 1-based");
        Require(op.Path.Length > 0, "path is required");
        Rect(op.Rect);
    }

    internal static void Link(AddLinkOp op)
    {
        Require(op.Page > 0, "page is 1-based");
        Rect(op.Rect);
        Require(
            Uri.TryCreate(op.Url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https" or "mailto",
            "url must use http, https or mailto");
    }

    internal static void RedactText(RedactTextOp op)
    {
        Require(op.Pattern.Length > 0, "pattern is required");
        OptionalPages(op.Pages);
        Color(op.FillColor);
        if (op.Regex)
        {
            try
            {
                _ = SafeRegex.Create(op.Pattern, caseSensitive: true);
            }
            catch (ArgumentException exception)
            {
                throw new OperationInvalidException($"invalid regex: {exception.Message}");
            }
        }
    }

    internal static void RedactArea(RedactAreaOp op)
    {
        Require(op.Page > 0, "page is 1-based");
        Rect(op.Rect);
        Color(op.FillColor);
    }

    internal static void Bookmark(AddBookmarkOp op)
    {
        Require(op.Title.Length > 0, "title is required");
        Require(op.Page > 0, "page is 1-based");
    }

    internal static void DeleteBookmarks(DeleteBookmarksOp op) =>
        Require(op.All != (op.Path is not null), "choose exactly one of all or path");

    internal static void Attachment(AddAttachmentOp op) => Require(op.Path.Length > 0, "path is required");

    internal static void RemoveAttachment(RemoveAttachmentOp op) => Require(op.Name.Length > 0, "name is required");

    internal static void PageLabels(SetPageLabelsOp op)
    {
        Require(op.Ranges.Count > 0, "ranges must not be empty");
        Require(
            op.Ranges.All(static range => range.StartPage > 0 && range.StartingValue >= 0
                && range.Style is "arabic" or "roman-upper" or "roman-lower" or "letters-upper" or "letters-lower" or "none"),
            "page-label values are invalid");
        Require(
            op.Ranges.Select(static range => range.StartPage).Distinct().Count() == op.Ranges.Count,
            "page-label start pages must be unique");
    }

    internal static void FormField(SetFormFieldOp op) => Require(op.Name.Length > 0, "name is required");

    internal static void FlattenForms(FlattenFormsOp op) =>
        Require(op.All != (op.Fields is { Count: > 0 }), "choose all or a non-empty fields list");

    internal static void Encrypt(EncryptPdfOp op) =>
        Require(op.OwnerPasswordEnv.Length > 0, "ownerPasswordEnv is required");

    internal static void Optimize(OptimizePdfOp op)
    {
        Require(op.DownsampleImagesDpi is null or >= 36 and <= 1200, "downsampleImagesDpi must be 36-1200");
        Require(op.ImageQuality is null or >= 1 and <= 100, "imageQuality must be 1-100");
    }

    private static void MarginText(string? pages, string text, string position)
    {
        OptionalPages(pages);
        Require(text.Length > 0, "text is required");
        Position(position);
    }

    private static void Pages(string value) => _ = PageRange.Parse(value);

    private static void OptionalPages(string? value)
    {
        if (value is not null)
        {
            Pages(value);
        }
    }

    private static void PageSize(string value) =>
        Require(value.ToUpperInvariant() is "A3" or "A4" or "LETTER" or "LEGAL", "unknown page size");

    private static void Rect(PdfRectInput value) =>
        Require(value.X >= 0 && value.Y >= 0 && value.Width > 0 && value.Height > 0,
            "rectangle coordinates must be non-negative and its size positive");

    private static void Color(string value) =>
        Require(
            value.Length == 7 && value[0] == '#'
            && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
            "color must use #RRGGBB");

    private static void Stamp(double opacity, string layer)
    {
        Require(opacity is >= 0 and <= 1, "opacity must be 0-1");
        Require(layer is "over" or "under", "layer must be over or under");
    }

    private static void Position(string value) =>
        Require(value is "top-left" or "top-center" or "top-right" or "bottom-left" or "bottom-center" or "bottom-right",
            "unknown position");
}
