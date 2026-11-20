using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>
/// The JSON Schema of a table's operation document, written from the table and nothing else:
/// the five operation shapes, one definition per kind (its addresses and rows), per add and per
/// action. Each kind, add (<c>add.&lt;name&gt;</c>) and action (<c>do.&lt;name&gt;</c>) also has a view of
/// its own. The text is deterministic: declaration order, two-space indentation, <c>\n</c> line
/// endings and a trailing newline.
/// </summary>
public sealed class TableSchema
{
    private readonly Lazy<JsonObject> _document;
    private readonly Lazy<string> _text;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _views;

    internal TableSchema(string id, string uri, DocumentTable table)
    {
        Id = id;
        _document = new Lazy<JsonObject>(() => TableSchemaWriter.Write(uri, table));
        _text = new Lazy<string>(() => TableSchemaWriter.Text(_document.Value));
        _views = new Lazy<IReadOnlyDictionary<string, string>>(() => new Dictionary<string, string>(TableSchemaWriter.Views(uri, _document.Value), StringComparer.Ordinal)
        {
            [DirectoryView] = TableSchemaWriter.Directory(table),
        });
        Names = [DirectoryView, .. table.Kinds.Select(static kind => kind.Name),
            .. table.Adders.Select(static adder => $"add.{adder.Name}").Distinct(StringComparer.Ordinal),
            .. table.Actions.Select(static action => $"do.{action.Name}")];
    }

    /// <summary>
    /// The view that lists the table in one short JSON document: each kind's addresses and rows,
    /// each add's parents and parameters, each action's parameters.
    /// </summary>
    public const string DirectoryView = "directory";

    /// <summary>The schema's id in the <c>schema</c> command, such as <c>v2/slides/ops</c>.</summary>
    public string Id { get; }

    /// <summary>The names of the views: <c>directory</c>, each kind, <c>add.&lt;name&gt;</c> and <c>do.&lt;name&gt;</c>.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The schema text.</summary>
    public string Document => _text.Value;

    /// <summary>Each view's text, by name.</summary>
    public IReadOnlyDictionary<string, string> Views => _views.Value;

    /// <summary>The schema of <paramref name="table"/>, published as the product's <c>ops</c> schema.</summary>
    public static TableSchema Of(string productId, DocumentTable table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentNullException.ThrowIfNull(table);
        return new TableSchema(
            Contracts.ResultEnvelope.CatalogId(productId, DocumentTable.SchemaId),
            Contracts.ResultEnvelope.SchemaUri(productId, DocumentTable.SchemaId),
            table);
    }
}

internal static class TableSchemaWriter
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";

    internal static JsonObject Write(string uri, DocumentTable table)
    {
        var definitions = new JsonObject
        {
            ["address"] = new JsonObject
            {
                ["type"] = "string",
                ["minLength"] = 1,
                ["description"] = $"An object's address: '{table.Root.Name}' for the document itself, otherwise segments and keys from the document down, "
                    + "such as each kind's x-addresses shows. read gives every object's address."
                    + (Recursive(table).Count == 0 ? string.Empty : " A form that starts with {kind} continues an address of an object of that kind, one of the kind's own forms.")
                    + (UsesOrdinals(table) ? " <ordinal> is a position from 1, as the engine's collection counts it; <index> is a position from 0." : string.Empty)
                    + (UsesText(table) ? @" In a key that is a name, a / is written \/ and a \ is written \\." : string.Empty),
            },
            ["set"] = Shape("Sets rows of the object at an address, in the order written; each row sets one engine member.",
                ("set", With(Ref("address"), "The address of the object whose rows are set.")),
                ("props", new JsonObject
                {
                    ["type"] = "object",
                    ["minProperties"] = 1,
                    ["description"] = "The rows to set, by name, each in its kind's form; the definition of the addressed object's kind lists its rows.",
                })),
            ["add"] = new JsonObject
            {
                ["description"] = "Makes one engine call that creates an object, then sets rows of the new object.",
                // One branch per add name: an add made under several kinds of parent has one definition.
                ["oneOf"] = new JsonArray([.. table.Adders.Select(static adder => adder.Name).Distinct(StringComparer.Ordinal).Select(name => (JsonNode)Ref($"add.{name}"))]),
            },
            ["delete"] = Shape("Deletes the object at an address.", ("delete", With(Ref("address"), "The address of the object to delete."))),
            ["move"] = Shape("Moves the object at an address to a position among its siblings.",
                ("move", With(Ref("address"), "The address of the object to move.")),
                ("to", new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["description"] = "The position to move to, the engine's, from 0." })),
            ["do"] = new JsonObject
            {
                ["description"] = "Makes one engine call on the whole document.",
                ["oneOf"] = new JsonArray([.. table.Actions.Select(action => (JsonNode)Ref($"do.{action.Name}"))]),
            },
        };
        foreach (ObjectKind kind in table.Kinds)
        {
            definitions[kind.Name] = Kind(table, kind);
        }

        // One definition per add name: the same add can be made under several kinds of parent.
        foreach (IGrouping<string, AdderEntry> adders in table.Adders.GroupBy(static adder => adder.Name, StringComparer.Ordinal))
        {
            AdderEntry adder = adders.First();
            string[] parents = [.. adders.Select(static entry => Parent(entry)).Distinct(StringComparer.Ordinal)];
            var properties = new JsonObject
            {
                ["add"] = new JsonObject { ["const"] = adder.Name, ["description"] = "The add's name." },
            };
            var required = new JsonArray("add");
            if (adder.Placement.Parent != table.Root)
            {
                properties["in"] = With(Ref("address"), $"The address of the {Either(parents)} to add to.");
                required.Add("in");
            }

            properties["args"] = With(Arguments(table, adder.Arguments), "The engine call's parameters, by name.");
            if (adder.Arguments.Any(static argument => argument.Required))
            {
                required.Add("args");
            }

            properties["props"] = With(new JsonObject { ["type"] = "object" }, $"Rows of the new {adder.Placement.Kind.Name} to set after the call, as #/$defs/{adder.Placement.Kind.Name} lists them.");
            definitions[$"add.{adder.Name}"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = adders.Count() == 1
                    ? $"Adds a {adder.Placement.Kind.Name} to a {adder.Placement.Parent.Name}: {adder.Description}"
                    : string.Join(" ", adders.Select(static entry => $"Adds a {entry.Placement.Kind.Name} to a {entry.Placement.Parent.Name}: {entry.Description.TrimEnd('.')}.")),
                ["required"] = required,
                ["additionalProperties"] = false,
                ["properties"] = properties,
            };
        }

        foreach (ActionEntry action in table.Actions)
        {
            var required = new JsonArray("do");
            if (action.Arguments.Any(static argument => argument.Required))
            {
                required.Add("args");
            }

            definitions[$"do.{action.Name}"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = action.Description,
                ["required"] = required,
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["do"] = new JsonObject { ["const"] = action.Name, ["description"] = "The action's name." },
                    ["args"] = With(Arguments(table, action.Arguments), "The engine call's parameters, by name."),
                },
            };
        }

        return new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = uri,
            ["title"] = "Object table operations",
            ["description"] = "An edit's operations, applied in order to the document in memory; when one fails, nothing is written. "
                + "Each kind's definition lists its addresses and rows, each add's and action's the engine call it makes.",
            ["type"] = "object",
            ["required"] = new JsonArray("ops"),
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["ops"] = new JsonObject
                {
                    ["type"] = "array",
                    ["minItems"] = 1,
                    ["maxItems"] = TableBatch.MaximumOperations,
                    ["items"] = new JsonObject
                    {
                        ["oneOf"] = new JsonArray(Ref("set"), Ref("add"), Ref("delete"), Ref("move"), Ref("do")),
                    },
                },
            },
            ["$defs"] = definitions,
        };
    }

    internal static string Text(JsonNode node) =>
        node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            .ReplaceLineEndings("\n") + "\n";

    /// <summary>One self-contained view per kind, add and action: its definition and the definitions it refers to.</summary>
    internal static IReadOnlyDictionary<string, string> Views(string uri, JsonObject document)
    {
        JsonObject definitions = document["$defs"]!.AsObject();
        var views = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string name, JsonNode? definition) in definitions)
        {
            if (name is "set" or "add" or "delete" or "move" or "do" or "address")
            {
                continue;
            }

            var view = new JsonObject { ["$schema"] = Draft, ["$id"] = $"{uri}#/$defs/{name}" };
            foreach ((string key, JsonNode? value) in definition!.AsObject())
            {
                view[key] = value?.DeepClone();
            }

            var referenced = new JsonObject();
            foreach (string reference in References(definition))
            {
                referenced[reference] = definitions[reference]!.DeepClone();
            }

            if (referenced.Count > 0)
            {
                view["$defs"] = referenced;
            }

            views[name] = Text(view);
        }

        return views;
    }

    private static IEnumerable<string> References(JsonNode node) => node switch
    {
        JsonObject value => value.SelectMany(member => member.Key == "$ref" && member.Value?.GetValue<string>() is { } target
            ? [target["#/$defs/".Length..]]
            : member.Value is null ? [] : References(member.Value)).Distinct(StringComparer.Ordinal),
        JsonArray items => items.Where(static item => item is not null).SelectMany(static item => References(item!)).Distinct(StringComparer.Ordinal),
        _ => [],
    };

    private static JsonObject Kind(DocumentTable table, ObjectKind kind)
    {
        var properties = new JsonObject();
        foreach (IGrouping<string, TableRow> rows in kind.Rows.GroupBy(static row => row.Name, StringComparer.Ordinal))
        {
            TableRow first = rows.First();
            JsonObject schema = first.Kind.Schema();
            string only = rows.All(row => row.Target == kind.Type)
                ? string.Empty
                : $" Only on {string.Join(" and ", rows.Select(static row => row.Target.Name))}.";
            schema["description"] = $"{string.Join("; ", rows.Select(static row => row.Description).Distinct(StringComparer.Ordinal))}.{only}".Replace("..", ".", StringComparison.Ordinal);
            if (!rows.Any(static row => row.Settable))
            {
                schema["readOnly"] = true;
            }

            properties[first.Name] = schema;
        }

        Placement? placement = table.Placements.FirstOrDefault(candidate => candidate.Kind == kind);
        var definition = new JsonObject
        {
            ["type"] = "object",
            ["description"] = kind.Description,
            ["x-addresses"] = new JsonArray([.. Addresses(table, kind, 0).Select(static address => (JsonNode)address)]),
        };
        if (placement is not null)
        {
            definition["x-delete"] = table.Placements.Any(candidate => candidate.Kind == kind && candidate.Delete is not null);
            definition["x-move"] = table.Placements.Any(candidate => candidate.Kind == kind && candidate.Move is not null);
            if (ListedWhenNamed(table, kind))
            {
                definition["x-listedWhenNamed"] = true;
            }
        }

        definition["properties"] = properties;
        return definition;
    }

    /// <summary>
    /// Every address form of a kind, such as <c>slide/&lt;id&gt;/shape/&lt;id&gt;</c>. A parent kind that can
    /// hold itself, such as a cell that holds a table, is written <c>{cell}</c> and not unrolled:
    /// the form continues an address of that kind, one of its own forms.
    /// </summary>
    private static IEnumerable<string> Addresses(DocumentTable table, ObjectKind kind, int depth) =>
        Addresses(table, kind, depth, Recursive(table));

    private static IEnumerable<string> Addresses(DocumentTable table, ObjectKind kind, int depth, IReadOnlySet<ObjectKind> recursive)
    {
        if (kind == table.Root)
        {
            return [table.Root.Name];
        }

        if (depth > 16)
        {
            return [];
        }

        var forms = new List<string>();
        foreach (Placement placement in table.Placements.Where(candidate => candidate.Kind == kind))
        {
            IEnumerable<string> parents = placement.Parent == table.Root
                ? [string.Empty]
                : recursive.Contains(placement.Parent)
                    ? [$"{{{placement.Parent.Name}}}/"]
                    : Addresses(table, placement.Parent, depth + 1, recursive).Select(static parent => parent + "/");
            string own = Step(placement.Segment, placement.Key, null);
            forms.AddRange(parents.Select(parent => parent + own));
            foreach (AliasPlacement alias in table.Aliases.Where(candidate => candidate.Target == placement))
            {
                string step = Step(alias.Segment, alias.Key, alias.Optional);
                forms.AddRange(parents.Select(parent => parent + step));
            }
        }

        return forms;
    }

    /// <summary>The kinds that can hold themselves through the table's placements, such as a cell that holds a table of cells.</summary>
    private static IReadOnlySet<ObjectKind> Recursive(DocumentTable table)
    {
        var recursive = new HashSet<ObjectKind>();
        foreach (ObjectKind kind in table.Kinds)
        {
            var seen = new HashSet<ObjectKind>();
            var pending = new Queue<ObjectKind>([kind]);
            while (pending.TryDequeue(out ObjectKind? parent))
            {
                foreach (Placement placement in table.Placements.Where(candidate => candidate.Parent == parent))
                {
                    if (placement.Kind == kind)
                    {
                        recursive.Add(kind);
                    }

                    if (seen.Add(placement.Kind))
                    {
                        pending.Enqueue(placement.Kind);
                    }
                }
            }
        }

        return recursive;
    }

    private static string Step(string segment, IReadOnlyList<KeyPart> key, KeyPart? optional) =>
        string.Join('/', [segment, .. key.Select(Part)]) + (optional is { } part ? $"[/{Part(part)}]" : string.Empty);

    private static string Part(KeyPart part) => part switch
    {
        KeyPart.Index => "<index>",
        KeyPart.Ordinal => "<ordinal>",
        KeyPart.Id => "<id>",
        KeyPart.Number => "<number>",
        KeyPart.Guid => "<guid>",
        KeyPart.CellName => "<cell>",
        KeyPart.Area => "<area>",
        KeyPart.RowName => "<row>",
        KeyPart.ColumnName => "<column>",
        _ => "<name>",
    };

    /// <summary>Whether a key of the table is a position from 1.</summary>
    private static bool UsesOrdinals(DocumentTable table) =>
        table.Placements.Any(static placement => placement.Key.Contains(KeyPart.Ordinal));

    /// <summary>Whether a key of the table is a name with any characters, which may hold a / or a \.</summary>
    private static bool UsesText(DocumentTable table) =>
        table.Placements.Any(static placement => placement.Key.Contains(KeyPart.Text))
        || table.Aliases.Any(static alias => alias.Key.Contains(KeyPart.Text));

    /// <summary>Whether read lists objects of <paramref name="kind"/> only when --kind names it.</summary>
    private static bool ListedWhenNamed(DocumentTable table, ObjectKind kind) =>
        table.Placements.Any(candidate => candidate.Kind == kind && candidate.ListedWhenNamed);

    private static JsonObject Arguments(DocumentTable table, IReadOnlyList<Argument> arguments)
    {
        var properties = new JsonObject();
        foreach (Argument argument in arguments)
        {
            JsonObject schema = argument.Kind.Schema();
            schema["description"] = argument.Description;
            if (argument.Addresses is { } kinds)
            {
                schema["x-addresses"] = new JsonArray([.. kinds.SelectMany(kind => Addresses(table, kind, 0)).Select(static address => (JsonNode)address)]);
            }

            properties[argument.Name] = schema;
        }

        var result = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
        };
        JsonNode[] required = [.. arguments.Where(static argument => argument.Required).Select(static argument => (JsonNode)argument.Name)];
        if (required.Length > 0)
        {
            result["required"] = new JsonArray(required);
        }

        return result;
    }

    private static JsonObject Shape(string description, params (string Name, JsonNode Schema)[] properties) => new()
    {
        ["type"] = "object",
        ["description"] = description,
        ["required"] = new JsonArray([.. properties.Select(static property => (JsonNode)property.Name)]),
        ["additionalProperties"] = false,
        ["properties"] = new JsonObject(properties.Select(static property => KeyValuePair.Create(property.Name, (JsonNode?)property.Schema))),
    };

    /// <summary>
    /// The directory of a table: per kind its address forms, the rows a set can write, the rows
    /// that are read only, and whether it can be deleted and moved; per add the kinds it adds to
    /// and its parameter names; per action its parameter names. JSON, written from the table.
    /// </summary>
    internal static string Directory(DocumentTable table)
    {
        var kinds = new JsonObject();
        foreach (ObjectKind kind in table.Kinds)
        {
            IGrouping<string, TableRow>[] rows = [.. kind.Rows.GroupBy(static row => row.Name, StringComparer.Ordinal)];
            var entry = new JsonObject
            {
                ["addresses"] = Names(Addresses(table, kind, 0)),
                ["rows"] = Names(rows.Where(static row => row.Any(static each => each.Settable)).Select(static row => row.Key)),
                ["readOnlyRows"] = Names(rows.Where(static row => !row.Any(static each => each.Settable)).Select(static row => row.Key)),
            };
            if (table.Placements.Any(candidate => candidate.Kind == kind))
            {
                entry["delete"] = table.Placements.Any(candidate => candidate.Kind == kind && candidate.Delete is not null);
                entry["move"] = table.Placements.Any(candidate => candidate.Kind == kind && candidate.Move is not null);
                if (ListedWhenNamed(table, kind))
                {
                    entry["listedWhenNamed"] = true;
                }
            }

            kinds[kind.Name] = entry;
        }

        var adds = new JsonObject();
        foreach (IGrouping<string, AdderEntry> adders in table.Adders.GroupBy(static adder => adder.Name, StringComparer.Ordinal))
        {
            AdderEntry adder = adders.First();
            var entry = new JsonObject
            {
                ["adds"] = adder.Placement.Kind.Name,
                ["in"] = Names(adders.Select(static each => each.Placement.Parent.Name).Distinct(StringComparer.Ordinal)),
            };
            AdderEntry[] narrowed = [.. adders.Where(static each => each.Target != each.Placement.Parent.Type)];
            if (narrowed.Length > 0)
            {
                entry["inOnly"] = new JsonObject(narrowed.Select(static each => KeyValuePair.Create(each.Placement.Parent.Name, (JsonNode?)each.Target.Name)));
            }

            entry["args"] = Names(adder.Arguments.Select(static argument => argument.Name));
            adds[adders.Key] = entry;
        }

        var actions = new JsonObject();
        foreach (ActionEntry action in table.Actions)
        {
            actions[action.Name] = new JsonObject { ["args"] = Names(action.Arguments.Select(static argument => argument.Name)) };
        }

        var directory = new JsonObject { ["kinds"] = kinds, ["adds"] = adds, ["actions"] = actions };
        if (Recursive(table).Count > 0)
        {
            directory["addressForms"] = "A form that starts with {kind} continues an address of an object of that kind, one of the kind's own addresses.";
        }

        return Text(directory);
    }

    /// <summary>The parent an add adds to, with the engine type it needs when that is narrower than the kind's.</summary>
    private static string Parent(AdderEntry adder) =>
        adder.Target == adder.Placement.Parent.Type ? adder.Placement.Parent.Name : $"{adder.Placement.Parent.Name} that is an {adder.Target.Name}";

    private static JsonArray Names(IEnumerable<string> names) => new([.. names.Select(static name => (JsonNode)name)]);

    private static string Either(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}";

    private static JsonObject Ref(string name) => new() { ["$ref"] = $"#/$defs/{name}" };

    private static JsonObject With(JsonObject schema, string description)
    {
        schema["description"] = description;
        return schema;
    }
}
