using System.Text.Json;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts.Serialization;

/// <summary>Connects the Pdf operation vocabulary to the shared wire protocol.</summary>
internal sealed class PdfOpJsonConverter()
    : OperationJsonConverter<PdfOp>(PdfOps.Registry, static operation => operation.OpName)
{
    protected override PdfOp ApplyDefaults(PdfOp value, JsonElement root) => value switch
    {
        InsertBlankPageOp op when Missing(root, "size") => op with { Size = "A4" },
        CropPagesOp op when Missing(root, "box") => op with { Box = "crop" },
        AddWatermarkTextOp op => op with
        {
            Size = Missing(root, "size") ? 48 : op.Size,
            Color = Missing(root, "color") ? "#808080" : op.Color,
            Opacity = Missing(root, "opacity") ? 0.35 : op.Opacity,
            Rotation = Missing(root, "rotation") ? 45 : op.Rotation,
            Layer = Missing(root, "layer") ? "over" : op.Layer,
        },
        AddWatermarkImageOp op => op with
        {
            Opacity = Missing(root, "opacity") ? 0.35 : op.Opacity,
            Scale = Missing(root, "scale") ? 0.5 : op.Scale,
            Layer = Missing(root, "layer") ? "over" : op.Layer,
        },
        AddPageNumbersOp op => op with
        {
            Format = Missing(root, "format") ? "Page {n} of {N}" : op.Format,
            Position = Missing(root, "position") ? "bottom-center" : op.Position,
            Start = Missing(root, "start") ? 1 : op.Start,
        },
        AddHeaderTextOp op when Missing(root, "position") => op with { Position = "top-center" },
        AddFooterTextOp op when Missing(root, "position") => op with { Position = "bottom-center" },
        RedactTextOp op when Missing(root, "fillColor") => op with { FillColor = "#000000" },
        RedactAreaOp op when Missing(root, "fillColor") => op with { FillColor = "#000000" },
        RemoveMetadataOp op => op with
        {
            Xmp = Missing(root, "xmp") || op.Xmp,
            DocumentInfo = Missing(root, "documentInfo") || op.DocumentInfo,
        },
        FlattenFormsOp op when Missing(root, "all") => op with { All = true },
        SetPageLabelsOp op => op with { Ranges = PageLabelDefaults(op.Ranges, root) },
        EncryptPdfOp op when Missing(root, "permissions") => op with { Permissions = new PdfPermissionsInput() },
        OptimizePdfOp op => op with
        {
            RemoveUnusedObjects = Missing(root, "removeUnusedObjects") || op.RemoveUnusedObjects,
            CompressStreams = Missing(root, "compressStreams") || op.CompressStreams,
        },
        _ => value,
    };

    private static bool Missing(JsonElement root, string name) => !root.TryGetProperty(name, out _);

    private static IReadOnlyList<PdfPageLabelRange> PageLabelDefaults(
        IReadOnlyList<PdfPageLabelRange> ranges,
        JsonElement root)
    {
        JsonElement.ArrayEnumerator elements = root.GetProperty("ranges").EnumerateArray();
        var normalized = new List<PdfPageLabelRange>(ranges.Count);
        int index = 0;
        foreach (JsonElement element in elements)
        {
            PdfPageLabelRange range = ranges[index++];
            normalized.Add(range with
            {
                Style = Missing(element, "style") ? "arabic" : range.Style,
                StartingValue = Missing(element, "startingValue") ? 1 : range.StartingValue,
            });
        }

        return normalized;
    }

}
