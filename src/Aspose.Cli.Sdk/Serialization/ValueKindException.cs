using System.Text.Json;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// Rejects a value whose JSON kind the contract does not take, such as an object where a whole
/// number goes, naming the field by its path so the error can say what the field is.
/// </summary>
internal sealed class ValueKindException(string field, string message) : JsonException(message)
{
    /// <summary>The path of the field whose value has the wrong kind, such as <c>at</c> or <c>pageSetup.margins</c>.</summary>
    public string Field { get; } = field;
}
