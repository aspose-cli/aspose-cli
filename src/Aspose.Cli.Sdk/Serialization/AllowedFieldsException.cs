using System.Text.Json;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// An object of the operation document names a field its contract does not declare, or a field
/// whose contract is an object holds another kind of value. The message is the wire-terms
/// reason; the fields the object accepts and the one most likely meant travel with it so the
/// operation error can publish them as details.
/// </summary>
internal sealed class AllowedFieldsException : JsonException
{
    private AllowedFieldsException(string message, IReadOnlyList<string> allowedFields, string? suggestion)
        : base(message)
    {
        AllowedFields = allowedFields;
        Suggestion = suggestion;
    }

    /// <summary>The object's accepted wire names in published order.</summary>
    public IReadOnlyList<string> AllowedFields { get; }

    /// <summary>The accepted field the caller most likely meant, or null.</summary>
    public string? Suggestion { get; }

    /// <summary>
    /// Rejects the member <paramref name="name"/> of the object at <paramref name="path"/>,
    /// naming the fields the object accepts and the one most likely meant: the closest name, or
    /// else the only required field missing.
    /// </summary>
    /// <param name="path">The object's field path; empty for an operation or the document itself.</param>
    /// <param name="name">The unknown member's name.</param>
    /// <param name="subject">How the reason names the object, such as an operation name or a field path.</param>
    /// <param name="allowedFields">The object's accepted wire names in published order.</param>
    /// <param name="missingRequired">The object's required fields the value omits.</param>
    public static AllowedFieldsException UnknownField(
        string path, string name, string subject, IReadOnlyList<string> allowedFields, IReadOnlyCollection<string> missingRequired)
    {
        string? suggestion = NameSuggestions.Closest(name, allowedFields).FirstOrDefault()
            ?? (missingRequired.Count == 1 ? missingRequired.First() : null);
        string field = path.Length == 0 ? name : $"{path}.{name}";
        string reason = $"unknown field '{field}'; {subject} accepts: {string.Join(", ", allowedFields)}"
            + (suggestion is null ? string.Empty : $" (did you mean '{suggestion}'?)");
        return new AllowedFieldsException(reason, allowedFields, suggestion);
    }

    /// <summary>Rejects a value of another kind where the contract expects an object with the given fields.</summary>
    /// <param name="path">The value's field path; never empty.</param>
    /// <param name="allowedFields">The object's accepted wire names in published order.</param>
    public static AllowedFieldsException NotAnObject(string path, IReadOnlyList<string> allowedFields) =>
        new($"{path} must be an object with the fields: {string.Join(", ", allowedFields)}", allowedFields, suggestion: null);
}
