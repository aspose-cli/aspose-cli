using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// A request that names nothing that exists, such as a mistyped command, option, value, operation,
/// field or sheet: what was asked for, what exists and the existing names closest to the request.
/// It is the one owner of how a mistake is reported: <c>details.available</c>,
/// <c>details.suggestions</c> and the "did you mean" text are written only here, so every error and
/// warning states a mistake the same way and a caller can correct it in one step.
/// </summary>
public sealed class Mistake
{
    private Mistake(string requested, IReadOnlyList<string> available, IReadOnlyList<string> suggestions)
    {
        Requested = requested;
        Available = available;
        Suggestions = suggestions;
    }

    /// <summary>The name as the caller wrote it.</summary>
    public string Requested { get; }

    /// <summary>The names that exist, in the order the caller sees them.</summary>
    public IReadOnlyList<string> Available { get; }

    /// <summary>
    /// Up to three existing names the caller most likely
    /// meant, best first; empty when none is close.
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; }

    /// <summary>The question that names the suggestions, such as <c>Did you mean 'Sales'?</c>, or null when none is close.</summary>
    public string? Question => Suggestions.Count == 0 ? null : $"Did you mean {Listed()}?";

    /// <summary>
    /// Describes a request that names nothing among <paramref name="available"/>.
    /// </summary>
    /// <param name="requested">The name as the caller wrote it.</param>
    /// <param name="available">The names that exist, in the order ties keep and details list them.</param>
    /// <param name="meant">
    /// A name known to be meant, such as the one that declares <paramref name="requested"/> a
    /// common mistake for it; it leads the suggestions. Null to rank by closeness alone.
    /// </param>
    /// <param name="fieldNames">
    /// Whether the names are compound field names such as <c>fontSize</c>, whose last word says
    /// what the field is, so a name that ends the other ranks first.
    /// </param>
    /// <param name="keyOf">
    /// The part of a name that is compared, such as an option without its dashes or a command path
    /// by its last word; it applies to <paramref name="requested"/> too. By default the whole name.
    /// </param>
    public static Mistake Of(
        string requested,
        IEnumerable<string> available,
        string? meant = null,
        bool fieldNames = false,
        Func<string, string>? keyOf = null)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(available);

        string[] names = [.. available];
        keyOf ??= static name => name;
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            byKey.TryAdd(keyOf(name), name);
        }

        IEnumerable<string> closest = NameSuggestions.Closest(keyOf(requested), byKey.Keys, fieldNames)
            .Select(key => byKey[key]);
        string[] suggestions =
        [
            .. (meant is null ? closest : closest.Prepend(meant))
                .Distinct(StringComparer.Ordinal)
                .Take(NameSuggestions.MaximumSuggestions),
        ];
        return new Mistake(requested, names, suggestions);
    }

    /// <summary>The hint for the mistake: the <see cref="Question"/>, when there is one, before <paramref name="next"/>.</summary>
    /// <param name="next">The next step that holds whether or not a name is close.</param>
    public string Hint(string next) => Question is { } question ? $"{question} {next}" : next;

    /// <summary>
    /// The suggestions as an aside that follows a name in running text, such as
    /// <c> (did you mean 'Sales'?)</c>, or an empty string when none is close.
    /// </summary>
    /// <param name="kind">What a suggestion is, such as <c>its unused key</c>, placed before the names; null for none.</param>
    public string Aside(string? kind = null) =>
        Suggestions.Count == 0 ? string.Empty : $" (did you mean {(kind is null ? string.Empty : kind + " ")}{Listed()}?)";

    /// <summary>
    /// Writes the mistake into error details: <c>available</c>, the first
    /// <paramref name="maximumAvailable"/> existing names (none when it is 0), and
    /// <c>suggestions</c>, an array present only when some name is close. Every error lists at
    /// most <see cref="CliErrors.MaximumAvailableNames"/> names; when it lists fewer than exist,
    /// <c>availableCount</c> states how many do.
    /// </summary>
    public void WriteTo(JsonObject details, int maximumAvailable = CliErrors.MaximumAvailableNames)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumAvailable);
        if (maximumAvailable > 0)
        {
            int listed = Math.Min(Math.Min(maximumAvailable, CliErrors.MaximumAvailableNames), Available.Count);
            details["available"] = new JsonArray([.. Available.Take(listed).Select(static name => (JsonNode)name)]);
            if (listed < Available.Count)
            {
                details["availableCount"] = Available.Count;
            }
        }

        if (Suggestions.Count > 0)
        {
            details["suggestions"] = new JsonArray([.. Suggestions.Select(static name => (JsonNode)name)]);
        }
    }

    private string Listed()
    {
        string[] quoted = [.. Suggestions.Select(static name => $"'{name}'")];
        return quoted.Length == 1 ? quoted[0] : $"{string.Join(", ", quoted[..^1])} or {quoted[^1]}";
    }
}
