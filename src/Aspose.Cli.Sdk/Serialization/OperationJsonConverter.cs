using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// The wire protocol of an operation vocabulary; declare it on the vocabulary's base record
/// with <c>[JsonConverter(typeof(OperationJsonConverter&lt;TOp&gt;))]</c>. It reads the
/// <c>op</c> discriminator, rejects unknown, null and duplicated members in wire terms (an
/// unknown member with the fields its object accepts and the closest one), and
/// writes every omitted member that has a default before the payload is read, so the schema's
/// <c>default</c> is exactly the value applied.
/// </summary>
public sealed class OperationJsonConverter<TOp> : JsonConverter<TOp>
    where TOp : BoundedOperation, IOperationVocabulary<TOp>
{
    private readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _strictOptions = new();

    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override TOp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("op", out JsonElement discriminator)
            || discriminator.ValueKind != JsonValueKind.String)
        {
            throw new JsonException(
                $"every op must be an object with a string 'op' field; valid ops: {ValidOperations}");
        }

        string name = discriminator.GetString()!;
        if (!TOp.Catalog.TryGetOperation(name, out OperationRecord? record))
        {
            throw new JsonException($"unknown op '{name}'; valid ops: {ValidOperations}");
        }

        // The payload below omits the discriminator, so only its duplicates need a check here;
        // the serializer rejects every other duplicate and the diagnostics name it.
        if (root.EnumerateObject().Count(static property => property.NameEquals("op")) > 1)
        {
            throw new JsonException("op is duplicated");
        }

        using var payload = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payload))
        {
            WriteMembers(writer, root, record, string.Empty, isOperation: true);
        }

        JsonSerializerOptions strict = Strict(options);
        byte[] fields = payload.ToArray();
        try
        {
            return (TOp)JsonSerializer.Deserialize(fields, record.Type, strict)!;
        }
        catch (JsonException rejection)
        {
            // The serializer's own text names CLR types; restate the failure in wire terms.
            // The explanation is a fresh exception without a path, so the enclosing read
            // records the op's position, and it keeps the fields an object accepts.
            using JsonDocument rejected = JsonDocument.Parse(fields);
            throw JsonContractDiagnostics.Explain(rejected.RootElement, record.Type, strict, rejection.Path);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TOp value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        writer.WriteString("op", TOp.Catalog.NameOf(value));
        JsonElement payload = JsonSerializer.SerializeToElement(value, value.GetType(), Strict(options));
        foreach (JsonProperty property in payload.EnumerateObject())
        {
            if (!property.NameEquals("opName"))
            {
                property.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    private static string ValidOperations => string.Join(", ", TOp.Catalog.Names);

    /// <summary>
    /// The outer options with unknown members disallowed and the vocabulary's own operation
    /// metadata in front of the outer resolver.
    /// </summary>
    private JsonSerializerOptions Strict(JsonSerializerOptions options) =>
        _strictOptions.GetValue(options, source =>
        {
            var value = new JsonSerializerOptions(source)
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                TypeInfoResolver = JsonTypeInfoResolver.Combine(TOp.Catalog.Contracts, source.TypeInfoResolver),
            };
            value.MakeReadOnly();
            return value;
        });

    /// <summary>
    /// Copies an object's members, dropping the discriminator of an operation, and appends
    /// every omitted member that has a default. Values of nested records, arrays and maps are
    /// completed the same way; so is an object default such as <c>{}</c>.
    /// </summary>
    private static void WriteMembers(Utf8JsonWriter writer, JsonElement value, OperationRecord record, string path, bool isOperation)
    {
        writer.WriteStartObject();
        foreach (JsonProperty member in value.EnumerateObject())
        {
            if (isOperation && member.NameEquals("opName"))
            {
                throw new JsonException("unknown field 'opName'; use the 'op' discriminator");
            }
            if (isOperation && member.NameEquals("op"))
            {
                continue;
            }

            OperationProperty? property = record.Properties.FirstOrDefault(property => member.NameEquals(property.Name));
            if (member.Value.ValueKind == JsonValueKind.Null
                && ((property is not null && !(property.Required && property.Value.Nullable)) || (isOperation && member.NameEquals("id"))))
            {
                // An optional member is omitted, never null, as the schema states; only a
                // required member whose type admits null may be null.
                throw new JsonException($"{Join(path, member.Name)} must not be null");
            }

            if (property is null)
            {
                if (!(isOperation && member.NameEquals("id")))
                {
                    throw UnknownField(value, record, path, member.Name, isOperation);
                }

                member.WriteTo(writer);
                continue;
            }

            writer.WritePropertyName(member.Name);
            WriteValue(writer, member.Value, property.Value, Join(path, property.Name));
        }

        foreach (OperationProperty property in record.Properties)
        {
            if (property.Required && !value.TryGetProperty(property.Name, out _))
            {
                throw JsonContractDiagnostics.MissingField(
                    Join(path, property.Name),
                    property.Constraints.OfType<AllowedValuesAttribute>().FirstOrDefault()?.Listed);
            }

            if (property.Default is not null && !value.TryGetProperty(property.Name, out _))
            {
                using JsonDocument fallback = JsonDocument.Parse(property.Default);
                writer.WritePropertyName(property.Name);
                WriteValue(writer, fallback.RootElement, property.Value, Join(path, property.Name));
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>Rejects a member the record does not declare; an operation also accepts <c>op</c> and <c>id</c>.</summary>
    private static AllowedFieldsException UnknownField(
        JsonElement value, OperationRecord record, string path, string name, bool isOperation)
    {
        string[] allowed = [.. isOperation ? ["op", "id"] : Array.Empty<string>(),
            .. record.Properties.Select(static property => property.Name)];
        string[] missing = [.. record.Properties
            .Where(property => property.Required && !value.TryGetProperty(property.Name, out _))
            .Select(static property => property.Name)];
        return AllowedFieldsException.UnknownField(path, name, isOperation ? record.Name : path, allowed, missing);
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value, OperationValue shape, string path)
    {
        switch (shape.Kind, value.ValueKind)
        {
            case (OperationValueKind.Record, JsonValueKind.Object):
                WriteMembers(writer, value, shape.Record!, path, isOperation: false);
                break;
            case (OperationValueKind.Array, JsonValueKind.Array):
                writer.WriteStartArray();
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteItem(writer, item, shape.Items!, $"{path}[{index++}]");
                }

                writer.WriteEndArray();
                break;
            case (OperationValueKind.Map, JsonValueKind.Object):
                writer.WriteStartObject();
                foreach (JsonProperty entry in value.EnumerateObject())
                {
                    writer.WritePropertyName(entry.Name);
                    WriteItem(writer, entry.Value, shape.Items!, Join(path, entry.Name));
                }

                writer.WriteEndObject();
                break;
            default:
                // Scalars and values of the wrong kind pass unchanged; the serializer then
                // accepts or rejects them.
                value.WriteTo(writer);
                break;
        }
    }

    /// <summary>Writes an array item or map value, which may be null only when its type admits null.</summary>
    private static void WriteItem(Utf8JsonWriter writer, JsonElement value, OperationValue shape, string path)
    {
        if (value.ValueKind == JsonValueKind.Null && !shape.Nullable && shape.Kind != OperationValueKind.Any)
        {
            throw new JsonException($"{path} must not be null");
        }

        WriteValue(writer, value, shape, path);
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
}
