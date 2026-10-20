using System.Text.Json;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// A JSON value its contract refuses, stated in wire terms: the message explains it, and the
/// operation error reports <see cref="Reason"/> as <c>details.reason</c> and
/// <see cref="Field"/> as <c>details.field</c>.
/// </summary>
/// <param name="reason">One of <see cref="OpsInvalidReasons"/>.</param>
/// <param name="field">The path of the field it is about, such as <c>style.color</c>; empty or null for the document or an operation as a whole.</param>
/// <param name="message">The explanation, in wire terms.</param>
internal class ContractRejection(string reason, string? field, string message) : JsonException(message)
{
    /// <summary>Why the value was refused, one of <see cref="OpsInvalidReasons"/>.</summary>
    public string Reason { get; } = reason;

    /// <summary>The path of the field it is about; null for the document or an operation as a whole.</summary>
    public string? Field { get; } = string.IsNullOrEmpty(field) ? null : field;
}

/// <summary>
/// Rejects a value whose JSON kind the contract does not take, such as an object where a whole
/// number goes, naming the field by its path, such as <c>at</c> or <c>pageSetup.margins</c>, so
/// the error can say what the field is.
/// </summary>
internal sealed class ValueKindException(string field, string message)
    : ContractRejection(OpsInvalidReasons.WrongType, field, message);
