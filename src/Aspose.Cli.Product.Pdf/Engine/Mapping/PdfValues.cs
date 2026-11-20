using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>The value kinds of the PDF table that the SDK has no kind for.</summary>
internal static class PdfValues
{
    /// <summary>The engine's <see cref="Rectangle"/> as the engine writes it, Rectangle.ToString(), and reads it, Rectangle.Parse; null where the engine gives none.</summary>
    internal static ValueKind<Rectangle?> Rectangle { get; } = ValueKinds.Create<Rectangle?>(
        "rectangle",
        "The engine's Rectangle as Rectangle.ToString() writes it and Rectangle.Parse reads it, LLX,LLY,URX,URY: the lower-left and upper-right corners in points, in the page's user space, whose origin is the page's lower-left corner before any rotation; null where the engine gives none.",
        static () => new JsonObject
        {
            ["type"] = new JsonArray("string", "null"),
            ["pattern"] = "^[^,]+,[^,]+,[^,]+,[^,]+$",
            ["examples"] = new JsonArray("150,700,258,713.2"),
        },
        static value => value is null ? null : JsonValue.Create(value.ToString()),
        static value => value.ValueKind == JsonValueKind.String && ParseRectangle(value.GetString()!) is { } rect
            ? rect
            : throw ValueKinds.Expected("a rectangle LLX,LLY,URX,URY such as 150,700,258,713.2", value));

    /// <summary>The rectangle Rectangle.Parse reads from four numbers joined by commas, or null for any other text.</summary>
    internal static Rectangle? ParseRectangle(string text)
    {
        string[] parts = text.Split(',');
        return parts.Length == 4 && parts.All(static part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number))
            ? Aspose.Pdf.Rectangle.Parse(text)
            : null;
    }

    /// <summary>The engine's <see cref="Aspose.Pdf.Color"/> as #RRGGBB, through Color.ToRgb and Color.FromRgb; null where the engine gives none.</summary>
    internal static ValueKind<PdfColor?> Color { get; } = ValueKinds.Create<PdfColor?>(
        "pdf-color",
        "A color as #RRGGBB, the engine's Color.ToRgb(), set with Color.FromRgb; null where the engine gives none.",
        static () => new JsonObject { ["type"] = new JsonArray("string", "null"), ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
        static value => value is null ? null : ValueKinds.Color.Encode(value.ToRgb()),
        static value => Aspose.Pdf.Color.FromRgb(ValueKinds.Color.Decode(value)));

    /// <summary>A date and time as the engine gives it, written in the round-trip form.</summary>
    internal static ValueKind<DateTime?> DateTime { get; } = ValueKinds.Create<DateTime?>(
        "date-time",
        "A date and time as the engine gives it, written yyyy-MM-ddTHH:mm:ss.fffffff; null where the engine gives none.",
        static () => new JsonObject { ["type"] = new JsonArray("string", "null") },
        static value => value is { } present ? JsonValue.Create(present.ToString("o", CultureInfo.InvariantCulture)) : null,
        static value => value.ValueKind == JsonValueKind.String
            && System.DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out System.DateTime parsed)
            ? parsed
            : throw ValueKinds.Expected("a date and time such as 2026-05-15T09:30:00", value));

    /// <summary>The options of a choice field, each with the engine's Option.Name and Option.Value.</summary>
    internal static ValueKind<OptionCollection> Options { get; } = ValueKinds.Create<OptionCollection>(
        "options",
        "The field's options in the engine's order, each {\"name\": Option.Name, \"value\": Option.Value}.",
        static () => new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["name"] = new JsonObject { ["type"] = new JsonArray("string", "null") }, ["value"] = new JsonObject { ["type"] = new JsonArray("string", "null") } },
            },
        },
        static value => new JsonArray([.. value.Cast<Option>().Select(static option => (JsonNode)new JsonObject { ["name"] = option.Name, ["value"] = option.Value })]),
        static value => throw ValueKinds.Expected("a value this read-only row takes", value));
}
