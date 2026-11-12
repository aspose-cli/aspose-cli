using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// C-R44: a set_ operation writes what is there. Where the object it sets is missing, it is refused,
// rather than given a new object to write into that no read would take for what it set.
internal static partial class ContractRules
{
    /// <summary>
    /// Every set_ operation with a read path (<see cref="ReadPaths"/>), through each of its samples,
    /// on each bare document of its product (<see cref="OperationSamples.Bare"/>). Refused, it holds
    /// once the sample is shown to address the bare part: the document's address replaced one of
    /// its properties, and the refusal names targets at the bare part (<see cref="BareSample.Target"/>).
    /// Applied, each value it writes reads back at a new place, and not only on objects it added
    /// of the units the document names (<see cref="BareSample.Objects"/>), as the reads before and
    /// after name objects (<see cref="RoundTripAddress"/>).
    /// </summary>
    private static IEnumerable<InvariantCase> R44Run(CliCatalog catalog)
    {
        foreach (BareSample bare in OperationSamples.Bare)
        {
            CliProduct product = catalog.Products.Single(item => item.Id == bare.Product);
            bool fast = true;
            foreach (string op in product.Ops.Where(static op => op.StartsWith("set_", StringComparison.Ordinal)))
            {
                (string Field, string Path)[] fields = [.. ReadPaths(product, op)];
                OperationSample[] samples = [.. OperationSamples.All.Where(item => item.Product == bare.Product && item.Op == op && item.FromHtml is null && !item.Encrypted)];
                if (fields.Length > 0 && samples.Length > 0)
                {
                    yield return Check("C-R44", $"{product.Id} {op} on {bare.Label}",
                        () => samples.SelectMany(sample => OnBare(product, bare, sample, fields)), shard: product.Id, slow: !fast);
                    fast = false;
                }
            }
        }
    }

    private static IEnumerable<string> OnBare(CliProduct product, BareSample bare, OperationSample sample, (string Field, string Path)[] fields)
    {
        using var workspace = new RuleWorkspace();
        string extension = OperationRuns.Extension(bare.Product);
        string document = "bare." + extension;
        RuleRun setup = OperationRuns.Edit(workspace, bare.Product, OperationRuns.SeedInput(workspace, bare.Product), document, JsonNode.Parse(bare.Setup)!.AsArray());
        if (setup.ExitCode != 0)
        {
            if (!setup.Unjudged)
            {
                yield return $"building {bare.Label} failed: {setup.Describe()}";
            }
            yield break;
        }
        JsonObject operation = OperationRuns.Operation(sample);
        int addressed = 0;
        foreach ((string name, JsonNode? address) in JsonNode.Parse(bare.Address)!.AsObject())
        {
            if (operation.ContainsKey(name))
            {
                operation[name] = address?.DeepClone();
                addressed++;
            }
        }
        string subject = $"{sample.Subject} as {operation.ToJsonString()}";
        string output = "edited." + extension;
        // With --best-effort a refused operation is reported with the targets it addressed.
        RuleRun edit = OperationRuns.Edit(workspace, bare.Product, document, output, [operation], "--best-effort");
        if (edit.Unjudged)
        {
            yield break;
        }
        JsonNode? applied = edit.Document?["applied"]?[0];
        if (applied?["status"]?.GetValue<string>() != "ok")
        {
            // Refused, as it should be where its object is missing, once it is shown to address the bare part.
            string[] targets = [.. (applied?["targets"] as JsonArray ?? []).Select(static target => target?.GetValue<string>()).OfType<string>()];
            if (applied is null || addressed == 0 || targets.Length == 0
                || !targets.All(target => target == bare.Target || target.StartsWith(bare.Target + "/", StringComparison.Ordinal)))
            {
                yield return $"{subject}: the sample does not address {bare.Label} ({bare.Target}): {(applied is null ? edit.Describe() : $"refused at {string.Join(", ", targets)}")}";
            }
            else if (applied["error"]?["code"]?.GetValue<string>() == "INTERNAL_ERROR")
            {
                yield return $"{subject}: {applied["error"]!.ToJsonString()}";
            }
            yield break;
        }
        string[] schemaIds = [.. fields.Select(static field => ReadTarget(field.Field, field.Path).SchemaId).Append($"v2/{bare.Product}/read-result").Distinct()];
        JsonArray before = [.. schemaIds.SelectMany(id => OperationRuns.Read(workspace, bare.Product, document, [], id)).Select(static item => item!.DeepClone())];
        JsonArray after = [.. schemaIds.SelectMany(id => OperationRuns.Read(workspace, bare.Product, output, [], id)).Select(static item => item!.DeepClone())];
        // An object an operation adds beside what it sets, such as the placeholders of a layout it
        // applies, is its effect; one it writes its value into instead is what this rule refuses.
        HashSet<string> existing = [.. Objects(bare, before)];
        string[] added = [.. Objects(bare, after).Where(item => !existing.Contains(item))];
        foreach (RoundTripValue value in WrittenValues(product, sample, operation, fields))
        {
            string[] places = [.. Locate(after, value.Read).Where(hit => OperationRuns.Holds(value.Value, hit.Value) && IsNew(before, after, hit.Location, value.Value)).Select(static hit => hit.Location)];
            if (places.Length == 0)
            {
                yield return $"{subject}: {value.Field} {OperationRuns.Show(value.Value)} does not read back as a new {value.Read}";
            }
            else if (places.All(place => RoundTripAddress(bare.Product, place, after, before)?.Any(added.Contains) == true))
            {
                yield return $"{subject}: {value.Field} {OperationRuns.Show(value.Value)} reads back only on what it added, at {places[0]}";
            }
        }
    }

    /// <summary>The addresses of the objects a read reports, of the units <paramref name="bare"/> names.</summary>
    private static IEnumerable<string> Objects(BareSample bare, JsonArray read) =>
        read.SelectMany(item => OperationRuns.Walk(item!["result"], string.Empty)
                .Select(place => $"{item["command"]!.GetValue<string>()}: {place.Path}"))
            .SelectMany(place => RoundTripAddress(bare.Product, place, read, read) ?? [])
            .Where(address => address.Split('/') is [.., var unit, _] && bare.Objects.Contains(unit))
            .Distinct(StringComparer.Ordinal);
}
