using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Generated;

namespace Aspose.Cli.Product.Cells.Contracts.Serialization;

/// <summary>
/// Polymorphic (de)serialization of ops, discriminated by the <c>op</c>
/// field. Hand-written instead of the built-in polymorphism support so the
/// discriminator may appear anywhere in the object (agents do not reliably
/// put it first) and unknown names fail with the full list of valid ops.
/// </summary>
internal sealed class OpJsonConverter : JsonConverter<Op>
{
    private static readonly Lazy<JsonSerializerOptions> StrictOptions = new(() =>
    {
        var strict = new JsonSerializerOptions(ProductJsonContext.Definition.LocalOptions)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        strict.MakeReadOnly();
        return strict;
    });

    /// <summary>
    /// Applies only to the abstract base: concrete op types keep their
    /// generated metadata, which this converter delegates to.
    /// </summary>
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Op);

    public override Op Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("op", out JsonElement discriminator))
        {
            throw new JsonException(
                $"Every op must be an object with an \"op\" field. Valid ops: {string.Join(", ", OpNames.All)}");
        }

        string? name = discriminator.GetString();
        if (name is null || !CellsOps.Registry.TryGetValue(name, out Type? opType))
        {
            throw new JsonException(
                $"Unknown op '{name}'. Valid ops: {string.Join(", ", OpNames.All)}");
        }

        using var payload = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payload))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Name != "op")
                {
                    property.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        return (Op)(JsonSerializer.Deserialize(
                payload.ToArray(),
                opType,
                StrictOptions.Value)
            ?? throw new JsonException($"Op '{name}' deserialized to null."));
    }

    public override void Write(Utf8JsonWriter writer, Op value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("op", value.OpName);

        // Delegate the payload to the runtime type's metadata and merge its
        // properties after the discriminator.
        using JsonDocument document = JsonDocument.Parse(
            JsonSerializer.Serialize(value, value.GetType(), options));
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
