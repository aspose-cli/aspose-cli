using System.Text.Json;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Contracts.Serialization;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Operations;

/// <summary>Parses and validates PDF operations before a document is opened.</summary>
internal static class PdfOpsParser
{
    private const int MaximumOperations = 256;

    public static PdfOpsBatch Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Invalid("the ops document is empty");
        }

        PdfOpsBatch batch;
        try
        {
            batch = ProductJsonContext.Definition.Deserialize<PdfOpsBatch>(json);
        }
        catch (JsonException exception)
        {
            throw Invalid(exception.Message);
        }

        return Prepare(batch);
    }

    /// <summary>Validates a composed batch and assigns stable missing IDs.</summary>
    internal static PdfOpsBatch Prepare(PdfOpsBatch batch)
    {
        BoundedOperationValidation.ValidateEnvelope(
            batch,
            PdfSchemaIds.Ops,
            Invalid);

        if (batch.Ops.Count is < 1 or > MaximumOperations)
        {
            throw Invalid($"the ops array must contain 1-{MaximumOperations} operations");
        }

        IReadOnlyList<PdfOp> identified = BoundedOperationIds.Assign(
            batch.Ops,
            static operation => operation.Id,
            static (operation, id) => operation with { Id = id },
            Invalid);
        for (int index = 0; index < identified.Count; index++)
        {
            Validate(identified[index], index);
        }

        return batch with { SchemaVersion = 2, Ops = identified };
    }

    private static void Validate(PdfOp op, int index)
    {
        if (op is RotatePagesOp or DeletePagesOp or MovePagesOp or InsertPagesFromOp
            or InsertBlankPageOp or CropPagesOp or SetPageSizeOp)
        {
            ValidatePageOperation(op, index);
        }
        else if (op is AddWatermarkTextOp or AddWatermarkImageOp or AddPageNumbersOp
                 or AddHeaderTextOp or AddFooterTextOp or AddStampImageOp or AddLinkOp
                 or RedactTextOp or RedactAreaOp)
        {
            ValidateContentOperation(op, index);
        }
        else
        {
            ValidateDocumentOperation(op, index);
        }
    }

    private static void ValidatePageOperation(PdfOp op, int index)
    {
        switch (op)
        {
            case RotatePagesOp value:
                Pages(value.Pages);
                Require(value.Angle is 90 or 180 or 270, index, op, "angle must be 90, 180 or 270");
                break;
            case DeletePagesOp value: Pages(value.Pages); break;
            case MovePagesOp value: Pages(value.Pages); Require(value.To > 0, index, op, "to is 1-based"); break;
            case InsertPagesFromOp value:
                Require(value.Path.Length > 0, index, op, "path is required");
                if (value.Pages is not null)
                {
                    Pages(value.Pages);
                }

                Require(value.At > 0, index, op, "at is 1-based");
                break;
            case InsertBlankPageOp value: Require(value.At > 0, index, op, "at is 1-based"); PageSize(value.Size, index, op); break;
            case CropPagesOp value: Pages(value.Pages); Require(value.Box is "media" or "crop", index, op, "box must be media or crop"); Rect(value.Rect, index, op); break;
            case SetPageSizeOp value: Pages(value.Pages); PageSize(value.Size, index, op); break;
        }
    }

    private static void ValidateContentOperation(PdfOp op, int index)
    {
        switch (op)
        {
            case AddWatermarkTextOp value: OptionalPages(value.Pages); Require(value.Text.Length > 0, index, op, "text is required"); Stamp(value.Opacity, value.Layer, index, op); Color(value.Color, index, op); Require(value.Size > 0, index, op, "size must be positive"); break;
            case AddWatermarkImageOp value: OptionalPages(value.Pages); Require(value.Path.Length > 0, index, op, "path is required"); Stamp(value.Opacity, value.Layer, index, op); Require(value.Scale > 0, index, op, "scale must be positive"); break;
            case AddPageNumbersOp value: OptionalPages(value.Pages); Require(value.Start >= 0, index, op, "start must not be negative"); Position(value.Position, index, op); break;
            case AddHeaderTextOp value: OptionalPages(value.Pages); Require(value.Text.Length > 0, index, op, "text is required"); Position(value.Position, index, op); break;
            case AddFooterTextOp value: OptionalPages(value.Pages); Require(value.Text.Length > 0, index, op, "text is required"); Position(value.Position, index, op); break;
            case AddStampImageOp value: Require(value.Page > 0, index, op, "page is 1-based"); Require(value.Path.Length > 0, index, op, "path is required"); Rect(value.Rect, index, op); break;
            case AddLinkOp value:
                Require(value.Page > 0, index, op, "page is 1-based");
                Rect(value.Rect, index, op);
                Require(
                    Uri.TryCreate(value.Url, UriKind.Absolute, out Uri? uri)
                    && uri.Scheme is "http" or "https" or "mailto",
                    index,
                    op,
                    "url must use http, https or mailto");
                break;
            case RedactTextOp value:
                Require(value.Pattern.Length > 0, index, op, "pattern is required");
                OptionalPages(value.Pages);
                Color(value.FillColor, index, op);
                if (value.Regex)
                {
                    try
                    {
                        _ = SafeRegex.Create(value.Pattern, caseSensitive: true);
                    }
                    catch (ArgumentException exception)
                    {
                        throw Invalid($"op {index} ({op.OpName}): invalid regex: {exception.Message}");
                    }
                }

                break;
            case RedactAreaOp value: Require(value.Page > 0, index, op, "page is 1-based"); Rect(value.Rect, index, op); Color(value.FillColor, index, op); break;
        }
    }

    private static void ValidateDocumentOperation(PdfOp op, int index)
    {
        switch (op)
        {
            case AddBookmarkOp value: Require(value.Title.Length > 0, index, op, "title is required"); Require(value.Page > 0, index, op, "page is 1-based"); break;
            case DeleteBookmarksOp value: Require(value.All != (value.Path is not null), index, op, "choose exactly one of all or path"); break;
            case AddAttachmentOp value: Require(value.Path.Length > 0, index, op, "path is required"); break;
            case RemoveAttachmentOp value: Require(value.Name.Length > 0, index, op, "name is required"); break;
            case SetPageLabelsOp value:
                Require(value.Ranges.Count > 0, index, op, "ranges must not be empty");
                Require(
                    value.Ranges.All(static range => range.StartPage > 0 && range.StartingValue >= 0
                        && range.Style is "arabic" or "roman-upper" or "roman-lower" or "letters-upper" or "letters-lower" or "none"),
                    index,
                    op,
                    "page-label values are invalid");
                Require(
                    value.Ranges.Select(static range => range.StartPage).Distinct().Count() == value.Ranges.Count,
                    index,
                    op,
                    "page-label start pages must be unique");
                break;
            case SetFormFieldOp value: Require(value.Name.Length > 0, index, op, "name is required"); break;
            case FlattenFormsOp value: Require(value.All != (value.Fields is { Count: > 0 }), index, op, "choose all or a non-empty fields list"); break;
            case EncryptPdfOp value: Require(value.OwnerPasswordEnv.Length > 0, index, op, "ownerPasswordEnv is required"); break;
            case OptimizePdfOp value:
                Require(value.DownsampleImagesDpi is null or >= 36 and <= 1200, index, op, "downsampleImagesDpi must be 36-1200");
                Require(value.ImageQuality is null or >= 1 and <= 100, index, op, "imageQuality must be 1-100");
                break;
        }
    }

    private static void Pages(string value) => _ = PageRange.Parse(value);
    private static void OptionalPages(string? value)
    {
        if (value is not null)
        {
            Pages(value);
        }
    }
    private static void PageSize(string value, int index, PdfOp op) =>
        Require(value.ToUpperInvariant() is "A3" or "A4" or "LETTER" or "LEGAL", index, op, "unknown page size");
    private static void Rect(PdfRectInput value, int index, PdfOp op) =>
        Require(value.X >= 0 && value.Y >= 0 && value.Width > 0 && value.Height > 0, index, op, "rectangle coordinates must be non-negative and its size positive");
    private static void Color(string value, int index, PdfOp op) =>
        Require(
            value.Length == 7 && value[0] == '#'
            && int.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _),
            index,
            op,
            "color must use #RRGGBB");
    private static void Stamp(double opacity, string layer, int index, PdfOp op)
    {
        Require(opacity is >= 0 and <= 1, index, op, "opacity must be 0-1");
        Require(layer is "over" or "under", index, op, "layer must be over or under");
    }
    private static void Position(string value, int index, PdfOp op) =>
        Require(value is "top-left" or "top-center" or "top-right" or "bottom-left" or "bottom-center" or "bottom-right", index, op, "unknown position");
    private static void Require(bool condition, int index, PdfOp op, string reason)
    {
        if (!condition)
        {
            throw Invalid($"op {index} ({op.OpName}): {reason}");
        }
    }
    private static CliException Invalid(string reason) => new(
        ErrorCodes.OpsInvalid,
        $"Invalid PDF ops batch: {reason}.",
        hint: "Fix the named op using 'aspose-cli schema v2/pdf/ops'.");
}
