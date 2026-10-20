using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The one shape of operation-document failures. Details always carry the operation's
/// <c>id</c> when one operation is at fault, the same id its <c>applied[]</c> entry carries,
/// and its <c>op</c> name when the entry names a known operation, so an agent can fix exactly
/// that entry. <c>details.reason</c> is one of <see cref="OpsInvalidReasons"/> and
/// <c>details.field</c> the path of the field it is about, when it is about one; the message
/// explains it and names the operation the same way, such as
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
    /// <param name="reason">One of <see cref="OpsInvalidReasons"/>.</param>
    /// <param name="explanation">Why, completing the message.</param>
    /// <param name="hint">The next step.</param>
    /// <param name="field">The path of the field it is about, if any.</param>
    /// <param name="allowed">The unknown or mistyped field's object and the fields it accepts, if any.</param>
    internal static CliException Invalid(string reason, string explanation, string hint, string? field = null, AllowedFieldsException? allowed = null)
    {
        var details = new JsonObject { ["reason"] = reason, ["field"] = field };
        AddAllowedFields(details, allowed);
        return CliException.Create(ErrorCodes.OpsInvalid, $"The operation document is invalid: {explanation}{Listed(allowed?.Mistake)}",
            hint: allowed?.Mistake?.Hint(hint) ?? hint, details: details);
    }

    /// <summary>Rejects the document as a whole for the serializer's rejection, by its reason and field.</summary>
    internal static CliException Invalid(JsonException rejection, string hint) =>
        Invalid(ReasonOf(rejection), rejection.Message, hint, FieldOf(rejection), rejection as AllowedFieldsException);

    /// <summary>
    /// Rejects one operation; <paramref name="name"/> is null when the entry names no known
    /// operation. An unknown or mistyped field adds details as the document's rejection does,
    /// and a value that names none of its allowed values adds them as <c>available</c> with the
    /// closest as <c>suggestions</c>.
    /// </summary>
    internal static CliException InvalidAt(
        string id, string? name, string reason, string explanation, string hint, string? field = null, ErrorCode? cause = null,
        AllowedFieldsException? allowed = null, Mistake? value = null, string? path = null)
    {
        JsonObject details = Identify(id, name);
        details["reason"] = reason;
        details["field"] = field;
        details["path"] = path;
        details["cause"] = cause?.Name;
        AddAllowedFields(details, allowed);
        value?.WriteTo(details);
        Mistake? mistake = value ?? allowed?.Mistake;
        return CliException.Create(ErrorCodes.OpsInvalid, $"{Subject(id, name)} is invalid: {explanation}{Listed(mistake)}",
            hint: mistake?.Hint(hint) ?? hint, details: details);
    }

    /// <summary>Rejects one operation the serializer could not read, by the rejection's reason and field.</summary>
    internal static CliException InvalidAt(string id, string? name, JsonException rejection, string hint) =>
        InvalidAt(id, name, ReasonOf(rejection), rejection.Message, hint, FieldOf(rejection), allowed: rejection as AllowedFieldsException);

    /// <summary>Rejects one operation whose value its constraints, its own check or its handler refused.</summary>
    internal static CliException InvalidAt(string id, string name, OperationInvalidException rejection, string defaultHint) =>
        InvalidAt(id, name, OpsInvalidReasons.InvalidValue, rejection.Message, rejection.Hint ?? defaultHint, rejection.Field,
            value: rejection.Mistake, path: rejection.Path);

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
    internal static CliException UnknownAt(string id, string explanation, string hint, Mistake operation)
    {
        JsonObject details = Identify(id, operation.Requested);
        details["reason"] = OpsInvalidReasons.UnknownOp;
        details["field"] = "op";
        operation.WriteTo(details);
        return CliException.Create(ErrorCodes.OpsInvalid, $"{Subject(id, name: null)} is invalid: {explanation}{Listed(operation)}",
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

    /// <summary>The reason of a serializer rejection: its own, or <c>invalid-value</c> for one that states none.</summary>
    private static string ReasonOf(JsonException rejection) =>
        rejection is ContractRejection stated ? stated.Reason : OpsInvalidReasons.InvalidValue;

    private static string? FieldOf(JsonException rejection) => (rejection as ContractRejection)?.Field;

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
