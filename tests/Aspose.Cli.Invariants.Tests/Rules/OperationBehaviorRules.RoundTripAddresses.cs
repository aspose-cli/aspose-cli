using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// One way a product's reads report an item: places matching <see cref="Place"/> belong to the
/// items <see cref="Items"/> names, as <c>&lt;unit&gt;/&lt;address&gt;</c> the way a target names
/// them; no item when the place is derived from others, such as a count or a preview.
/// </summary>
/// <param name="Place">The place, <c>&lt;command&gt;: &lt;path&gt;</c>, with groups the items use.</param>
/// <param name="Items">The items of a match, by its groups (the whole place first) and a lookup of a path in the same read result.</param>
/// <param name="When">Whether the rule applies to a match; null when it applies to every one.</param>
/// <param name="Keyed">
/// Whether the item's address is made of the values the place reports, such as a merged range:
/// a change of them removes the item at the address before it and adds one at the address after
/// it, so the place belongs to both.
/// </param>
internal sealed record ReadItem(Regex Place, Func<string[], Func<string, string?>, IEnumerable<string>> Items, Func<string[], Func<string, string?>, bool>? When = null, bool Keyed = false);

// C-R39: the items whose reads changed, named as the targets of an edit name them (AGENTS.md
// "Targets"). One table per product, in one place; a changed place no rule names is reported.
internal static partial class ContractRules
{
    /// <summary>The items of each product's reads, first match wins.</summary>
    internal static IReadOnlyDictionary<string, ReadItem[]> ReadItems { get; } = new Dictionary<string, ReadItem[]>(StringComparer.Ordinal)
    {
    };

    private static ReadItem Item([StringSyntax(StringSyntaxAttribute.Regex)] string place, Func<string[], Func<string, string?>, IEnumerable<string>> items) =>
        new(new Regex(place, RegexOptions.CultureInvariant), items);

    /// <summary>A rule that applies only where <paramref name="when"/> holds; another rule names the other places.</summary>
    private static ReadItem Item(
        [StringSyntax(StringSyntaxAttribute.Regex)] string place, Func<string[], Func<string, string?>, IEnumerable<string>> items, Func<string[], Func<string, string?>, bool> when) =>
        new(new Regex(place, RegexOptions.CultureInvariant), items, when);

    private static ReadItem Derived([StringSyntax(StringSyntaxAttribute.Regex)] string place) =>
        new(new Regex(place, RegexOptions.CultureInvariant), static (_, _) => []);

    /// <summary>
    /// The items a changed place of a read belongs to, by the product's <see cref="ReadItems"/>,
    /// looking its names up in the read after the change, or before it for an item the change
    /// removed, and for a keyed item in both; none for a derived place, and null for a place no
    /// rule names.
    /// </summary>
    internal static IEnumerable<string>? RoundTripAddress(string product, string change, JsonArray read, JsonArray staged)
    {
        int colon = change.IndexOf(": ", StringComparison.Ordinal);
        string command = change[..colon];
        string? Lookup(string path) => LookupIn(path, read, staged);
        string? Before(string path) => LookupIn(path, staged, read);
        string? LookupIn(string path, params JsonArray[] states)
        {
            foreach (JsonArray state in states)
            {
                JsonNode? result = state.FirstOrDefault(item => item!["command"]!.GetValue<string>() == command)?["result"];
                if (result is not null && ScenarioRunner.TryResolve(result, path, out JsonNode? value) && value is JsonValue scalar)
                {
                    return scalar.ToJsonString().Trim('"');
                }
            }
            return null;
        }
        foreach (ReadItem rule in ReadItems.GetValueOrDefault(product) ?? [])
        {
            if (rule.Place.Match(change) is { Success: true } match
                && match.Groups.Cast<Group>().Select(static group => group.Value).ToArray() is var groups
                && rule.When?.Invoke(groups, Lookup) != false)
            {
                string[] items = rule.Keyed
                    ? [.. rule.Items(groups, Lookup).Union(rule.Items(groups, Before), StringComparer.Ordinal)]
                    : [.. rule.Items(groups, Lookup)];
                // A name the lookup could not find leaves the address incomplete: no rule names it.
                return items.Any(static item => item.Contains("//", StringComparison.Ordinal) || item.EndsWith('/') || item.EndsWith('!')) ? null : items;
            }
        }
        return null;
    }

    /// <summary>
    /// A read with its style pool resolved into the cells that use it, so that a change of the
    /// pool's numbering is no change and a changed style is a change of each cell that uses it.
    /// </summary>
    internal static JsonArray ResolveStyles(JsonArray state) =>
        [.. state.Select(static item =>
        {
            var copy = (JsonObject)item!.DeepClone();
            if (copy["result"] is JsonObject result && result["styles"] is JsonObject pool)
            {
                result.Remove("styles");
                Inline(result, pool);
            }
            return (JsonNode)copy;
        })];

    private static void Inline(JsonNode? node, JsonObject pool)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["styleId"]?.GetValue<string>() is { } id)
                {
                    obj["styleId"] = pool[id]?.DeepClone();
                }
                foreach ((_, JsonNode? value) in obj.ToArray())
                {
                    Inline(value, pool);
                }
                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    Inline(item, pool);
                }
                break;
        }
    }
}
