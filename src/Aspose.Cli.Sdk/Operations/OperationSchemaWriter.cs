using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The schema a generated vocabulary publishes and its per-operation views, each written on
/// first use and then kept.
/// </summary>
internal sealed class GeneratedOperationSchema
{
    private readonly Lazy<string> _document;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _operations;
    private readonly Lazy<string> _fingerprint;

    public GeneratedOperationSchema(Func<string> write, IReadOnlyList<string> operations)
    {
        Names = operations;
        _document = new Lazy<string>(write);
        _operations = new Lazy<IReadOnlyDictionary<string, string>>(() => OperationSchemaWriter.Views(Document, operations));
        _fingerprint = new Lazy<string>(() => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Document))));
    }

    /// <summary>The operations' wire names in published order.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The complete schema text.</summary>
    public string Document => _document.Value;

    /// <summary>Each operation's self-contained view, by wire name.</summary>
    public IReadOnlyDictionary<string, string> Operations => _operations.Value;

    /// <summary><c>sha256:</c> and the lowercase hex SHA-256 of the UTF-8 schema text.</summary>
    public string Fingerprint => _fingerprint.Value;
}

/// <summary>
/// Writes the JSON Schema of a generated operation vocabulary from its descriptors: the bounded
/// edit envelope, one <c>$defs</c> entry per operation, and shared entries for nested records
/// and value kinds. The text is deterministic: stable member order, two-space indentation,
/// <c>\n</c> line endings and a trailing newline.
/// </summary>
internal sealed class OperationSchemaWriter
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";
    private const string DefinitionPrefix = "#/$defs/";
    private const string IdDefinition = "id";

    /// <summary>How every bounded edit applies a document, stated after the vocabulary's own description.</summary>
    private const string BatchSemantics =
        "Operations apply in order. Without --best-effort the batch is atomic: if any operation fails, no file is written. "
        + "With --best-effort the operations that succeed are kept and saved, each one that fails is reported, and the command "
        + "exits 8 when any failed; an invalid document or a failure inside the document engine still writes nothing.";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SortedDictionary<string, JsonObject> _shared = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _records = new(StringComparer.Ordinal);

    private OperationSchemaWriter()
    {
    }

    /// <summary>Writes the schema of one vocabulary.</summary>
    /// <param name="schemaId">Canonical schema identifier.</param>
    /// <param name="maximumOperations">Largest accepted number of operations.</param>
    /// <param name="description">The vocabulary's description, or null.</param>
    /// <param name="operations">The operations in published order.</param>
    /// <exception cref="InvalidOperationException">The descriptors cannot be published consistently.</exception>
    public static string Write(
        string schemaId,
        int maximumOperations,
        string? description,
        IReadOnlyList<OperationRecord> operations)
    {
        var writer = new OperationSchemaWriter();
        var definitions = new JsonObject
        {
            [IdDefinition] = new JsonObject
            {
                ["description"] = "A correlation id echoed in the result; op-0001 style ids are assigned when omitted.",
                ["type"] = "string",
                ["pattern"] = "^[A-Za-z][A-Za-z0-9._-]{0,63}$",
            },
        };
        foreach (OperationRecord operation in operations)
        {
            definitions.Add(operation.Name, writer.Record(operation, isOperation: true));
        }

        foreach ((string name, JsonObject definition) in writer._shared)
        {
            if (definitions.ContainsKey(name))
            {
                throw new InvalidOperationException($"Schema definition '{name}' is claimed by an operation and a shared value.");
            }

            definitions.Add(name, definition);
        }

        JsonObject root = Envelope(schemaId, maximumOperations, description, operations.Select(static operation => operation.Name));
        root["$defs"] = definitions;
        return Serialize(root);
    }

    /// <summary>Each operation's view of a written schema: the document narrowed to that operation.</summary>
    public static IReadOnlyDictionary<string, string> Views(string document, IEnumerable<string> operations)
    {
        var root = (JsonObject)JsonNode.Parse(document)!;
        var views = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string operation in operations)
        {
            views.Add(operation, Serialize(View(root, operation)));
        }

        return views;
    }

    private static JsonObject Envelope(string schemaId, int maximumOperations, string? description, IEnumerable<string> operations)
    {
        var root = new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = schemaId,
            ["description"] = description is null ? BatchSemantics : $"{description} {BatchSemantics}",
        };
        root["type"] = "object";
        root["required"] = new JsonArray("ops");
        root["additionalProperties"] = false;
        root["properties"] = new JsonObject
        {
            ["schema"] = new JsonObject { ["description"] = "Optional; checked when present.", ["const"] = schemaId },
            ["schemaVersion"] = new JsonObject { ["const"] = 2 },
            ["ifMatch"] = new JsonObject
            {
                ["description"] = "The SHA-256 fingerprint the input must still have; the edit fails when it changed.",
                ["type"] = "string",
                ["pattern"] = "^[0-9A-Fa-f]{64}$",
            },
            ["ops"] = new JsonObject
            {
                ["description"] = "Operations applied in order.",
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = maximumOperations,
                ["items"] = new JsonObject
                {
                    ["oneOf"] = new JsonArray([.. operations.Select(static name => (JsonNode)Reference(name))]),
                },
            },
        };
        return root;
    }

    private JsonObject Record(OperationRecord record, bool isOperation)
    {
        var schema = new JsonObject();
        if (record.Description is not null)
        {
            schema["description"] = record.Description;
        }

        schema["type"] = "object";
        string[] required = [.. isOperation ? ["op"] : Array.Empty<string>(),
            .. record.Properties.Where(static property => property.Required).Select(static property => property.Name)];
        if (required.Length > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(static name => (JsonNode)name)]);
        }

        schema["additionalProperties"] = false;
        var properties = new JsonObject();
        if (isOperation)
        {
            properties["id"] = Reference(IdDefinition);
            properties["op"] = new JsonObject { ["const"] = record.Name };
        }

        foreach (OperationProperty property in record.Properties)
        {
            properties.Add(property.Name, Property(property, $"{record.Name}.{property.Name}"));
        }

        schema["properties"] = properties;
        if (record.Constraints.Count > 0)
        {
            // Each record rule is one allOf entry, so rules never compete for a keyword.
            var shape = new OperationValue { Kind = OperationValueKind.Record, Record = record };
            schema["allOf"] = new JsonArray([.. record.Constraints.Select(constraint =>
            {
                var rule = new JsonObject();
                constraint.Describe(rule, shape);
                return (JsonNode)rule;
            })]);
        }

        return schema;
    }

    private JsonObject Property(OperationProperty property, string path)
    {
        var schema = new JsonObject();
        if (property.Description is not null)
        {
            schema["description"] = property.Description;
        }

        Value(schema, property.Value, PlacedConstraint.Declared(property.Constraints), path);
        if (property.Default is not null)
        {
            schema["default"] = JsonNode.Parse(property.Default);
        }

        return schema;
    }

    private void Value(JsonObject schema, OperationValue value, IReadOnlyList<PlacedConstraint> constraints, string path)
    {
        (IReadOnlyList<ValueConstraintAttribute> here, IReadOnlyList<PlacedConstraint> items) =
            OperationContractValidator.Place(value, constraints, path);
        ValueConstraintAttribute[] shared = [.. here.Where(static constraint => constraint.Definition is not null)];
        if (shared.Length > 1 || (shared.Length == 1 && value.Kind == OperationValueKind.Record))
        {
            throw new InvalidOperationException($"'{path}' has more than one shared definition.");
        }

        if (value.Kind == OperationValueKind.Record)
        {
            schema["$ref"] = DefinitionPrefix + NestedRecord(value.Record!);
        }
        else if (shared.Length == 1)
        {
            schema["$ref"] = DefinitionPrefix + SharedValue(shared[0], value);
        }
        else if (TypeName(value.Kind) is { } type)
        {
            schema["type"] = type;
        }

        foreach (ValueConstraintAttribute constraint in here.Except(shared))
        {
            constraint.Describe(schema, value);
        }

        if (value.Kind is OperationValueKind.Array or OperationValueKind.Map)
        {
            var item = new JsonObject();
            Value(item, value.Items!, items, path + "[]");
            schema[value.Kind == OperationValueKind.Array ? "items" : "additionalProperties"] =
                value.Items!.Nullable ? AllowNull(item) : item;
        }
    }

    private string NestedRecord(OperationRecord record)
    {
        if (_records.TryGetValue(record.Name, out Type? owner))
        {
            return owner == record.Type
                ? record.Name
                : throw new InvalidOperationException(
                    $"Schema definition '{record.Name}' is claimed by {owner.Name} and {record.Type.Name}.");
        }

        _records.Add(record.Name, record.Type);
        _shared.Add(record.Name, Record(record, isOperation: false));
        return record.Name;
    }

    private string SharedValue(ValueConstraintAttribute constraint, OperationValue value)
    {
        string name = constraint.Definition!;
        var definition = new JsonObject();
        if (TypeName(value.Kind) is { } type)
        {
            definition["type"] = type;
        }

        constraint.Describe(definition, value);
        if (_records.ContainsKey(name)
            || (_shared.TryGetValue(name, out JsonObject? existing) && !JsonNode.DeepEquals(existing, definition)))
        {
            throw new InvalidOperationException($"Schema definition '{name}' has conflicting declarations.");
        }

        _shared[name] = definition;
        return name;
    }

    private static JsonObject AllowNull(JsonObject schema)
    {
        if (schema["type"] is JsonValue type)
        {
            schema["type"] = new JsonArray(type.GetValue<string>(), "null");
            if (schema["enum"] is JsonArray values)
            {
                values.Add(null);
            }

            return schema;
        }

        return schema.ContainsKey("$ref")
            ? new JsonObject { ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" }) }
            : schema;
    }

    private static string? TypeName(OperationValueKind kind) => kind switch
    {
        OperationValueKind.String => "string",
        OperationValueKind.Integer => "integer",
        OperationValueKind.Number => "number",
        OperationValueKind.Boolean => "boolean",
        OperationValueKind.Array => "array",
        OperationValueKind.Record or OperationValueKind.Map => "object",
        _ => null,
    };

    private static JsonObject Reference(string definition) => new() { ["$ref"] = DefinitionPrefix + definition };

    /// <summary>The document narrowed to one operation and the definitions it reaches.</summary>
    private static JsonObject View(JsonObject root, string operation)
    {
        var view = (JsonObject)root.DeepClone();
        view["properties"]!["ops"]!["items"]!["oneOf"] = new JsonArray(Reference(operation));
        var definitions = (JsonObject)view["$defs"]!;
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([operation]);
        while (pending.TryPop(out string? name))
        {
            if (reached.Add(name))
            {
                foreach (string next in References(definitions[name]!))
                {
                    pending.Push(next);
                }
            }
        }

        foreach (string name in definitions.Select(static entry => entry.Key).Where(name => !reached.Contains(name)).ToArray())
        {
            definitions.Remove(name);
        }

        return view;
    }

    private static IEnumerable<string> References(JsonNode node) => node switch
    {
        JsonObject value => value.SelectMany(static member =>
            member.Key == "$ref" && member.Value!.GetValue<string>() is { } target && target.StartsWith(DefinitionPrefix, StringComparison.Ordinal)
                ? [target[DefinitionPrefix.Length..]]
                : member.Value is null ? [] : References(member.Value)),
        JsonArray items => items.SelectMany(static item => item is null ? [] : References(item)),
        _ => [],
    };

    private static string Serialize(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }
}
