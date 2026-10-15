using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The one shape of operation-document failures. Details always carry the operation's
/// <c>id</c> when one operation is at fault, the same id its <c>applied[]</c> entry carries,
/// and its <c>op</c> name when the entry names a known operation, so an agent can fix exactly
/// that entry. The message names the operation the same way, such as
/// <c>Operation op-0002 (set_values)</c>. A list of names the caller can choose from appears
/// once, as <c>details.available</c>, and the message's last sentence names the same list.
/// </summary>
internal static class OperationErrors
{
    /// <summary>
    /// Rejects the document as a whole. An unknown field, or another kind of value where an
    /// object belongs, lists the fields the object accepts in <c>available</c> and, when some
    /// are likely meant, names them in <c>suggestions</c>.
    /// </summary>
    internal static CliException Invalid(string reason, string hint, AllowedFieldsException? field = null)
    {
        var details = new JsonObject { ["reason"] = reason };
        AddAllowedFields(details, field);
        return CliException.Create(ErrorCodes.OpsInvalid, $"The operation document is invalid: {reason}{Listed(field?.Mistake)}",
            hint: field?.Mistake?.Hint(hint) ?? hint, details: details);
    }

    /// <summary>
    /// Rejects one operation; <paramref name="name"/> is null when the entry names no known
    /// operation. An unknown or mistyped field adds details as <see cref="Invalid"/> does, and a
    /// value that names none of its allowed values adds them as <c>available</c> with the closest
    /// as <c>suggestions</c>.
    /// </summary>
    internal static CliException InvalidAt(
        string id, string? name, string reason, string hint, ErrorCode? cause = null,
        AllowedFieldsException? field = null, Mistake? value = null)
    {
        JsonObject details = Identify(id, name);
        details["reason"] = reason;
        if (cause is not null)
        {
            details["cause"] = cause.Name;
        }
        AddAllowedFields(details, field);
        value?.WriteTo(details);
        Mistake? mistake = value ?? field?.Mistake;
        return CliException.Create(ErrorCodes.OpsInvalid, $"{Subject(id, name)} is invalid: {reason}{Listed(mistake)}",
            hint: mistake?.Hint(hint) ?? hint, details: details);
    }

    /// <summary>
    /// Rejects every invalid operation of a batch at once, and every unknown field of each, so a
    /// caller fixes them in one pass. With more than one failure, the first leads the details and
    /// <c>details.errors</c> lists the details of each; the message counts them and quotes the first.
    /// </summary>
    internal static CliException InvalidAll(IReadOnlyList<CliException> failures)
    {
        CliException first = failures[0];
        if (failures.Count == 1)
        {
            return first;
        }

        JsonObject details = first.Details!.DeepClone().AsObject();
        details["errors"] = new JsonArray([.. failures.Select(static failure => failure.Details!.DeepClone())]);
        int operations = failures.Select(static failure => failure.Details?["id"]?.GetValue<string>()).Distinct(StringComparer.Ordinal).Count();
        string counted = operations == failures.Count ? $"{failures.Count} operations are invalid"
            : operations == 1 ? $"{failures.Count} problems make one operation invalid"
            : $"{failures.Count} problems make {operations} operations invalid";
        return CliException.Create(
            ErrorCodes.OpsInvalid,
            $"{counted}, each listed in details.errors; the first: {first.Message}",
            hint: first.Hint, details: details);
    }

    /// <summary>
    /// Rejects an entry whose op names no operation of the vocabulary, listing the operations in
    /// <c>available</c> and, when some are likely meant, naming them in <c>suggestions</c>;
    /// <c>details.op</c> is the op name as the entry wrote it, so the caller finds the entry.
    /// </summary>
    internal static CliException UnknownAt(string id, string reason, string hint, Mistake operation)
    {
        JsonObject details = Identify(id, operation.Requested);
        details["reason"] = reason;
        operation.WriteTo(details);
        return CliException.Create(ErrorCodes.OpsInvalid, $"{Subject(id, name: null)} is invalid: {reason}{Listed(operation)}",
            hint: operation.Hint(hint), details: details);
    }

    /// <summary>Keeps a domain failure's own code (for example SHEET_NOT_FOUND) and names the operation.</summary>
    internal static CliException FailedAt(string id, string name, CliException cause)
    {
        JsonObject details = cause.Details?.DeepClone().AsObject() ?? [];
        details["id"] = id;
        details["op"] = name;
        return CliException.Create(cause.Code, $"{Subject(id, name)} failed: {cause.Message}",
            hint: cause.Hint, details: details, innerException: cause);
    }

    /// <summary>
    /// An engine failure can leave the document half changed, so the batch stops in every mode
    /// and publishes nothing; the message says so because best-effort otherwise continues.
    /// </summary>
    internal static CliException EngineFailedAt(string id, string name, EngineOpException failure) => EngineErrors.EngineFailed(
        $"{Subject(id, name)} failed inside the document engine, so the batch stopped and nothing was written: {failure.Message}",
        failure,
        Identify(id, name));

    private static string Subject(string id, string? name) => name is null ? $"Operation {id}" : $"Operation {id} ({name})";

    private static JsonObject Identify(string id, string? name)
    {
        var details = new JsonObject { ["id"] = id };
        if (name is not null)
        {
            details["op"] = name;
        }

        return details;
    }

    /// <summary>The sentence that names the choices of a mistake, after the reason; empty without one.</summary>
    private static string Listed(Mistake? mistake) => mistake is null ? string.Empty : $". {mistake.MustBeOneOf()}";

    private static void AddAllowedFields(JsonObject details, AllowedFieldsException? field)
    {
        if (field is null)
        {
            return;
        }

        if (field.Mistake is { } mistake)
        {
            mistake.WriteTo(details);
            return;
        }

        int listed = Math.Min(CliErrors.MaximumAvailableNames, field.AllowedFields.Count);
        details["available"] = new JsonArray([.. field.AllowedFields.Take(listed).Select(static item => (JsonNode)item)]);
        if (listed < field.AllowedFields.Count)
        {
            details["availableCount"] = field.AllowedFields.Count;
        }
    }
}
