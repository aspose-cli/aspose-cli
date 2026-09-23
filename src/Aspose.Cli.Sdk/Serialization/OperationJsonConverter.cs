using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// Owns the operation discriminator protocol; each product supplies its vocabulary and defaults.
/// </summary>
public abstract class OperationJsonConverter<TOperation> : JsonConverter<TOperation>
    where TOperation : BoundedOperation
{
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> StrictOptions = new();
    private readonly IReadOnlyDictionary<string, Type> operations;
    private readonly Func<TOperation, string> operationName;

    /// <summary>Connects a product's operation catalog to the shared wire protocol.</summary>
    protected OperationJsonConverter(OperationCatalog<TOperation> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        operations = catalog.Registry;
        operationName = catalog.NameOf;
    }

    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override TOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("op", out JsonElement discriminator)
            || discriminator.ValueKind != JsonValueKind.String)
        {
            throw new JsonException(
                $"Every op must be an object with a string 'op' field. Valid ops: {string.Join(", ", operations.Keys)}");
        }

        string name = discriminator.GetString()!;
        if (!operations.TryGetValue(name, out Type? type))
        {
            throw new JsonException($"Unknown op '{name}'. Valid ops: {string.Join(", ", operations.Keys)}");
        }

        BoundedJsonValidation.ValidateNoDuplicateProperties(root, static reason => new JsonException(reason));
        using var payload = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payload))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.NameEquals("opName"))
                {
                    throw new JsonException("Unknown field 'opName'. Use the 'op' discriminator.");
                }
                if (!property.NameEquals("op"))
                {
                    property.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        JsonSerializerOptions strict = StrictOptions.GetValue(options, static source =>
        {
            var value = new JsonSerializerOptions(source)
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            };
            value.MakeReadOnly();
            return value;
        });
        var operation = (TOperation)(JsonSerializer.Deserialize(payload.ToArray(), type, strict)
            ?? throw new JsonException($"Op '{name}' deserialized to null."));
        return ApplyDefaults(operation, root);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TOperation value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("op", operationName(value));
        JsonElement payload = JsonSerializer.SerializeToElement(value, value.GetType(), options);
        foreach (JsonProperty property in payload.EnumerateObject())
        {
            if (!property.NameEquals("opName"))
            {
                property.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    /// <summary>Applies product-owned defaults while preserving explicitly supplied values.</summary>
    protected virtual TOperation ApplyDefaults(TOperation operation, JsonElement payload) => operation;
}
