using System.Text.Json;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Serialization;

/// <summary>
/// An object of the operation document names a field its contract does not declare, or a field
/// whose contract is an object holds another kind of value. The message is the wire-terms
/// reason without the list of fields; the fields the object accepts and the mistake, with the
/// fields most likely meant, travel with it so the operation error lists them once, as
/// <c>details.available</c> and in the sentence that names them.
/// </summary>
internal sealed class AllowedFieldsException : ContractRejection
{
    private AllowedFieldsException(string reason, string field, string message, IReadOnlyList<string> allowedFields, Mistake? mistake)
        : base(reason, field, message)
    {
        AllowedFields = allowedFields;
        Mistake = mistake;
    }

    /// <summary>The object's accepted wire names in published order.</summary>
    public IReadOnlyList<string> AllowedFields { get; }

    /// <summary>The unknown field and the accepted fields most likely meant; null for a value of another kind.</summary>
    public Mistake? Mistake { get; }

    /// <summary>
    /// Rejects the member <paramref name="name"/> of the object at <paramref name="path"/>,
    /// naming the fields the object accepts and the ones most likely meant: the field declared
    /// for the mistaken name first, then the closest names, else the only required field missing.
    /// </summary>
    /// <param name="path">The object's field path; empty for an operation or the document itself.</param>
    /// <param name="name">The unknown member's name.</param>
    /// <param name="allowedFields">The object's accepted wire names in published order.</param>
    /// <param name="missingRequired">The object's required fields the value omits.</param>
    /// <param name="meant">The field that declares <paramref name="name"/> a common mistake for it, or null.</param>
    public static AllowedFieldsException UnknownField(
        string path,
        string name,
        IReadOnlyList<string> allowedFields,
        IReadOnlyCollection<string> missingRequired,
        string? meant = null)
    {
        var mistake = Mistake.Of(name, allowedFields, meant, fieldNames: true);
        if (mistake.Suggestions.Count == 0 && missingRequired.Count == 1)
        {
            mistake = Mistake.Of(name, allowedFields, missingRequired.First(), fieldNames: true);
        }

        string field = path.Length == 0 ? name : $"{path}.{name}";
        return new AllowedFieldsException(OpsInvalidReasons.UnknownField, field, $"unknown field '{field}'", allowedFields, mistake);
    }

    /// <summary>Rejects a value of another kind where the contract expects an object with the given fields.</summary>
    /// <param name="path">The value's field path; never empty.</param>
    /// <param name="allowedFields">The object's accepted wire names in published order.</param>
    public static AllowedFieldsException NotAnObject(string path, IReadOnlyList<string> allowedFields) =>
        new(OpsInvalidReasons.WrongType, path, $"{path} must be an object; details.available lists its fields", allowedFields, mistake: null);
}

/// <summary>
/// Several unknown fields of one operation entry, each an <see cref="AllowedFieldsException"/>,
/// so the operation error lists every one of them.
/// </summary>
internal sealed class UnknownFieldsException(IReadOnlyList<AllowedFieldsException> fields)
    : JsonException(string.Join("; ", fields.Select(static field => field.Message)))
{
    /// <summary>The unknown fields in the entry's order.</summary>
    public IReadOnlyList<AllowedFieldsException> Fields { get; } = fields;
}
