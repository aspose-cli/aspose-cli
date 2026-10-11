using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// The dictionary of <c>error.details</c> keys: one key per concept, each with the description
/// the error schema publishes for it. Budgets state <c>limit</c>, <c>actual</c> and <c>unit</c>;
/// collections state <c>available</c> and <c>suggestions</c>; an operation error states the
/// operation's <c>id</c> and <c>op</c>; a positional argument is <c>argument</c>, an option
/// <c>option</c>. A details object holds no key outside it and no null value.
/// </summary>
public static class ErrorDetailKeys
{
    /// <summary>Every key with its description.</summary>
    public static FrozenDictionary<string, string> All { get; } = new (string Key, string Description)[]
    {
        ("actual", "The measured amount of a budget, in unit."),
        ("actualExists", "Whether the output existed when publication found it changed."),
        ("actualSha256", "The SHA-256 of the output publication found."),
        ("actualSizeBytes", "The size of the output publication found."),
        ("argument", "The positional argument the error is about, such as file."),
        ("available", "The names or values that exist or are accepted, at most 50."),
        ("availableCount", "How many names or values exist or are accepted."),
        ("cause", "Why an operation field was refused."),
        ("declared", "The product the command or extension declares."),
        ("detected", "The products whose formats the content matches."),
        ("diagnosticId", "The host diagnostic id of a startup or process failure."),
        ("directory", "The directory the error is about."),
        ("dpi", "The resolution of a render, in dots per inch."),
        ("engineMessage", "The document engine's own message."),
        ("errors", "Every problem, when there are several."),
        ("expectedBytes", "The size an input had when it was admitted."),
        ("expectedExists", "Whether the output existed when the command read it."),
        ("expectedSha256", "The SHA-256 the file had when the command read it."),
        ("expectedSizeBytes", "The size the output had when the command read it."),
        ("extension", "The file extension the error is about."),
        ("feature", "The feature the product or format does not support."),
        ("feed", "The release feed the update read."),
        ("field", "The operation field the error is about."),
        ("format", "The format id the error is about."),
        ("height", "The height of a render, in pixels."),
        ("id", "The id of the failing operation, as its applied[] entry states it."),
        ("limit", "The budget's limit, in unit."),
        ("line", "The line of a text input the error is about."),
        ("observedBytes", "The size an input had when it was read again."),
        ("observedSha256", "The SHA-256 an input had when it was read again."),
        ("offset", "The byte offset of the problem in an input."),
        ("op", "The op name of the failing operation."),
        ("operation", "The product feature the error is about."),
        ("option", "The option the error is about, such as --pages."),
        ("originalError", "The error that stopped a publication: its code and details."),
        ("path", "The absolute path of the file the error is about."),
        ("phase", "The stage of processing that failed."),
        ("pid", "The id of the process the error is about."),
        ("port", "The loopback port the error is about."),
        ("problems", "The problems the engine reported."),
        ("product", "The product the error is about."),
        ("profile", "The conformance profile the error is about."),
        ("quiescenceConfirmed", "Whether the stopped worker was confirmed to have ended."),
        ("range", "The range or selection as the caller wrote it."),
        ("reason", "Why the value or file was refused."),
        ("recoveryComplete", "Whether every publication target was restored."),
        ("requested", "The name, number, value or path as the caller wrote it."),
        ("resource", "The budget's resource kind."),
        ("sample", "An excerpt of the input the error is about."),
        ("sheet", "The worksheet the error is about."),
        ("source", "The source of a value, such as a variable or file."),
        ("stage", "The stage of the worker that failed."),
        ("subject", "What was looked up, such as sheet, page or file."),
        ("suggestions", "The accepted names closest to the request, best first."),
        ("target", "The target the error is about."),
        ("targets", "The publication targets and their state."),
        ("timeoutSeconds", "The timeout that elapsed, in seconds."),
        ("unit", "The unit of a budget's limit and actual."),
        ("variable", "The environment variable the error is about."),
        ("width", "The width of a render, in pixels."),
    }.ToFrozenDictionary(static key => key.Key, static key => key.Description, StringComparer.Ordinal);
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
                .Select(static key => KeyValuePair.Create(key.Key, (JsonNode?)new JsonObject { ["description"] = key.Value })));
    }
}
