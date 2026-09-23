using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Operations;

/// <summary>
/// The one shape of operation-document failures. Details always carry the operation's
/// <c>index</c> and <c>op</c> name when one operation is at fault, so an agent can fix
/// exactly that entry.
/// </summary>
internal static class OperationErrors
{
    internal static CliException Invalid(string reason, string hint) => new(
        ErrorCodes.OpsInvalid,
        $"The operation document is invalid: {reason}",
        hint: hint,
        details: new JsonObject { ["reason"] = reason });

    internal static CliException InvalidAt(int index, string name, string reason, string hint, ErrorCode? cause = null)
    {
        var details = new JsonObject { ["index"] = index, ["op"] = name, ["reason"] = reason };
        if (cause is not null)
        {
            details["cause"] = cause.Name;
        }
        return new CliException(ErrorCodes.OpsInvalid, $"Operation {index} ({name}) is invalid: {reason}", hint: hint, details: details);
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

    internal static CliException EngineFailedAt(int index, string name, EngineOpException failure) => new(
        ErrorCodes.FeatureUnsupported,
        $"Operation {index} ({name}) could not be applied by the document engine: {failure.Message}",
        hint: "Nothing was written. Simplify or remove this operation, or try a standard copy of the document.",
        details: new JsonObject { ["index"] = index, ["op"] = name },
        innerException: failure);
}
