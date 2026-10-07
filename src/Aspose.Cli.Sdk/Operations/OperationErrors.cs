using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The one shape of operation-document failures. Details always carry the operation's
/// <c>index</c> when one operation is at fault, and its <c>op</c> name when the entry names a
/// known operation, so an agent can fix exactly that entry.
/// </summary>
internal static class OperationErrors
{
    /// <summary>
    /// Rejects the document as a whole. An unknown field, or another kind of value where an
    /// object belongs, adds <c>allowedFields</c> and, when some are likely meant, <c>suggestions</c>.
    /// </summary>
    internal static CliException Invalid(string reason, string hint, AllowedFieldsException? field = null)
    {
        var details = new JsonObject { ["reason"] = reason };
        AddAllowedFields(details, field);
        return new CliException(ErrorCodes.OpsInvalid, $"The operation document is invalid: {reason}",
            hint: field?.Mistake?.Hint(hint) ?? hint, details: details);
    }

    /// <summary>
    /// Rejects one operation; <paramref name="name"/> is null when the entry names no known
    /// operation. An unknown or mistyped field adds details as <see cref="Invalid"/> does, and a
    /// value that names none of its allowed values adds them as <c>available</c> with the closest
    /// as <c>suggestions</c>.
    /// </summary>
    internal static CliException InvalidAt(
        int index, string? name, string reason, string hint, ErrorCode? cause = null,
        AllowedFieldsException? field = null, Mistake? value = null)
    {
        var details = new JsonObject { ["index"] = index };
        if (name is not null)
        {
            details["op"] = name;
        }
        details["reason"] = reason;
        if (cause is not null)
        {
            details["cause"] = cause.Name;
        }
        AddAllowedFields(details, field);
        value?.WriteTo(details);
        Mistake? mistake = value ?? field?.Mistake;
        string subject = name is null ? $"Operation {index}" : $"Operation {index} ({name})";
        return new CliException(ErrorCodes.OpsInvalid, $"{subject} is invalid: {reason}",
            hint: mistake?.Hint(hint) ?? hint, details: details);
    }

    /// <summary>
    /// Rejects every invalid operation of a batch at once, so a caller fixes them in one pass.
    /// The first failure leads; with more than one, <c>details.errors</c> lists the details of each.
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
        int more = failures.Count - 1;
        string others = more == 1 ? "1 more operation is invalid" : $"{more} more operations are invalid";
        return new CliException(ErrorCodes.OpsInvalid, $"{first.Message}; {others}, listed in details.errors",
            hint: first.Hint, details: details);
    }

    /// <summary>
    /// Rejects an entry whose op names no operation of the vocabulary, listing the operations in
    /// <c>available</c> and, when some are likely meant, naming them in <c>suggestions</c>.
    /// </summary>
    internal static CliException UnknownAt(int index, string reason, string hint, Mistake operation)
    {
        var details = new JsonObject
        {
            ["index"] = index,
            ["reason"] = reason,
        };
        operation.WriteTo(details);
        return new CliException(ErrorCodes.OpsInvalid, $"Operation {index} is invalid: {reason}",
            hint: operation.Hint(hint), details: details);
    }

    private static void AddAllowedFields(JsonObject details, AllowedFieldsException? field)
    {
        if (field is null)
        {
            return;
        }

        details["allowedFields"] = new JsonArray([.. field.AllowedFields.Select(static item => (JsonNode)item)]);
        field.Mistake?.WriteTo(details, maximumAvailable: 0);
    }

    /// <summary>Keeps a domain failure's own code (for example SHEET_NOT_FOUND) and adds its position.</summary>
    internal static CliException FailedAt(int index, string name, CliException cause)
    {
        JsonObject details = cause.Details?.DeepClone().AsObject() ?? [];
        details["index"] = index;
        details["op"] = name;
        return new CliException(cause.Code, $"Operation {index} ({name}) failed: {cause.Message}",
            hint: cause.Hint, details: details, innerException: cause);
    }

    /// <summary>
    /// An engine failure can leave the document half changed, so the batch stops in every mode
    /// and publishes nothing; the message says so because best-effort otherwise continues.
    /// </summary>
    internal static CliException EngineFailedAt(int index, string name, EngineOpException failure) => CliErrors.EngineFailed(
        $"Operation {index} ({name}) failed inside the document engine, so the batch stopped and nothing was written: {failure.Message}",
        failure,
        new JsonObject { ["index"] = index, ["op"] = name });
}
