using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The dictionary of <c>error.details</c> keys: one key per concept, each with the description
/// the error schema publishes for it. Budgets state <c>limit</c>, <c>actual</c> and <c>unit</c>;
/// collections state <c>available</c> and <c>suggestions</c>; an operation error states the
/// operation's <c>id</c> and <c>op</c>; a positional argument is <c>argument</c>, an option
/// <c>option</c>. A details object holds no key outside it and no null value. A key whose
/// values are tokens publishes their pattern (<see cref="Patterns"/>), or, when they form a
/// closed set, each token with its meaning (<see cref="Tokens"/>).
/// </summary>
public static class ErrorDetailKeys
{
    /// <summary>Every key with its description.</summary>
    public static FrozenDictionary<string, string> All { get; } = new (string Key, string Description)[]
    {
        ("actual", "The measured amount of a budget, in unit."),
        ("actualExists", "Whether the output existed when publication found it changed."),
        ("actualSha256", "The SHA-256 the file had when the command found it changed."),
        ("actualSizeBytes", "The size the file had when the command found it changed."),
        ("argument", "The positional argument the error is about, such as file."),
        ("available", "The names or values that exist or are accepted, at most 50."),
        ("availableCount", "How many names or values exist or are accepted."),
        ("cause", "Why an operation field was refused."),
        ("content", "The kinds of content an output leaves out, each one of these tokens."),
        ("declared", "The product the command or extension declares."),
        ("detected", "The products whose formats the content matches."),
        ("diagnosticId", "The host diagnostic id of a startup or process failure."),
        ("directory", "The directory the error is about."),
        ("dpi", "The resolution of a render, in dots per inch."),
        ("engineMessage", "The document engine's own message."),
        ("errors", "Every problem, when there are several."),
        ("expectedExists", "Whether the output existed when the command read it."),
        ("expectedSha256", "The SHA-256 the file had when the command read it."),
        ("expectedSizeBytes", "The size the file had when the command read it."),
        ("extension", "The file extension the error is about."),
        ("feature", "The feature the product or format does not support."),
        ("feed", "The release feed the update read."),
        ("field", "The operation field the error is about."),
        ("format", "The format id the error is about."),
        ("height", "The height of a render, in pixels."),
        ("id", "The id of the failing operation, as its applied[] entry states it."),
        ("limit", "The budget's limit, in unit."),
        ("list", "The result field holding a list that was capped, such as outline."),
        ("offset", "The byte offset of the problem in an input."),
        ("op", "The op name of the failing operation."),
        ("operation", "The product feature the error is about."),
        ("option", "The option the error is about, such as --pages."),
        ("originalError", "The error that stopped a publication: its code and details."),
        ("part", "The kind of part of a document that holds text an operation found and left as it was, one of these tokens."),
        ("path", "The absolute path of the file the error is about."),
        ("phase", "The stage of processing that failed."),
        ("pid", "The id of the process the error is about."),
        ("port", "The loopback port the error is about."),
        ("problems", "The problems the engine reported."),
        ("product", "The product the error is about."),
        ("profile", "The conformance profile the error is about."),
        ("quiescenceConfirmed", "Whether the stopped worker was confirmed to have ended."),
        ("range", "The range or selection as the caller wrote it."),
        ("reason", "Why the value or file was refused, as a short kebab-case token such as unknown-field; the message explains it."),
        ("recoveryComplete", "Whether every publication target was restored."),
        ("requested", "The name, number, value or path as the caller wrote it."),
        ("resource", "The budget's resource kind."),
        ("sheet", "The worksheet the error is about."),
        ("source", "The source of a value, such as a variable or file."),
        ("subject", "What was looked up, such as sheet, page or file."),
        ("suggestions", "The accepted names closest to the request, best first."),
        ("target", "The target the error is about."),
        ("targets", "The publication targets and their state."),
        ("timeoutSeconds", "The timeout that elapsed, in seconds."),
        ("unit", "The unit of a budget's limit and actual."),
        ("variable", "The environment variable the error is about."),
        ("width", "The width of a render, in pixels."),
    }.ToFrozenDictionary(static key => key.Key, static key => key.Description, StringComparer.Ordinal);

    /// <summary>
    /// The pattern of each key whose value is a token, never words: <c>reason</c> is kebab-case,
    /// so a caller can branch on it while the message explains it.
    /// </summary>
    public static FrozenDictionary<string, string> Patterns { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["reason"] = "^[a-z0-9]+(-[a-z0-9]+)*$",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The closed set of tokens of each key whose value is one of them, or a list of them
    /// (<see cref="TokenLists"/>), each with its meaning, which the schema publishes:
    /// <c>content</c> takes the <see cref="ContentKind"/> tokens and <c>part</c> the
    /// <see cref="TextPart"/> tokens.
    /// </summary>
    public static FrozenDictionary<string, IReadOnlyList<(string Token, string Description)>> Tokens { get; } =
        new Dictionary<string, IReadOnlyList<(string Token, string Description)>>(StringComparer.Ordinal)
        {
            ["content"] = [.. ContentKind.All.Select(static kind => (kind.Token, kind.Description))],
            ["part"] = [.. TextPart.All.Select(static part => (part.Token, part.Description))],
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The keys of <see cref="Tokens"/> whose value is a list of tokens rather than one.</summary>
    public static FrozenSet<string> TokenLists { get; } = new[] { "content" }.ToFrozenSet(StringComparer.Ordinal);
}

/// <summary>
/// Publishes <see cref="ErrorDetailKeys"/> on the <c>details</c> object of an error: every key
/// with its description, and no other key.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ErrorDetailsAttribute : ValueConstraintAttribute
{
    /// <inheritdoc />
    public override string? Check(object value) => null;

    /// <inheritdoc />
    public override void Describe(JsonObject schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        schema["additionalProperties"] = false;
        schema["properties"] = new JsonObject(
            ErrorDetailKeys.All.OrderBy(static key => key.Key, StringComparer.Ordinal)
                .Select(static key => KeyValuePair.Create(key.Key, (JsonNode?)Describe(key.Key, key.Value))));
    }

    private static JsonObject Describe(string key, string description)
    {
        var property = new JsonObject { ["description"] = description };
        if (ErrorDetailKeys.Tokens.TryGetValue(key, out IReadOnlyList<(string Token, string Description)>? tokens))
        {
            if (ErrorDetailKeys.TokenLists.Contains(key))
            {
                property["type"] = "array";
                property["items"] = new JsonObject { ["type"] = "string", ["oneOf"] = OneOf(tokens) };
            }
            else
            {
                property["type"] = "string";
                property["oneOf"] = OneOf(tokens);
            }
        }
        else if (ErrorDetailKeys.Patterns.TryGetValue(key, out string? pattern))
        {
            property["type"] = "string";
            property["pattern"] = pattern;
        }

        return property;
    }

    /// <summary>The alternatives of a string that is one of <paramref name="tokens"/>, each with its meaning.</summary>
    private static JsonArray OneOf(IReadOnlyList<(string Token, string Description)> tokens) =>
        new([.. tokens.Select(static token => (JsonNode)new JsonObject
        {
            ["const"] = token.Token,
            ["description"] = token.Description,
        })]);
}
