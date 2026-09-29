using System.Text.Json;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// An operation or nested record names a field its contract does not declare. The message is
/// the wire-terms reason; the fields the object accepts and the closest one travel with it so
/// the operation error can publish them as details.
/// </summary>
internal sealed class UnknownFieldException(string message, IReadOnlyList<string> allowedFields, string? suggestion)
    : JsonException(message)
{
    /// <summary>The object's accepted wire names: <c>op</c> and <c>id</c> first on an operation, then declaration order.</summary>
    public IReadOnlyList<string> AllowedFields { get; } = allowedFields;

    /// <summary>The accepted field the caller most likely meant, or null.</summary>
    public string? Suggestion { get; } = suggestion;
}
