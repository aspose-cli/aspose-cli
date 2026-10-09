using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// The result schemas one owner publishes, written from the records its assembly describes (see
/// <see cref="IResultSchemaSource"/>). Each schema is written on first use and then kept; most
/// invocations never read one.
/// </summary>
/// <remarks>
/// A schema is the published record with its inherited members first, closed
/// (<c>additionalProperties: false</c>) and described from the records' documentation. A member
/// whose record is published elsewhere references that schema by its URI; any other nested record
/// is written once under <c>$defs</c>, keyed by its camelCase type name. The text is deterministic:
/// stable member order, two-space indentation, <c>\n</c> line endings and a trailing newline.
/// </remarks>
public sealed class ResultSchemaSet
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";
    private const string DefinitionPrefix = "#/$defs/";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ResultSchemaSet? _common;
    private readonly FrozenDictionary<Type, ResultRecord> _records;
    private readonly FrozenDictionary<string, Lazy<string>> _schemas;

    /// <summary>Indexes the records of one owner.</summary>
    /// <param name="owner">A product id, or <c>common</c> for the SDK and the host.</param>
    /// <param name="records">The records the owner's assembly describes.</param>
    /// <param name="common">The SDK's set, whose records the owner's records may reference; null for the SDK's own set.</param>
    /// <exception cref="InvalidOperationException">Two records claim one type or one schema id.</exception>
    public ResultSchemaSet(string owner, IEnumerable<ResultRecord> records, ResultSchemaSet? common)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(records);
        Owner = owner;
        _common = common;
        var byType = new Dictionary<Type, ResultRecord>();
        var schemas = new Dictionary<string, Lazy<string>>(StringComparer.Ordinal);
        foreach (ResultRecord record in records)
        {
            // Every JSON context of an assembly carries the assembly's records.
            if (byType.TryGetValue(record.Type, out ResultRecord? known))
            {
                if (!ReferenceEquals(known, record) && known.SchemaId != record.SchemaId)
                {
                    throw new InvalidOperationException($"Result record {record.Type.Name} is described twice.");
                }

                continue;
            }

            byType.Add(record.Type, record);
            if (record.SchemaId is { } id && !schemas.TryAdd(Id(owner, id), new Lazy<string>(() => Write(record))))
            {
                throw new InvalidOperationException($"Schema '{Id(owner, id)}' is published by more than one result record.");
            }
        }

        _records = byType.ToFrozenDictionary();
        _schemas = schemas.ToFrozenDictionary(StringComparer.Ordinal);
        Ids = [.. _schemas.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>The owner: a product id, or <c>common</c>.</summary>
    public string Owner { get; }

    /// <summary>The published schema ids, such as <c>v2/pdf/render-result</c>, in ordinal order.</summary>
    public IReadOnlyList<string> Ids { get; }

    /// <summary>The schema id of a relative id of <paramref name="owner"/>, such as <c>v2/common/backup</c>.</summary>
    public static string Id(string owner, string relativeId) => $"v2/{owner}/{relativeId}";

    /// <summary>Reads a published schema.</summary>
    public bool TryRead(string id, [NotNullWhen(true)] out string? document)
    {
        document = id is not null && _schemas.TryGetValue(id, out Lazy<string>? schema) ? schema.Value : null;
        return document is not null;
    }

    private bool TryResolve(Type type, [NotNullWhen(true)] out ResultRecord? record, [NotNullWhen(true)] out string? owner)
    {
        if (_records.TryGetValue(type, out record))
        {
            owner = Owner;
            return true;
        }

        if (_common is not null && _common._records.TryGetValue(type, out record))
        {
            owner = _common.Owner;
            return true;
        }

        owner = null;
        return false;
    }

    private ResultRecord Resolve(Type type, out string owner)
    {
        if (!TryResolve(type, out ResultRecord? record, out string? found))
        {
            throw new InvalidOperationException($"Result record {type.Name} is not described by the result contract generator.");
        }

        owner = found;
        return record;
    }

    private string Write(ResultRecord root)
    {
        var definitions = new SortedDictionary<string, JsonObject?>(StringComparer.Ordinal);
        var defined = new Dictionary<string, Type>(StringComparer.Ordinal);
        string uri = ResultEnvelope.SchemaUri(Owner, root.SchemaId!);
        var document = new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = uri,
        };
        foreach ((string key, JsonNode? value) in Object(root, (uri, root.SchemaVersion), definitions, defined))
        {
            document[key] = value?.DeepClone();
        }

        if (definitions.Count > 0)
        {
            document["$defs"] = new JsonObject([.. definitions.Select(static entry => KeyValuePair.Create(entry.Key, (JsonNode?)entry.Value))]);
        }

        return Serialize(document);
    }

    /// <summary>The schema of a record's object, its inherited members first.</summary>
    /// <param name="record">The record.</param>
    /// <param name="identity">The published URI and version a root states in its schema and schemaVersion members; null for a nested record.</param>
    /// <param name="definitions">The document's <c>$defs</c>, which nested records are added to.</param>
    /// <param name="defined">The record type of each <c>$defs</c> entry.</param>
    private JsonObject Object(
        ResultRecord record,
        (string Uri, int Version)? identity,
        SortedDictionary<string, JsonObject?> definitions,
        Dictionary<string, Type> defined)
    {
        var chain = new List<ResultRecord>();
        for (ResultRecord? current = record; current is not null; current = current.Base is null ? null : Resolve(current.Base, out _))
        {
            chain.Insert(0, current);
        }

        ResultProperty[] members =
        [
            .. chain.SelectMany(static (owner, level) => owner.Properties.Select((property, index) => (property, level, index)))
                .OrderBy(static member => member.property.Order)
                .ThenBy(static member => member.level)
                .ThenBy(static member => member.index)
                .Select(static member => member.property),
        ];
        if (members.Select(static member => member.Name).Distinct(StringComparer.Ordinal).Count() != members.Length)
        {
            throw new InvalidOperationException($"Result record {record.Type.Name} has two members with one wire name.");
        }

        var schema = new JsonObject();
        if (record.Description is not null)
        {
            schema["description"] = record.Description;
        }

        schema["type"] = "object";
        string[] required = [.. members.Where(static member => member.Required && !member.Extension).Select(static member => member.Name)];
        if (required.Length > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(static name => (JsonNode)name)]);
        }

        // Extension data writes members the record does not declare, so such a record stays open
        // and declares no members of its own.
        if (members.Any(static member => member.Extension))
        {
            return members.All(static member => member.Extension)
                ? schema
                : throw new InvalidOperationException($"Result record {record.Type.Name} declares members beside its extension data.");
        }

        schema["additionalProperties"] = false;
        var properties = new JsonObject();
        foreach (ResultProperty member in members)
        {
            properties.Add(member.Name, Property(record, member, identity, definitions, defined));
        }

        schema["properties"] = properties;
        ResultProperty[] dependent = [.. members.Where(static member => member.Cases.Count > 0)];
        if (dependent.Length > 1)
        {
            throw new InvalidOperationException($"Result record {record.Type.Name} has more than one member whose type another member decides.");
        }

        if (dependent.Length == 1)
        {
            schema["oneOf"] = Cases(dependent[0]);
        }

        return schema;
    }

    private JsonObject Property(
        ResultRecord record,
        ResultProperty member,
        (string Uri, int Version)? identity,
        SortedDictionary<string, JsonObject?> definitions,
        Dictionary<string, Type> defined)
    {
        var schema = new JsonObject();
        if (member.Description is not null)
        {
            schema["description"] = member.Description;
        }

        JsonNode? constant = member.Const is null ? null : JsonNode.Parse(member.Const);
        if (identity is { } root && member.Name is "schema" or "schemaVersion")
        {
            JsonNode stated = member.Name == "schema" ? JsonValue.Create(root.Uri) : JsonValue.Create(root.Version);
            if (member.Name == "schemaVersion" && root.Version == 0)
            {
                stated = constant ?? throw new InvalidOperationException($"Result record {record.Type.Name} states no schema version.");
            }

            if (constant is not null && !JsonNode.DeepEquals(constant, stated))
            {
                throw new InvalidOperationException($"Result record {record.Type.Name} states {member.Name} {constant.ToJsonString()}, but publishes {stated.ToJsonString()}.");
            }

            constant = stated;
        }

        if (constant is not null)
        {
            schema["const"] = constant;
            return schema;
        }

        Value(schema, member.Value, [.. member.Constraints.Select(static constraint => (constraint, constraint.Depth))], member.OpenPattern, definitions, defined);

        // A value kind may describe itself; the member's own summary says what this member means.
        if (member.Description is not null)
        {
            schema["description"] = member.Description;
        }

        return schema;
    }

    private void Value(
        JsonObject schema,
        ResultValue value,
        IReadOnlyList<(ValueConstraintAttribute Constraint, int Depth)> constraints,
        string? openPattern,
        SortedDictionary<string, JsonObject?> definitions,
        Dictionary<string, Type> defined)
    {
        ValueConstraintAttribute[] here = [.. constraints.Where(static placed => placed.Depth == 0).Select(static placed => placed.Constraint)];
        (ValueConstraintAttribute, int)[] below = [.. constraints.Where(static placed => placed.Depth > 0).Select(static placed => (placed.Constraint, placed.Depth - 1))];
        switch (value.Kind)
        {
            case ResultValueKind.Record:
                schema["$ref"] = Reference(value.Record!, definitions, defined);
                break;
            case ResultValueKind.Any:
                break;
            default:
                schema["type"] = value.Kind switch
                {
                    ResultValueKind.String => "string",
                    ResultValueKind.Integer => "integer",
                    ResultValueKind.Number => "number",
                    ResultValueKind.Boolean => "boolean",
                    ResultValueKind.Array => "array",
                    _ => "object",
                };
                break;
        }

        if (value.Format is not null)
        {
            schema["format"] = value.Format;
        }

        foreach (ValueConstraintAttribute constraint in here)
        {
            constraint.Describe(schema);
        }

        if (openPattern is not null)
        {
            JsonNode named = schema["enum"] ?? throw new InvalidOperationException("An open enumeration needs its allowed values.");
            schema.Remove("enum");
            schema["anyOf"] = new JsonArray(new JsonObject { ["enum"] = named.DeepClone() }, new JsonObject { ["pattern"] = openPattern });
        }

        if (value.Kind is ResultValueKind.Array or ResultValueKind.Map)
        {
            var item = new JsonObject();
            Value(item, value.Items!, below, openPattern: null, definitions, defined);
            schema[value.Kind == ResultValueKind.Array ? "items" : "additionalProperties"] = value.Items!.Nullable ? AllowNull(item) : item;
        }
        else if (below.Length > 0)
        {
            throw new InvalidOperationException($"A constraint is placed below a {value.Kind} value.");
        }
    }

    /// <summary>The reference to a record: its published URI, or its entry under <c>$defs</c>.</summary>
    private string Reference(Type type, SortedDictionary<string, JsonObject?> definitions, Dictionary<string, Type> defined)
    {
        ResultRecord record = Resolve(type, out string owner);
        if (record.SchemaId is not null)
        {
            return ResultEnvelope.SchemaUri(owner, record.SchemaId);
        }

        string name = JsonNamingPolicy.CamelCase.ConvertName(type.Name);
        if (defined.TryGetValue(name, out Type? claimed))
        {
            return claimed == type
                ? DefinitionPrefix + name
                : throw new InvalidOperationException($"Schema definition '{name}' is claimed by {claimed.Name} and {type.Name}.");
        }

        defined.Add(name, type);
        definitions[name] = null;
        definitions[name] = Object(record, identity: null, definitions, defined);
        return DefinitionPrefix + name;
    }

    private static JsonArray Cases(ResultProperty member) =>
        [
            .. member.Cases.Select(@case =>
            {
                var branch = new JsonObject();
                if (@case.Type != "null")
                {
                    branch["required"] = new JsonArray(member.Name);
                }

                branch["properties"] = new JsonObject
                {
                    [@case.Discriminator] = @case.Values.Count == 1
                        ? new JsonObject { ["const"] = @case.Values[0] }
                        : new JsonObject { ["enum"] = new JsonArray([.. @case.Values.Select(static value => (JsonNode)value)]) },
                    [member.Name] = new JsonObject { ["type"] = @case.Type },
                };
                return (JsonNode)branch;
            }),
        ];

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
