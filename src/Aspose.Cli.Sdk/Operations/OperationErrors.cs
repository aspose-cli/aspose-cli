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
    internal static CliException Invalid(string reason, string hint) => new(
        ErrorCodes.OpsInvalid,
        $"The operation document is invalid: {reason}",
        hint: hint,
        details: new JsonObject { ["reason"] = reason });

    /// <summary>
    /// Rejects one operation; <paramref name="name"/> is null when the entry names no known
    /// operation. An unknown field adds <c>allowedFields</c> and, when one is likely meant,
    /// <c>suggestion</c>.
    /// </summary>
    internal static CliException InvalidAt(
        int index, string? name, string reason, string hint, ErrorCode? cause = null, UnknownFieldException? field = null)
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
        if (field is not null)
        {
            details["allowedFields"] = new JsonArray([.. field.AllowedFields.Select(static item => (JsonNode)item)]);
            if (field.Suggestion is not null)
            {
                details["suggestion"] = field.Suggestion;
            }
        }
        string subject = name is null ? $"Operation {index}" : $"Operation {index} ({name})";
        return new CliException(ErrorCodes.OpsInvalid, $"{subject} is invalid: {reason}", hint: hint, details: details);
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
