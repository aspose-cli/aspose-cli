using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// ----- C-R40: a verb several products share reports its files in one shape --------------------
// A product verb does the same work in every product (C-R02), so an agent reads its result with
// the same code: the files it writes are an outputs[] of the SDK's OutputItem records or one
// output, the same one of the two in every product, and the files a create reads are its
// inputs[] of the SDK's source-input objects, never fields of their own.
internal static partial class ContractRules
{
    private const string CommonInput = "v2/common/input";
    private const string CommonOutput = "v2/common/output";
    private const string CommonSourceInput = "v2/common/source-input";
    private const string CommonBackup = "v2/common/backup";
    private const string CommonFingerprint = "v2/common/file-fingerprint";

    private static IEnumerable<InvariantCase> R40(CliCatalog catalog)
    {
        HashSet<string> results = [.. PublishedSchema.ResultIds(catalog)];
        foreach (IGrouping<string, string> verb in SharedVerbResults(catalog, results))
        {
            string[] ids = [.. verb];
            yield return Check("C-R40", $"{verb.Key} file shape", () => VerbFileShape(verb.Key, ids, PublishedSchema.Document));
        }
        yield return Check("C-R40", "historical shapes", HistoricalShapes);
    }

    /// <summary>The result schema of every product verb that two or more products have, by verb.</summary>
    private static IEnumerable<IGrouping<string, string>> SharedVerbResults(CliCatalog catalog, HashSet<string> results) =>
        catalog.Commands
            .Where(static command => command.Product is not null && command.Tokens.Length == 2)
            .Select(command => (command.Verb, Id: $"v2/{command.Product}/{command.Verb}-result"))
            .Where(item => results.Contains(item.Id))
            .GroupBy(static item => item.Verb, static item => item.Id)
            .Where(static verb => verb.Count() > 1)
            .OrderBy(static verb => verb.Key, StringComparer.Ordinal);

    /// <summary>
    /// The ways the results of <paramref name="verb"/> (one schema id per product) report their
    /// files differently from one another or from the shapes the SDK owns; <paramref name="documents"/>
    /// reads a schema by id, so a check can judge a schema changed for the purpose.
    /// </summary>
    private static IEnumerable<string> VerbFileShape(string verb, IReadOnlyList<string> ids, Func<string, JsonObject> documents)
    {
        var written = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            JsonObject document = documents(id);
            bool sources = false;
            foreach ((string name, JsonNode? value) in document["properties"] as JsonObject ?? [])
            {
                if (value is not JsonObject schema)
                {
                    continue;
                }
                switch (FileKindOf(documents, id, schema))
                {
                    case FileKind.Output:
                        written[id] = "output";
                        break;
                    case FileKind.Files when name == "outputs" && IsOutputItem(documents, id, ItemsOf(schema)):
                        written[id] = "outputs[]";
                        break;
                    case FileKind.Files:
                        yield return $"{id}: lists files in {name}[] whose items are not an SDK OutputItem; the files a command writes are outputs[] of OutputItem records or one output";
                        break;
                    case FileKind.File:
                        yield return $"{id}: reports the file {name} as an object of its own, not as output";
                        break;
                    case FileKind.Sources when verb == "create" && name == "inputs":
                        sources = true;
                        break;
                    case FileKind.Sources or FileKind.Source:
                        yield return $"{id}: reports {name} as source-input, which only the inputs[] of a create is";
                        break;
                    case FileKind.Read when verb == "create":
                        yield return $"{id}: reports the source {name} as a field of its own, not as an item of inputs[]";
                        break;
                    case FileKind.Inputs when verb == "create":
                        yield return $"{id}: {name}[] holds input objects without the option that named each file, not source-input";
                        break;
                }
            }
            if (verb == "create" && !sources)
            {
                yield return $"{id}: reports no inputs[] of the files it was created from";
            }
        }
        if (written.Values.Distinct(StringComparer.Ordinal).Count() > 1)
        {
            yield return $"{verb} reports its written files in different shapes: {string.Join(", ", written.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => $"{pair.Key} {pair.Value}"))}";
        }
    }

    /// <summary>What a property of a result says about files.</summary>
    private enum FileKind
    {
        /// <summary>Nothing: not a file object or a list of them.</summary>
        None,

        /// <summary>The SDK input object of a file the command read.</summary>
        Read,

        /// <summary>A list of SDK input objects.</summary>
        Inputs,

        /// <summary>One SDK source-input object.</summary>
        Source,

        /// <summary>A list of SDK source-input objects.</summary>
        Sources,

        /// <summary>The SDK output object of the file the command wrote.</summary>
        Output,

        /// <summary>The SDK backup object of an in-place edit.</summary>
        Backup,

        /// <summary>A list of objects with a path, of a product's own type.</summary>
        Files,

        /// <summary>One object with a path, of a product's own type.</summary>
        File,
    }

    private static FileKind FileKindOf(Func<string, JsonObject> documents, string id, JsonObject schema)
    {
        switch (CommonId(schema))
        {
            case CommonInput: return FileKind.Read;
            case CommonOutput: return FileKind.Output;
            case CommonSourceInput: return FileKind.Source;
            case CommonBackup: return FileKind.Backup;
        }
        (string resolvedId, JsonObject node) = ResolveIn(documents, id, schema);
        if (node["items"] is JsonObject items)
        {
            return CommonId(items) switch
            {
                CommonInput => FileKind.Inputs,
                CommonSourceInput => FileKind.Sources,
                CommonOutput => FileKind.Files,
                _ => ResolveIn(documents, resolvedId, items).Node["properties"]?["path"] is not null ? FileKind.Files : FileKind.None,
            };
        }
        return node["properties"]?["path"] is not null ? FileKind.File : FileKind.None;
    }

    private static JsonObject ItemsOf(JsonObject schema) => (JsonObject)schema["items"]!;

    /// <summary>Whether a list item is an SDK <c>OutputItem</c>: the file fields with the SDK fingerprint, beside at most a unit.</summary>
    private static bool IsOutputItem(Func<string, JsonObject> documents, string id, JsonObject item)
    {
        JsonObject node = ResolveIn(documents, id, item).Node;
        return node["properties"] is JsonObject properties
            && FileCore.Append("encrypted").All(properties.ContainsKey)
            && properties["fingerprint"] is JsonObject fingerprint
            && CommonId(fingerprint) == CommonFingerprint;
    }

    /// <summary>The common schema a property references directly, or null.</summary>
    private static string? CommonId(JsonObject schema) =>
        schema["$ref"]?.GetValue<string>() is { } reference && !reference.StartsWith('#')
            ? PublishedSchemas.IdOf(reference.Split('#')[0]) is var target && target.StartsWith("v2/common/", StringComparison.Ordinal) ? target : null
            : null;

    /// <summary>A <c>$ref</c> followed to the node it names, in the schemas <paramref name="documents"/> reads.</summary>
    private static (string Id, JsonObject Node) ResolveIn(Func<string, JsonObject> documents, string id, JsonObject node)
    {
        for (int depth = 0; depth < 16 && node["$ref"]?.GetValue<string>() is { } reference; depth++)
        {
            string[] parts = reference.Split('#', 2);
            if (parts[0].Length > 0)
            {
                id = PublishedSchemas.IdOf(parts[0]);
            }
            JsonNode current = documents(id);
            foreach (string segment in (parts.Length > 1 ? parts[1] : string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current[segment] ?? throw new InvalidOperationException($"'{reference}' does not resolve in '{id}'.");
            }
            node = (JsonObject)current;
        }
        return (id, node);
    }

    /// <summary>
    /// The rule on the shapes the products had before it: each changed result must report its
    /// problem. A rule that stopped reporting one of these fails here.
    /// </summary>
    private static IEnumerable<string> HistoricalShapes()
    {
        static JsonObject Copy(string id) => (JsonObject)PublishedSchema.Document(id).DeepClone();
        static JsonObject Reference(string id) => new() { ["$ref"] = $"https://schemas.aspose.com/aspose-cli/{id}.schema.json" };

        // Slides and Words extract listed items[] of their own records, without format or fingerprint.
        JsonObject extract = Copy("v2/slides/extract-result");
        JsonObject extractProperties = (JsonObject)extract["properties"]!;
        extractProperties.Remove("outputs");
        extractProperties["items"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" }, ["slide"] = new JsonObject { ["type"] = "integer" } },
            },
        };

        // Slides create reported its template and Markdown as fields of their own.
        JsonObject slidesCreate = Copy("v2/slides/create-result");
        JsonObject slidesCreateProperties = (JsonObject)slidesCreate["properties"]!;
        slidesCreateProperties.Remove("inputs");
        slidesCreateProperties["template"] = Reference(CommonInput);
        slidesCreateProperties["markdown"] = Reference(CommonInput);

        // Words create reported no source at all.
        JsonObject wordsCreate = Copy("v2/words/create-result");
        ((JsonObject)wordsCreate["properties"]!).Remove("inputs");

        // One product's convert wrote one output where the others list outputs[].
        JsonObject convert = Copy("v2/words/convert-result");
        JsonObject convertProperties = (JsonObject)convert["properties"]!;
        convertProperties.Remove("outputs");
        convertProperties["output"] = Reference(CommonOutput);

        (string Verb, string[] Ids, JsonObject Changed, string Problem)[] samples =
        [
            ("extract", ["v2/pdf/extract-result", "v2/slides/extract-result"], extract, "lists files in items[] whose items are not an SDK OutputItem"),
            ("create", ["v2/pdf/create-result", "v2/slides/create-result"], slidesCreate, "reports the source template as a field of its own"),
            ("create", ["v2/pdf/create-result", "v2/slides/create-result"], slidesCreate, "v2/slides/create-result: reports no inputs[]"),
            ("create", ["v2/pdf/create-result", "v2/words/create-result"], wordsCreate, "v2/words/create-result: reports no inputs[]"),
            ("convert", ["v2/pdf/convert-result", "v2/words/convert-result"], convert, "convert reports its written files in different shapes"),
        ];
        foreach ((string verb, string[] ids, JsonObject changed, string problem) in samples)
        {
            string changedId = ids[^1];
            JsonObject Documents(string id) => id == changedId ? changed : PublishedSchema.Document(id);
            string[] found = [.. VerbFileShape(verb, ids, Documents)];
            if (!found.Any(finding => finding.Contains(problem, StringComparison.Ordinal)))
            {
                yield return $"the {verb} sample changed in {changedId} does not report \"{problem}\" but [{string.Join("; ", found)}]";
            }
        }
    }
}
