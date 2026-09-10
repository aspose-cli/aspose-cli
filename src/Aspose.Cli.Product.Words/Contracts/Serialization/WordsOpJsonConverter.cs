using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Product.Words.Contracts.Serialization;

/// <summary>Discriminator converter for the Words op vocabulary.</summary>
internal sealed class WordsOpJsonConverter : JsonConverter<WordsOp>
{
    public override WordsOp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("op", out JsonElement discriminator))
        {
            throw new JsonException($"Every Words op needs an 'op' field. Valid ops: {string.Join(", ", WordsOps.Names)}");
        }

        string? name = discriminator.GetString();
        if (name is null || !WordsOps.Registry.TryGetValue(name, out Type? type))
        {
            throw new JsonException($"Unknown Words op '{name}'. Valid ops: {string.Join(", ", WordsOps.Names)}");
        }

        StrictJsonObjectValidator.Validate(root, type, options, "$", allowDiscriminator: true);
        return (WordsOp)(root.Deserialize(type, options) ?? throw new JsonException($"Op '{name}' deserialized to null."));
    }

    public override void Write(Utf8JsonWriter writer, WordsOp value, JsonSerializerOptions options)
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

}
