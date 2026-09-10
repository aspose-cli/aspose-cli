using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Slides.Contracts.Serialization;

/// <summary>Strict discriminator converter for the Slides operation vocabulary.</summary>
internal sealed class SlidesOpJsonConverter : JsonConverter<SlidesOp>
{
    public override SlidesOp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("op", out JsonElement discriminator))
        {
            throw new JsonException($"Every Slides op needs an 'op' field. Valid ops: {string.Join(", ", SlidesOps.Names)}");
        }

        string? name = discriminator.GetString();
        if (name is null || !SlidesOps.Registry.TryGetValue(name, out Type? type))
        {
            throw new JsonException($"Unknown Slides op '{name}'. Valid ops: {string.Join(", ", SlidesOps.Names)}");
        }

        StrictJsonObjectValidator.Validate(root, type, options, "$", allowDiscriminator: true);
        SlidesOp value = (SlidesOp)(root.Deserialize(type, options)
            ?? throw new JsonException($"Op '{name}' deserialized to null."));
        return ApplyWireDefaults(value, root);
    }

    public override void Write(Utf8JsonWriter writer, SlidesOp value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("op", value.OpName);
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (!property.NameEquals("opName"))
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static SlidesOp ApplyWireDefaults(SlidesOp value, JsonElement root) => value switch
    {
        AppendPresentationOp op when Missing(root, "masterPolicy") => op with { MasterPolicy = "keep-source" },
        SlidesReplaceTextOp op when Missing(root, "scope") => op with { Scope = "all" },
        SetShapeStyleOp op when Missing(root, "style") => op with { Style = new SlidesShapeStyleInput() },
        SetSlideSizeOp op when Missing(root, "scaleContent") => op with { ScaleContent = true },
        _ => value,
    };

    private static bool Missing(JsonElement root, string name) => !root.TryGetProperty(name, out _);
}
