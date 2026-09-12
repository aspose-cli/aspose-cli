using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts.Serialization;

/// <summary>Strict discriminator converter for the PDF op vocabulary.</summary>
internal sealed class PdfOpJsonConverter : JsonConverter<PdfOp>
{
    public override PdfOp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("op", out JsonElement discriminator))
        {
            throw new JsonException($"Every PDF op needs an 'op' field. Valid ops: {string.Join(", ", PdfOps.Names)}");
        }

        string? name = discriminator.GetString();
        if (name is null || !PdfOps.Registry.TryGetValue(name, out Type? type))
        {
            throw new JsonException($"Unknown PDF op '{name}'. Valid ops: {string.Join(", ", PdfOps.Names)}");
        }

        StrictJsonObjectValidator.Validate(root, type, options, "$", allowDiscriminator: true);
        PdfOp value = (PdfOp)(root.Deserialize(type, options) ?? throw new JsonException($"Op '{name}' deserialized to null."));
        return ApplyWireDefaults(value, root);
    }

    public override void Write(Utf8JsonWriter writer, PdfOp value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("op", value.OpName);
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("opName"))
            {
                continue;
            }

            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static PdfOp ApplyWireDefaults(PdfOp value, JsonElement root) => value switch
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
