using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Errors;

/// <summary>
/// A request that names nothing that exists, such as a mistyped command, option, value, operation,
/// field or sheet: what was asked for and what exists. It is the one owner of how a mistake is
/// reported: <c>details.available</c> is written only here, so every error states a mistake the
/// same way.
/// </summary>
public sealed class Mistake
{
    private Mistake(string requested, IReadOnlyList<string> available)
    {
        Requested = requested;
        Available = available;
    }

    /// <summary>The name as the caller wrote it.</summary>
    public string Requested { get; }

    /// <summary>The names that exist, in the order the caller sees them.</summary>
    public IReadOnlyList<string> Available { get; }

    /// <summary>Describes a request that names nothing among <paramref name="available"/>.</summary>
    /// <param name="requested">The name as the caller wrote it.</param>
    /// <param name="available">The names that exist, in the order details list them.</param>
    public static Mistake Of(string requested, IEnumerable<string> available)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(available);
        return new Mistake(requested, [.. available]);
    }

    /// <summary>
    /// The sentence that lists the names an error's <c>details.available</c> holds, such as
    /// <c>Must be one of: light, dark</c>, so the message and the details list the same names.
    /// When more exist than an error lists, a sentence before it says how many.
    /// </summary>
    public string MustBeOneOf()
    {
        int listed = Math.Min(CliErrors.MaximumAvailableNames, Available.Count);
        string count = listed < Available.Count ? $"The first {listed} of {Available.Count} follow. " : string.Empty;
        return $"{count}Must be one of: {string.Join(", ", Available.Take(listed))}";
    }

    /// <summary>
    /// Writes the mistake into error details: <c>available</c>, the existing names. Every error
    /// lists at most <see cref="CliErrors.MaximumAvailableNames"/> names; when it lists fewer than
    /// exist, <c>availableCount</c> states how many do.
    /// </summary>
    public void WriteTo(JsonObject details)
    {
        ArgumentNullException.ThrowIfNull(details);
        int listed = Math.Min(CliErrors.MaximumAvailableNames, Available.Count);
        details["available"] = new JsonArray([.. Available.Take(listed).Select(static name => (JsonNode)name)]);
        if (listed < Available.Count)
        {
            details["availableCount"] = Available.Count;
        }
    }
}
