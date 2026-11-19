using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// C-R44: a set writes what is there. A set at an address that names no object of the document is
// refused with ADDRESS_NOT_FOUND, which names the operation and the address, and writes nothing,
// rather than making an object to write into.
internal static partial class ContractRules
{
    /// <summary>The key each part of an address form takes so that it names no object of a document.</summary>
    private static readonly Dictionary<string, string> MissingKeys = new(StringComparer.Ordinal)
    {
        ["<id>"] = "4294967295",
        ["<index>"] = "9999",
        ["<guid>"] = "00000000-0000-0000-0000-000000000000",
        ["<name>"] = "NoSuch",
    };

    /// <summary>The pattern of an address the part of an address form matches.</summary>
    private static readonly Dictionary<string, string> KeyPatterns = new(StringComparer.Ordinal)
    {
        ["<id>"] = @"\d+",
        ["<index>"] = @"\d+",
        ["<guid>"] = @"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}",
        ["<name>"] = "[A-Za-z][A-Za-z0-9._-]*",
    };

    /// <summary>The addresses a product's read lists for its rich fixture, in document order, read once per test process.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<(string[]? Addresses, string? Failure)>> RichAddresses = new(StringComparer.Ordinal);

    /// <summary>
    /// Every kind of a product's object table, the definitions of its ops schema
    /// (<c>v2/&lt;product&gt;/ops</c>) that declare <c>x-addresses</c>, with a row that can be set
    /// (one not <c>readOnly</c>): a set of the kind's first settable row, with a value of the row's
    /// schema, at an address of the kind's first address form that names no object, is refused
    /// with exit 4 and <c>ADDRESS_NOT_FOUND</c>, whose <c>details.index</c> is 0 and
    /// <c>details.at</c> the address, and the edit writes no file. The edit runs on the product's
    /// rich fixture, and only the kind's own step of the address names nothing there (see
    /// <see cref="MissingAddress"/>). A kind whose address form has no step, the document itself,
    /// always exists and has no case.
    /// </summary>
    private static IEnumerable<InvariantCase> R44Run(CliCatalog catalog)
    {
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            string schemaId = $"v2/{product.Id}/ops";
            if (!PublishedSchema.Ids(catalog).Contains(schemaId, StringComparer.Ordinal)
                || PublishedSchema.Document(schemaId)["$defs"] is not JsonObject definitions)
            {
                continue;
            }
            bool fast = true;
            foreach ((string kind, JsonNode? definition) in definitions)
            {
                if (definition?["x-addresses"] is not JsonArray { Count: > 0 } forms
                    || forms[0]!.GetValue<string>().Split('/') is not { } form
                    || !form.Any(IsKeyPart)
                    || (definition["properties"] as JsonObject ?? new JsonObject()).FirstOrDefault(static row => row.Value?["readOnly"]?.GetValue<bool>() != true) is not { Key: { } row, Value: { } schema })
                {
                    continue;
                }
                JsonNode? value = SchemaValue(schema);
                yield return Check("C-R44", $"{product.Id} set {kind} at a missing address",
                    () => MissingAddress(product.Id, form, row, value), shard: product.Id, slow: !fast);
                fast = false;
            }
        }
    }

    /// <summary>
    /// The violations of one kind's case. The address is the form with each step before the kind's
    /// own (its last segment and the keys after it) filled from an address the fixture's read
    /// lists: one that holds objects of the kind where some does, else, for a keyed step, the one
    /// with the most objects below it, and for a step without a key one that holds no such object.
    /// The kind's own keys are <see cref="MissingKeys"/>; a step without a key names nothing in a
    /// parent that holds none.
    /// </summary>
    private static IEnumerable<string> MissingAddress(string product, string[] form, string row, JsonNode? value)
    {
        int own = Array.FindLastIndex(form, static part => !IsKeyPart(part));
        string segment = form[own];
        string[] ownKeys = [.. form[(own + 1)..].Where(static part => !part.StartsWith('[')).Select(part => MissingKeys[part])];
        string? parent = null;
        if (own > 0)
        {
            (string[]? addresses, string? failure) = RichAddresses.GetOrAdd(product, static key => new Lazy<(string[]?, string?)>(() => ReadRichAddresses(key))).Value;
            if (failure is not null)
            {
                yield return failure;
                yield break;
            }
            if (addresses is null)
            {
                yield break;
            }
            var pattern = new Regex("^" + FormPattern(form[..own]) + "$", RegexOptions.CultureInvariant);
            string[] candidates = [.. addresses.Where(address => pattern.IsMatch(address))];
            string[] Below(string candidate) => [.. addresses.Where(address => address.StartsWith(candidate + "/", StringComparison.Ordinal))];
            parent = ownKeys.Length == 0
                ? candidates.FirstOrDefault(candidate => !addresses.Contains($"{candidate}/{segment}", StringComparer.Ordinal))
                : candidates.FirstOrDefault(candidate => Below(candidate).Any(address => address.StartsWith($"{candidate}/{segment}/", StringComparison.Ordinal)))
                    ?? candidates.OrderByDescending(candidate => Below(candidate).Length).FirstOrDefault();
            if (parent is null)
            {
                yield return $"the {product} rich fixture holds no object at {string.Join('/', form[..own])}"
                    + (ownKeys.Length == 0 ? $" without a {segment}" : string.Empty) + ", so no address reaches the kind's own step";
                yield break;
            }
        }
        string at = string.Join('/', new[] { parent, segment }.OfType<string>().Concat(ownKeys));
        var operation = new JsonObject { ["set"] = at, ["props"] = new JsonObject { [row] = value?.DeepClone() } };
        using var workspace = new RuleWorkspace();
        string extension = OperationRuns.Extension(product);
        string input = "input." + extension;
        string output = "edited." + extension;
        workspace.Seed(input, ScenarioFixtures.Read($"{product}.rich"));
        workspace.Seed("missing.ops.json", new JsonObject { ["ops"] = new JsonArray(operation) }.ToJsonString());
        IReadOnlyDictionary<string, string> before = workspace.Snapshot();
        RuleRun edit = workspace.Json(product, "edit", input, "--ops", "missing.ops.json", "--out", output);
        if (edit.Unjudged)
        {
            yield break;
        }
        string subject = operation.ToJsonString();
        JsonNode? details = edit.Document?["error"]?["details"];
        if (edit.ExitCode != 4 || edit.Error != "ADDRESS_NOT_FOUND")
        {
            yield return $"{subject}: expected exit 4 ADDRESS_NOT_FOUND, got {edit.Describe()}";
        }
        else if (details?["index"]?.ToJsonString() != "0" || details["at"]?.GetValue<string>() != at)
        {
            yield return $"{subject}: details.index {details?["index"]?.ToJsonString() ?? "missing"} and details.at {details?["at"]?.ToJsonString() ?? "missing"}, expected 0 and \"{at}\"";
        }
        if (RuleWorkspace.Written(before, workspace.Snapshot()) is { Length: > 0 } written)
        {
            yield return $"{subject}: wrote {string.Join(", ", written)}";
        }
    }

    /// <summary>Every address the product's read lists for its rich fixture, or the failure of that read; neither when evaluation mode stops it.</summary>
    private static (string[]? Addresses, string? Failure) ReadRichAddresses(string product)
    {
        using var workspace = new RuleWorkspace();
        string input = "input." + OperationRuns.Extension(product);
        workspace.Seed(input, ScenarioFixtures.Read($"{product}.rich"));
        CliCommand read = CliCatalog.Current.Command($"{product} read");
        RuleRun run = workspace.Json([.. read.Tokens, input, .. read.Has("--max-objects") ? new[] { "--max-objects", "10000" } : []]);
        if (run.ExitCode != 0)
        {
            return run.Unjudged ? (null, null) : (null, $"{product} read of the rich fixture failed: {run.Describe()}");
        }
        if (run.Document?["window"]?["truncated"]?.GetValue<bool>() == true)
        {
            return (null, $"{product} read of the rich fixture is truncated");
        }
        return ([.. (run.Document?["objects"] as JsonArray ?? []).Select(static item => item?["at"]?.GetValue<string>()).OfType<string>()], null);
    }

    /// <summary>Whether a part of an address form is a key, such as <c>&lt;id&gt;</c> or the optional <c>[&lt;index&gt;]</c>.</summary>
    private static bool IsKeyPart(string part) => part.StartsWith('<') || part.StartsWith('[');

    /// <summary>The pattern of the addresses an address form names: a segment as it is, a key by its kind, an optional key or none.</summary>
    private static string FormPattern(string[] form)
    {
        var pattern = new StringBuilder();
        foreach (string part in form)
        {
            pattern.Append(part.StartsWith('[')
                ? $"(?:/{KeyPatterns[part.Trim('[', ']')]})?"
                : (pattern.Length == 0 ? string.Empty : "/") + (part.StartsWith('<') ? KeyPatterns[part] : Regex.Escape(part)));
        }
        return pattern.ToString();
    }

    /// <summary>A value a row's schema accepts: its first enum value, else one of its first type that is not null.</summary>
    private static JsonNode? SchemaValue(JsonNode schema)
    {
        if (schema["enum"] is JsonArray { Count: > 0 } values)
        {
            return values[0]?.DeepClone();
        }
        string? type = schema["type"] switch
        {
            JsonArray types => types.Select(static item => item?.GetValue<string>()).FirstOrDefault(static item => item != "null"),
            JsonValue single => single.GetValue<string>(),
            _ => null,
        };
        return type switch
        {
            "boolean" => true,
            "integer" or "number" => schema["minimum"]?.DeepClone() ?? JsonValue.Create(1),
            "string" => "x",
            "array" => new JsonArray(),
            "object" => new JsonObject(),
            _ => null,
        };
    }
}
