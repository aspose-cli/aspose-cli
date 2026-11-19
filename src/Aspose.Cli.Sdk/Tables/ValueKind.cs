using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>
/// How a value of the object table is written in JSON: its schema, how an engine value is
/// written (<c>Encode</c>) and how a value the caller wrote is read (<c>Decode</c>). The form is
/// fixed and does not depend on the document: a color is <c>#RRGGBB</c>, a length is in points,
/// an enumeration value is the engine's member name.
/// </summary>
public abstract class ValueKind
{
    private protected ValueKind(string name, string description, TableInput input = TableInput.None)
    {
        Input = input;
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
    }

    /// <summary>The kind's name in the schema, such as <c>length</c>.</summary>
    public string Name { get; }

    /// <summary>What a value of the kind is, written into the schema.</summary>
    public string Description { get; }

    /// <summary>What the edit reads for a value of the kind before it opens the document: nothing, a file's path, an image's bytes or a secret.</summary>
    public TableInput Input { get; }

    /// <summary>A new JSON Schema fragment for a value of the kind.</summary>
    internal abstract JsonObject Schema();

    internal abstract JsonNode? EncodeObject(object? value);

    internal abstract object? DecodeObject(JsonElement value);
}

/// <summary>A value kind whose engine values are <typeparamref name="T"/>.</summary>
public sealed class ValueKind<T> : ValueKind
{
    private readonly Func<JsonObject> _schema;
    private readonly Func<T, JsonNode?> _encode;
    private readonly Func<JsonElement, T> _decode;

    internal ValueKind(string name, string description, Func<JsonObject> schema, Func<T, JsonNode?> encode, Func<JsonElement, T> decode, TableInput input = TableInput.None)
        : base(name, description, input)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _encode = encode ?? throw new ArgumentNullException(nameof(encode));
        _decode = decode ?? throw new ArgumentNullException(nameof(decode));
    }

    /// <summary>Writes an engine value.</summary>
    public JsonNode? Encode(T value) => _encode(value);

    /// <summary>Reads a value the caller wrote.</summary>
    /// <exception cref="TableValueException">The value is not of this kind.</exception>
    public T Decode(JsonElement value) => _decode(value);

    internal override JsonObject Schema() => _schema();

    internal override JsonNode? EncodeObject(object? value) => _encode((T)value!);

    internal override object? DecodeObject(JsonElement value) => _decode(value);
}

/// <summary>What the edit reads for a value before it opens the document.</summary>
public enum TableInput
{
    /// <summary>Nothing: the value is used as written.</summary>
    None,

    /// <summary>The path of a local file, resolved against the directory the command runs in.</summary>
    File,

    /// <summary>The bytes of a local image file, which the SDK checks is an intact image.</summary>
    Image,

    /// <summary>The value of the environment variable the value names, a secret such as a password.</summary>
    Secret,
}

/// <summary>A value the caller wrote is not of the kind it is given for; the message states the fact.</summary>
public sealed class TableValueException(string message) : Exception(message);

/// <summary>The value kinds of the object table.</summary>
public static class ValueKinds
{
    /// <summary>Text, written as the engine gives it: never trimmed, line breaks kept.</summary>
    public static ValueKind<string?> Text { get; } = new(
        "text", "A string, as the engine gives it.",
        static () => new JsonObject { ["type"] = "string" },
        static value => value is null ? null : JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Expected("a string", value));

    /// <summary>A whole number in the range of a 32-bit integer.</summary>
    public static ValueKind<int> Integer { get; } = new(
        "integer", "A whole number.",
        static () => new JsonObject { ["type"] = "integer" },
        static value => JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : throw Expected("a whole number", value));

    /// <summary>A whole number in the range of a 16-bit integer, such as a paragraph's depth.</summary>
    public static ValueKind<short> ShortInteger { get; } = new(
        "short-integer", "A whole number from -32768 to 32767.",
        static () => new JsonObject { ["type"] = "integer", ["minimum"] = short.MinValue, ["maximum"] = short.MaxValue },
        static value => JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out short number) ? number : throw Expected("a whole number from -32768 to 32767", value));

    /// <summary>One character, such as a bullet's.</summary>
    public static ValueKind<char> Character { get; } = new(
        "character", "A string of one character.",
        static () => new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 1 },
        static value => JsonValue.Create(value.ToString()),
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: 1 } text ? text[0] : throw Expected("one character", value));

    /// <summary>An unsigned whole number, such as an engine id.</summary>
    public static ValueKind<uint> UnsignedInteger { get; } = new(
        "unsigned-integer", "A whole number from 0.",
        static () => new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = uint.MaxValue },
        static value => JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out uint number) ? number : throw Expected("a whole number from 0", value));

    /// <summary>A number; an engine value that is not a number (NaN) is written as null.</summary>
    public static ValueKind<double> Number { get; } = new(
        "number", "A number; null where the engine gives NaN.",
        static () => new JsonObject { ["type"] = new JsonArray("number", "null") },
        static value => double.IsFinite(value) ? JsonValue.Create(value) : null,
        static value => value.ValueKind == JsonValueKind.Number ? value.GetDouble() : throw Expected("a number", value));

    /// <summary>A length in points; an engine value that is not a number (NaN) is written as null.</summary>
    public static ValueKind<float> Length { get; } = new(
        "length", "A length in points; null where the engine gives NaN.",
        static () => new JsonObject { ["type"] = new JsonArray("number", "null") },
        static value => float.IsFinite(value) ? JsonValue.Create(value) : null,
        static value => value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number) && float.IsFinite(number)
            ? number
            : throw Expected("a number of points", value));

    /// <summary>A number of the engine's single-precision type, such as a duration in seconds.</summary>
    public static ValueKind<float> Single { get; } = new(
        "single", "A number; null where the engine gives NaN.",
        static () => new JsonObject { ["type"] = new JsonArray("number", "null") },
        static value => float.IsFinite(value) ? JsonValue.Create(value) : null,
        static value => value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number) && float.IsFinite(number)
            ? number
            : throw Expected("a number", value));

    /// <summary>true or false.</summary>
    public static ValueKind<bool> Boolean { get; } = new(
        "boolean", "true or false.",
        static () => new JsonObject { ["type"] = "boolean" },
        static value => JsonValue.Create(value),
        static value => value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Expected("true or false", value),
        });

    /// <summary>A color as <c>#RRGGBB</c>, upper case; an empty engine color is written as null.</summary>
    public static ValueKind<Color> Color { get; } = new(
        "color", "A color as #RRGGBB; null where the engine gives an empty color.",
        static () => new JsonObject { ["type"] = new JsonArray("string", "null"), ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
        static value => value.IsEmpty ? null : JsonValue.Create($"#{value.R:X2}{value.G:X2}{value.B:X2}"),
        static value => value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: 7 } text && text[0] == '#'
            && int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb)
            ? System.Drawing.Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
            : throw Expected("a color written #RRGGBB", value));

    /// <summary>A GUID written in the <c>D</c> form.</summary>
    public static ValueKind<Guid> Guid { get; } = new(
        "guid", "A GUID such as 9f1c2c1e-0d6b-4a52-9d0c-8f0a3b1f7e11.",
        static () => new JsonObject { ["type"] = "string", ["format"] = "uuid" },
        static value => JsonValue.Create(value.ToString("D")),
        static value => value.ValueKind == JsonValueKind.String && System.Guid.TryParseExact(value.GetString(), "D", out Guid parsed)
            ? parsed
            : throw Expected("a GUID such as 9f1c2c1e-0d6b-4a52-9d0c-8f0a3b1f7e11", value));

    /// <summary>
    /// A path of a local file to read, relative to the directory the command runs in; the edit
    /// resolves it and refuses an output that names it.
    /// </summary>
    public static ValueKind<string> File { get; } = new(
        "file", "The path of a local file to read, relative to the directory the command runs in.",
        static () => new JsonObject { ["type"] = "string", ["minLength"] = 1 },
        static value => JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } path ? path : throw Expected("a file path", value),
        TableInput.File);

    /// <summary>
    /// The path of a local image file, relative to the directory the command runs in; the edit reads
    /// the image and checks it before it opens the document, and passes its bytes on.
    /// </summary>
    public static ValueKind<object> Image { get; } = new(
        "image", "The path of a local image file (PNG, JPEG, GIF, BMP, TIFF and the other bitmap and engine formats the CLI checks; no SVG), relative to the directory the command runs in.",
        static () => new JsonObject { ["type"] = "string", ["minLength"] = 1 },
        static value => value is string path ? JsonValue.Create(path) : null,
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } path ? path : throw Expected("an image file path", value),
        TableInput.Image);

    /// <summary>The name of an environment variable whose value is a secret, such as a password; the edit reads it, and never writes it.</summary>
    public static ValueKind<object> SecretVariable { get; } = new(
        "secret-variable", "The name of an environment variable that holds the secret.",
        static () => new JsonObject { ["type"] = "string", ["minLength"] = 1 },
        static _ => null,
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } name ? name : throw Expected("an environment variable name", value),
        TableInput.Secret);

    /// <summary>
    /// Text written as a key part of <paramref name="part"/> is, such as an A1 area <c>B2:D10</c> or
    /// a column name <c>C</c>: its form is checked as an address checks it, and what it names is
    /// the engine's to say.
    /// </summary>
    public static ValueKind<string> KeyText(KeyPart part, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string description = TableAddress.Describe(part);
        return new(
            name, $"{char.ToUpperInvariant(description[0])}{description[1..]}.",
            static () => new JsonObject { ["type"] = "string", ["minLength"] = 1 },
            static value => JsonValue.Create(value),
            value => value.ValueKind == JsonValueKind.String && value.GetString() is { } text && TableAddress.Matches(part, text)
                ? text
                : throw Expected(description, value));
    }

    /// <summary>The address of an object, as read gives it in <c>at</c>; an address argument's value.</summary>
    internal static ValueKind<string> AddressText { get; } = new(
        "address", "An object's address, as read gives it in at.",
        static () => new JsonObject { ["type"] = "string", ["minLength"] = 1 },
        static value => JsonValue.Create(value),
        static value => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : throw Expected("an address", value));

    /// <summary>A member of the engine's enumeration <typeparamref name="TEnum"/>, by its name.</summary>
    public static ValueKind<TEnum> Enum<TEnum>()
        where TEnum : struct, System.Enum
    {
        string[] names = System.Enum.GetNames<TEnum>();
        return new(
            $"enum:{typeof(TEnum).Name}",
            $"A member name of the engine's {typeof(TEnum).Name}.",
            () => new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. names.Select(static name => (JsonNode)name)]) },
            static value => JsonValue.Create(System.Enum.GetName(value) ?? Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)),
            value => value.ValueKind == JsonValueKind.String && System.Enum.TryParse(value.GetString(), ignoreCase: false, out TEnum parsed)
                && names.Contains(value.GetString(), StringComparer.Ordinal)
                ? parsed
                : throw new TableValueException($"{Describe(value)} is not a member of {typeof(TEnum).Name}; it is one of: {string.Join(", ", names)}"));
    }

    /// <summary>A value of <paramref name="kind"/>, or null where the engine gives none, such as the placeholder of a shape that is no placeholder.</summary>
    public static ValueKind<T?> Optional<T>(ValueKind<T> kind)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(kind);
        return new(
            $"optional:{kind.Name}",
            $"{kind.Description} null where the engine gives none.",
            () => new JsonObject { ["anyOf"] = new JsonArray(kind.Schema(), new JsonObject { ["type"] = "null" }) },
            value => value is { } present ? kind.Encode(present) : null,
            value => value.ValueKind == JsonValueKind.Null ? null : kind.Decode(value));
    }

    /// <summary>A list of values of <paramref name="item"/>.</summary>
    public static ValueKind<IReadOnlyList<T>> ListOf<T>(ValueKind<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new(
            $"list:{item.Name}",
            $"A list; each item: {item.Description}",
            () => new JsonObject { ["type"] = "array", ["items"] = item.Schema() },
            values => new JsonArray([.. values.Select(item.Encode)]),
            value => value.ValueKind == JsonValueKind.Array
                ? [.. value.EnumerateArray().Select(item.Decode)]
                : throw Expected("a list", value));
    }

    /// <summary>A product's own kind, such as a reference to a cell of a chart's workbook.</summary>
    public static ValueKind<T> Create<T>(string name, string description, Func<JsonObject> schema, Func<T, JsonNode?> encode, Func<JsonElement, T> decode, TableInput input = TableInput.None) =>
        new(name, description, schema, encode, decode, input);

    /// <summary>The refusal of a value that is not what <paramref name="expected"/> says.</summary>
    public static TableValueException Expected(string expected, JsonElement value) =>
        new($"{Describe(value)} is not {expected}");

    /// <summary>A short description of what the caller wrote, which never quotes a long string in full.</summary>
    internal static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() is { Length: <= 40 } text ? $"\"{text}\"" : "the string",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "an array",
        _ => "an object",
    };
}
