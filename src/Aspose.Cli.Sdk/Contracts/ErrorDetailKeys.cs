using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The dictionary of <c>error.details</c> keys: one key per concept, each with the description
/// the error schema publishes for it. Budgets state <c>limit</c>, <c>actual</c> and <c>unit</c>;
/// collections state <c>available</c>; an operation error states the operation's <c>index</c>;
/// a positional argument is <c>argument</c>, an option <c>option</c>. A details object holds no
/// key outside it and no null value. A key whose values are tokens publishes their pattern
/// (<see cref="Patterns"/>).
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
        ("at", "The object table address the error is about, such as slide/256/shape/3."),
        ("available", "The names or values that exist or are accepted, at most 50."),
        ("availableCount", "How many names or values exist or are accepted."),
        ("diagnosticId", "The host diagnostic id of a startup or process failure."),
        ("directory", "The directory the error is about."),
        ("dpi", "The resolution of a render, in dots per inch."),
        ("engineException", "The type of the exception the document engine threw, such as System.ArgumentException."),
        ("engineMessage", "The document engine's own message."),
        ("errors", "Every problem, when there are several."),
        ("expectedExists", "Whether the output existed when the command read it."),
        ("expectedSha256", "The SHA-256 the file had when the command read it."),
        ("expectedSizeBytes", "The size the file had when the command read it."),
        ("extension", "The file extension the error is about."),
        ("field", "The operation field the error is about."),
        ("format", "The format id the error is about."),
        ("height", "The height of a render, in pixels."),
        ("index", "The position of the failing operation in an object table batch's ops, from 0."),
        ("limit", "The budget's limit, in unit."),
        ("notSupplied", "The resources a document names that the CLI's resource loader supplied no bytes for, each as {reference, reason}: the reference as the document writes it and why nothing was supplied, such as a network address."),
        ("option", "The option the error is about, such as --pages."),
        ("originalError", "The error that stopped a publication: its code and details."),
        ("originalPath", "Where the original that a publication moved aside is now, when the publication could not move it back."),
        ("path", "The absolute path of the file the error is about."),
        ("phase", "The stage of processing that failed."),
        ("reason", "Why the value or file was refused, as a short kebab-case token such as unknown-field; the message explains it."),
        ("recoveryComplete", "Whether every publication target was restored."),
        ("requested", "The name, number, value or path as the caller wrote it."),
        ("resource", "The budget's resource kind."),
        ("row", "The object table row the error is about, such as fill.color."),
        ("sheet", "The worksheet the error is about."),
        ("source", "The source of a value, such as a variable or file."),
        ("subject", "What was looked up, such as sheet, page or file."),
        ("target", "The target the error is about."),
        ("targets", "The publication targets and their state; a target whose original was not moved back names where it is (originalPath)."),
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
        if (ErrorDetailKeys.Patterns.TryGetValue(key, out string? pattern))
        {
            property["type"] = "string";
            property["pattern"] = pattern;
        }

        return property;
    }
}
