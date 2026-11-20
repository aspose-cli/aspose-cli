using System.Text.Json;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>One operation of a batch, checked against the table.</summary>
/// <param name="Index">Its position in the batch, from 0.</param>
internal abstract record TableOp(int Index);

/// <summary>A value for one row, read from what the caller wrote.</summary>
/// <param name="Name">The row's name.</param>
/// <param name="Value">The value, decoded in the row's kind.</param>
internal sealed record PropValue(string Name, object? Value);

/// <summary>Sets rows of the object at an address.</summary>
internal sealed record SetOp(int Index, TableAddress Address, IReadOnlyList<PropValue> Props) : TableOp(Index);

/// <summary>Makes an add's engine call below the object at <paramref name="In"/>, then sets rows of the new object.</summary>
internal sealed record AddOp(int Index, AdderEntry Adder, TableAddress? In, Arguments Arguments, IReadOnlyList<PropValue> Props) : TableOp(Index);

/// <summary>Deletes the object at an address.</summary>
internal sealed record DeleteOp(int Index, TableAddress Address) : TableOp(Index);

/// <summary>Moves the object at an address to a position.</summary>
internal sealed record MoveOp(int Index, TableAddress Address, int To) : TableOp(Index);

/// <summary>Makes an action's engine call.</summary>
internal sealed record DoOp(int Index, ActionEntry Action, Arguments Arguments) : TableOp(Index);

/// <summary>
/// The operation document of an object table edit: <c>{"ops": [...]}</c>, each operation one of
/// five shapes — set, add, delete, move or do. It is checked against the table before any
/// document is opened: the address follows the table's segments, every name is in the table and
/// every value is of its kind. What depends on the document (whether an address exists, whether
/// an object of a subtype has a row) is checked when the operation runs.
/// </summary>
public sealed class TableBatch
{
    /// <summary>The largest number of operations in one batch.</summary>
    public const int MaximumOperations = 10_000;

    private static readonly string[] Shapes = ["set", "add", "delete", "move", "do"];

    private TableBatch(IReadOnlyList<TableOp> ops, IReadOnlyList<string> files)
    {
        Ops = ops;
        Files = files;
    }

    internal IReadOnlyList<TableOp> Ops { get; }

    /// <summary>The local files the operations read, as they were resolved.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>The number of operations.</summary>
    public int Count => Ops.Count;

    /// <summary>Reads and checks an operation document.</summary>
    /// <param name="json">The document.</param>
    /// <param name="table">The product's table.</param>
    /// <param name="inputs">Reads what the operations name outside the document: files, images and secrets.</param>
    /// <param name="quotesContent">Whether a syntax error may quote the text it stopped at; false for a document read from standard input.</param>
    /// <exception cref="CliException"><c>OPS_INVALID</c>, every problem listed.</exception>
    public static TableBatch Parse(string json, DocumentTable table, ITableInputs inputs, bool quotesContent = true)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(inputs);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw TableErrors.Invalid(null, OpsInvalidReasons.InvalidJson, null, "the document is empty");
        }

        using JsonDocument document = ParseJson(json, quotesContent);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw TableErrors.Invalid(null, OpsInvalidReasons.WrongType, null,
                $"the ops document is an object with one member \"ops\"; this one is {Shape(root.ValueKind)}");
        }

        CheckMembers(null, root, null, ["ops"]);
        if (!root.TryGetProperty("ops", out JsonElement ops))
        {
            throw TableErrors.Invalid(null, OpsInvalidReasons.MissingField, "ops", "the ops document is an object with one member \"ops\"; this one has no ops");
        }

        if (ops.ValueKind != JsonValueKind.Array || ops.GetArrayLength() is < 1 or > MaximumOperations)
        {
            throw TableErrors.Invalid(null, OpsInvalidReasons.InvalidValue, "ops", $"ops is not an array of 1-{MaximumOperations} operations");
        }

        var parser = new Parser(table, inputs);
        var parsed = new List<TableOp>();
        var failures = new List<CliException>();
        int index = 0;
        foreach (JsonElement op in ops.EnumerateArray())
        {
            try
            {
                parsed.Add(parser.Operation(index, op));
            }
            catch (CliException failure)
            {
                failures.Add(failure);
            }

            index++;
        }

        if (failures.Count > 0)
        {
            throw TableErrors.InvalidAll(failures);
        }

        return new TableBatch(parsed, parser.Files);
    }

    /// <summary>What a JSON value is, as an error states it.</summary>
    private static string Shape(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        _ => "null",
    };

    private static JsonDocument ParseJson(string json, bool quotesContent)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            string position = exception is { LineNumber: long line, BytePositionInLine: long column }
                ? $" (line {line + 1}, byte {column + 1})"
                : string.Empty;
            int suffix = exception.Message.IndexOf(" LineNumber:", StringComparison.Ordinal);
            string reason = quotesContent ? $": {(suffix < 0 ? exception.Message : exception.Message[..suffix])}" : string.Empty;
            throw TableErrors.Invalid(null, OpsInvalidReasons.InvalidJson, null, $"the document is not valid JSON{reason}{position}");
        }
    }

    /// <summary>Refuses a member an object does not take, or one written twice.</summary>
    private static void CheckMembers(int? index, JsonElement value, string? path, IReadOnlyList<string> allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty member in value.EnumerateObject())
        {
            string field = path is null ? member.Name : $"{path}.{member.Name}";
            if (!seen.Add(member.Name))
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.DuplicateField, field, $"'{field}' is written twice");
            }

            if (!allowed.Contains(member.Name, StringComparer.Ordinal))
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.UnknownField, field, $"'{field}' is no field {(path is null ? "here" : $"of {path}")}", available: allowed);
            }
        }
    }

    private sealed class Parser(DocumentTable table, ITableInputs inputs)
    {
        private readonly List<string> _files = [];

        public IReadOnlyList<string> Files => _files;

        public TableOp Operation(int index, JsonElement op)
        {
            if (op.ValueKind != JsonValueKind.Object)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.WrongType, null, "an operation is an object with one of set, add, delete, move or do");
            }

            string[] shapes = [.. Shapes.Where(shape => op.TryGetProperty(shape, out _))];
            if (shapes.Length != 1)
            {
                throw TableErrors.Invalid(index, shapes.Length == 0 ? OpsInvalidReasons.MissingField : OpsInvalidReasons.InvalidValue, shapes.Length == 0 ? null : shapes[1],
                    shapes.Length == 0 ? "an operation is one of set, add, delete, move or do, and this one names none of them" : $"an operation is one of set, add, delete, move or do, and this one names {string.Join(" and ", shapes)}",
                    available: shapes.Length == 0 ? Shapes : null);
            }

            return shapes[0] switch
            {
                "set" => Set(index, op),
                "add" => Add(index, op),
                "delete" => Delete(index, op),
                "move" => Move(index, op),
                _ => Do(index, op),
            };
        }

        private SetOp Set(int index, JsonElement op)
        {
            CheckMembers(index, op, null, ["set", "props"]);
            TableAddress address = Address(index, op, "set");
            if (!op.TryGetProperty("props", out JsonElement props))
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.MissingField, "props", "a set has no props", address.Text);
            }

            IReadOnlyList<PropValue> values = Props(index, props, address.Kind, address.Text);
            if (values.Count == 0)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, "props", "props names no row", address.Text);
            }

            return new SetOp(index, address, values);
        }

        private AddOp Add(int index, JsonElement op)
        {
            CheckMembers(index, op, null, ["add", "in", "args", "props"]);
            string name = Name(index, op, "add");
            AdderEntry[] adders = [.. table.Adders.Where(candidate => candidate.Name == name)];
            if (adders.Length == 0)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.UnknownOp, "add", $"'{name}' is no add of this table",
                    available: table.Adders.Select(static candidate => candidate.Name).Distinct(StringComparer.Ordinal));
            }

            string parents = string.Join(" or ", adders.Select(static candidate => $"a {candidate.Placement.Parent.Name}"));
            AdderEntry adder;
            TableAddress? parent = null;
            if (op.TryGetProperty("in", out _))
            {
                parent = Address(index, op, "in");
                adder = adders.FirstOrDefault(candidate => candidate.Placement.Parent == parent.Kind)
                    ?? throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, "in",
                        $"in names a {parent.Kind.Name}; '{name}' adds to {parents}", parent.Text);
                if (adder.Placement.Parent == table.Root)
                {
                    throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, "in", $"'{name}' adds to the {table.Root.Name} itself, so it takes no in");
                }
            }
            else
            {
                adder = adders.FirstOrDefault(candidate => candidate.Placement.Parent == table.Root)
                    ?? throw TableErrors.Invalid(index, OpsInvalidReasons.MissingField, "in", $"'{name}' adds to {parents}; in names which one");
            }

            Arguments arguments = Arguments(index, op, adder.Arguments, name);
            IReadOnlyList<PropValue> props = op.TryGetProperty("props", out JsonElement values)
                ? Props(index, values, adder.Placement.Kind, parent?.Text)
                : [];
            return new AddOp(index, adder, parent, arguments, props);
        }

        private DeleteOp Delete(int index, JsonElement op)
        {
            CheckMembers(index, op, null, ["delete"]);
            TableAddress address = Address(index, op, "delete");
            if (address.Placement?.Delete is null)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, "delete",
                    $"a {address.Kind.Name} {(address.Placement is null ? string.Empty : $"below a {address.Placement.Parent.Name} ")}cannot be deleted", address.Text);
            }

            return new DeleteOp(index, address);
        }

        private MoveOp Move(int index, JsonElement op)
        {
            CheckMembers(index, op, null, ["move", "to"]);
            TableAddress address = Address(index, op, "move");
            if (address.Placement?.Move is null)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, "move",
                    $"a {address.Kind.Name} {(address.Placement is null ? string.Empty : $"below a {address.Placement.Parent.Name} ")}cannot be moved", address.Text);
            }

            if (!op.TryGetProperty("to", out JsonElement to))
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.MissingField, "to", "a move has no to, the position to move to", address.Text);
            }

            if (to.ValueKind != JsonValueKind.Number || !to.TryGetInt32(out int position) || position < 0)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, "to", $"{ValueKinds.Describe(to)} is not a position from 0", address.Text);
            }

            return new MoveOp(index, address, position);
        }

        private DoOp Do(int index, JsonElement op)
        {
            CheckMembers(index, op, null, ["do", "args"]);
            string name = Name(index, op, "do");
            ActionEntry action = table.Actions.FirstOrDefault(candidate => candidate.Name == name)
                ?? throw TableErrors.Invalid(index, OpsInvalidReasons.UnknownOp, "do", $"'{name}' is no action of this table",
                    available: table.Actions.Select(static candidate => candidate.Name));
            return new DoOp(index, action, Arguments(index, op, action.Arguments, name));
        }

        private static string Name(int index, JsonElement op, string field)
        {
            JsonElement value = op.GetProperty(field);
            return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } name
                ? name
                : throw TableErrors.Invalid(index, OpsInvalidReasons.WrongType, field, $"{field} is {ValueKinds.Describe(value)}, not a name");
        }

        private TableAddress Address(int index, JsonElement op, string field)
        {
            JsonElement value = op.GetProperty(field);
            if (value.ValueKind != JsonValueKind.String)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.WrongType, field, $"{field} is {ValueKinds.Describe(value)}, not an address");
            }

            try
            {
                return TableAddress.Parse(table, value.GetString()!);
            }
            catch (TableValueException invalid)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, field, invalid.Message, value.GetString());
            }
        }

        private List<PropValue> Props(int index, JsonElement props, ObjectKind kind, string? at)
        {
            if (props.ValueKind != JsonValueKind.Object)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.WrongType, "props", $"props is {ValueKinds.Describe(props)}, not an object of rows", at);
            }

            var values = new List<PropValue>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty member in props.EnumerateObject())
            {
                string field = $"props.{member.Name}";
                if (!seen.Add(member.Name))
                {
                    throw TableErrors.Invalid(index, OpsInvalidReasons.DuplicateField, field, $"'{field}' is written twice", at);
                }

                TableRow? row = kind.Rows.FirstOrDefault(candidate => candidate.Name == member.Name);
                if (row is null)
                {
                    throw TableErrors.Invalid(index, OpsInvalidReasons.UnknownField, field, $"a {kind.Name} has no row '{member.Name}'", at, kind.SettableNames);
                }

                if (!kind.Rows.Any(candidate => candidate.Name == member.Name && candidate.Settable))
                {
                    throw TableErrors.Invalid(index, OpsInvalidReasons.ReadOnlyField, field, $"row '{member.Name}' of a {kind.Name} can only be read", at, kind.SettableNames);
                }

                values.Add(new PropValue(member.Name, Decode(index, field, row.Kind, member.Value, at)));
            }

            return values;
        }

        private Arguments Arguments(int index, JsonElement op, IReadOnlyList<Argument> declared, string name)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (op.TryGetProperty("args", out JsonElement args))
            {
                if (args.ValueKind != JsonValueKind.Object)
                {
                    throw TableErrors.Invalid(index, OpsInvalidReasons.WrongType, "args", $"args is {ValueKinds.Describe(args)}, not an object");
                }

                if (declared.Count == 0 && args.EnumerateObject().Any())
                {
                    throw TableErrors.Invalid(index, OpsInvalidReasons.UnknownField, "args", $"'{name}' takes no args");
                }

                CheckMembers(index, args, "args", [.. declared.Select(static argument => argument.Name)]);
                foreach (JsonProperty member in args.EnumerateObject())
                {
                    Argument argument = declared.First(candidate => candidate.Name == member.Name);
                    object? value = Decode(index, $"args.{member.Name}", argument.Kind, member.Value, null);
                    values[member.Name] = argument.Addresses is { } kinds ? AddressArgument(index, $"args.{member.Name}", (string)value!, kinds, argument.ResolvedByTheCall) : value;
                }
            }

            if (declared.FirstOrDefault(argument => argument.Required && !values.ContainsKey(argument.Name)) is { } missing)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.MissingField, $"args.{missing.Name}", $"'{name}' needs args.{missing.Name}: {missing.Description}");
            }

            return new Arguments(values);
        }

        /// <summary>An address argument checked against the table: its segments and keys, and the kind it names.</summary>
        private Arguments.AddressValue AddressArgument(int index, string field, string text, IReadOnlyList<ObjectKind> kinds, bool resolvedByTheCall)
        {
            TableAddress address;
            try
            {
                address = TableAddress.Parse(table, text);
            }
            catch (TableValueException invalid)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, field, $"{field}: {invalid.Message}", text);
            }

            return kinds.Contains(address.Kind)
                ? new Arguments.AddressValue(address, resolvedByTheCall)
                : throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, field,
                    $"{field} names a {address.Kind.Name}; it takes the address of a {string.Join(" or a ", kinds.Select(static kind => kind.Name))}", text);
        }

        private object? Decode(int index, string field, ValueKind kind, JsonElement value, string? at)
        {
            object? decoded;
            try
            {
                decoded = kind.DecodeObject(value);
            }
            catch (TableValueException invalid)
            {
                throw TableErrors.Invalid(index, value.ValueKind == JsonValueKind.Null ? OpsInvalidReasons.WrongType : OpsInvalidReasons.InvalidValue,
                    field, $"{field}: {invalid.Message}", at);
            }

            if (kind.Input == TableInput.None || decoded is not string named)
            {
                return decoded;
            }

            if (kind.Input == TableInput.Secret)
            {
                return inputs.ReadSecret(named)
                    ?? throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, field, $"{field}: the environment variable '{named}' is not set or is empty", at);
            }

            string resolved = inputs.ResolvePath(named);
            _files.Add(resolved);
            return kind.Input == TableInput.Image ? inputs.ReadImage(resolved) : resolved;
        }
    }
}
