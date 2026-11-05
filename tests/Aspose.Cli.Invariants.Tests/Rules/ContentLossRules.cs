using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// What a command leaves out of its output, it says. Each product's rich fixture holds one marker
// text in each kind of content the product makes (ScenarioFixtures.RichMarkers); a command that
// writes the fixture's content to a file is judged by the markers that file still holds. Only
// silence is judged: a result that carries any warning other than an evaluation notice has said
// something, and whether that warning names what was lost is for review.
internal static partial class ContractRules
{
    /// <summary>
    /// The warnings evaluation mode adds to a result, which say that the engine marked or truncated
    /// what it wrote or read but nothing about what the command left out: <c>EVALUATION_MODE</c>,
    /// <c>EVALUATION_INPUT_MARKED</c> and <c>EVALUATION_INPUT_TRUNCATED</c>. The fourth evaluation
    /// code, <c>EVALUATION_LIMIT</c>, is an error: a run that ends with it is not judged.
    /// </summary>
    private static readonly HashSet<string> EvaluationNotices =
        new(["EVALUATION_MODE", "EVALUATION_INPUT_MARKED", "EVALUATION_INPUT_TRUNCATED"], StringComparer.Ordinal);

    /// <summary>A probe of <see cref="OperationRuns.State"/> that evaluation mode stopped (<see cref="RuleRun.Unjudged"/>), as the state records it.</summary>
    private const string EvaluationStopped = "error EVALUATION_LIMIT";

    /// <summary>The text <c>replace_text</c> puts in place of <see cref="ScenarioFixtures.MarkerPrefix"/>.</summary>
    private const string Replacement = "Replaced";

    /// <summary>
    /// The kinds of content whose marker each product does not read from its own rich fixture,
    /// found once per test process; null when evaluation mode stops the read.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<string[]?>> UnreadSamples = new(StringComparer.Ordinal);

    // ----- C-R42: a convert that loses content says so -------------------------------------------

    /// <summary>
    /// Every <c>convert --to</c> target of a product with a rich fixture, when some product takes a
    /// file of that format as input: the fixture converts, and the product that reads the output
    /// (<see cref="ReaderOf"/>) must read every marker back, or the result must warn. A file from
    /// which the reader reads no marker at all is one it cannot read, such as the frameset of a
    /// Cells MHTML archive that Words reads as an empty document, so the markers that file holds as
    /// text count instead. A target no product reads is skipped; today these are the Cells
    /// <c>json</c> and <c>xps</c>, the PDF <c>tiff</c> and <c>xps</c>, the Slides <c>gif</c>,
    /// <c>tiff</c> and <c>xps</c>, and the Words <c>openxps</c>, <c>pcl</c>, <c>ps</c> and <c>xps</c>.
    /// </summary>
    private static IEnumerable<InvariantCase> R42(CliCatalog catalog)
    {
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} convert")?.Option("--to") is not { } to)
            {
                continue;
            }
            string own = ScenarioFixtures.PrimaryFormat(product.Id);
            foreach (string target in to.AllowedValues.Where(target => Extensions(catalog, product.Id, target).Any(extension => Readers(catalog, extension).Length > 0)))
            {
                yield return Check("C-R42", $"{product.Id} convert {own} -> {target}", () => ConversionLosses(catalog, product.Id, target), shard: product.Id);
            }
        }
    }

    private static IEnumerable<string> ConversionLosses(CliCatalog catalog, string product, string target)
    {
        if (Unjudgeable(catalog, product) is { } reason)
        {
            if (reason.Length > 0)
            {
                yield return reason;
            }
            yield break;
        }
        using var workspace = new RuleWorkspace();
        RuleRun converted = workspace.Json(product, "convert", SeedRich(workspace, product), "--to", target);
        if (converted.ExitCode != 0)
        {
            if (!converted.Unjudged)
            {
                yield return "the convert failed: " + converted.Describe();
            }
            yield break;
        }
        if (Warned(converted.Document))
        {
            yield break;
        }
        IReadOnlyList<KeyValuePair<string, string>> markers = ScenarioFixtures.RichMarkers(product);
        string[] outputs = [.. OutputFiles(converted.Document)];
        var readers = new SortedSet<string>(StringComparer.Ordinal);
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (string output in outputs)
        {
            if (ReaderOf(catalog, product, output) is not { } reader)
            {
                continue;
            }
            if (ReadText(catalog, workspace, reader, output) is not { } read)
            {
                yield break;
            }
            readers.Add(reader);
            Func<string, bool> holds = markers.Any(marker => Holds(read, marker.Value))
                ? marker => Holds(read, marker)
                : marker => ScenarioRunner.TextIn(output, marker) is not null;
            kept.UnionWith(markers.Where(marker => holds(marker.Value)).Select(static marker => marker.Key));
        }
        if (readers.Count == 0)
        {
            yield return $"no product reads the outputs {string.Join(", ", outputs.Select(Path.GetFileName))}";
            yield break;
        }
        string[] lost = [.. markers.Select(static marker => marker.Key).Where(kind => !kept.Contains(kind))];
        if (lost.Length > 0)
        {
            yield return $"{Sentence([.. readers], "and")} reads no {Sentence(lost, "or")} marker from the {target} output, and the convert warns of nothing";
        }
    }

    // ----- C-R43: a replacement that leaves a match in place says so -----------------------------

    /// <summary>
    /// Every product with a rich fixture and a <c>replace_text</c> operation replaces
    /// <see cref="ScenarioFixtures.MarkerPrefix"/>, the start of every marker, in the scope that
    /// covers the most content (<c>all</c> where the operation has a scope): the product must read
    /// no marker back from the output, or the result must warn.
    /// </summary>
    private static IEnumerable<InvariantCase> R43(CliCatalog catalog)
    {
        foreach (CliProduct product in catalog.Products.Where(static product =>
            ScenarioFixtures.Products.Contains(product.Id) && product.Ops.Contains("replace_text")))
        {
            yield return Check("C-R43", $"{product.Id} replace_text", () => ReplacementLeftovers(catalog, product), shard: product.Id);
        }
    }

    private static IEnumerable<string> ReplacementLeftovers(CliCatalog catalog, CliProduct product)
    {
        if (Unjudgeable(catalog, product.Id) is { } reason)
        {
            if (reason.Length > 0)
            {
                yield return reason;
            }
            yield break;
        }
        var operation = new JsonObject { ["op"] = "replace_text", ["find"] = ScenarioFixtures.MarkerPrefix, ["replace"] = Replacement };
        JsonNode definition = JsonNode.Parse(catalog.SchemaText(product.OpsSchemaId!))!["$defs"]!["replace_text"]!;
        if (definition["properties"]?["scope"]?["enum"] is JsonArray scopes && scopes.Any(static scope => scope?.GetValue<string>() == "all"))
        {
            operation["scope"] = "all";
        }
        using var workspace = new RuleWorkspace();
        string output = "output." + ScenarioFixtures.PrimaryFormat(product.Id);
        RuleRun edit = OperationRuns.Edit(workspace, product.Id, SeedRich(workspace, product.Id), output, [operation]);
        if (edit.ExitCode != 0)
        {
            if (!edit.Unjudged)
            {
                yield return "the edit failed: " + edit.Describe();
            }
            yield break;
        }
        if (Warned(edit.Document) || ReadText(catalog, workspace, product.Id, output) is not { } read)
        {
            yield break;
        }
        string[] left = [.. ScenarioFixtures.RichMarkers(product.Id).Where(marker => Holds(read, marker.Value)).Select(static marker => marker.Key)];
        if (left.Length > 0)
        {
            yield return $"{product.Id} still reads the {Sentence(left, "and")} marker{(left.Length == 1 ? string.Empty : "s")} after the replacement, and the edit warns of nothing";
        }
    }

    // ----- helpers ---------------------------------------------------------------------------------

    /// <summary>Seeds the product's rich fixture as <c>input.&lt;format&gt;</c>.</summary>
    private static string SeedRich(RuleWorkspace workspace, string product)
    {
        string input = "input." + ScenarioFixtures.PrimaryFormat(product);
        workspace.Seed(input, ScenarioFixtures.Read($"{product}.rich"));
        return input;
    }

    /// <summary>Whether a result carries a warning other than an evaluation notice.</summary>
    private static bool Warned(JsonNode? document) =>
        ScenarioContract.Codes(document?["warnings"]).Any(static code => !EvaluationNotices.Contains(code));

    private static bool Holds(string text, string marker) => text.Contains(marker, StringComparison.OrdinalIgnoreCase);

    /// <summary>Names in a sentence: <c>a</c>, <c>a or b</c>, <c>a, b or c</c>.</summary>
    private static string Sentence(IReadOnlyList<string> names, string conjunction) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} {conjunction} {names[^1]}";

    /// <summary>
    /// Why the product's rich fixture cannot judge a command: a problem when the product's own
    /// commands do not read every marker of it, as a missing marker in an output would then say
    /// nothing about the command; an empty string when evaluation mode stops that read; null when
    /// the fixture can judge. The fixture is read once per test process.
    /// </summary>
    private static string? Unjudgeable(CliCatalog catalog, string product)
    {
        string[]? unread = UnreadSamples.GetOrAdd(product, key => new Lazy<string[]?>(() =>
        {
            using var workspace = new RuleWorkspace();
            return ReadText(catalog, workspace, key, SeedRich(workspace, key)) is { } read
                ? [.. ScenarioFixtures.RichMarkers(key).Where(marker => !Holds(read, marker.Value)).Select(static marker => marker.Key)]
                : null;
        })).Value;
        return unread is null ? string.Empty
            : unread.Length > 0 ? $"{product} reads no {Sentence(unread, "or")} marker from its rich fixture"
            : null;
    }

    /// <summary>
    /// The text a product reads from a file: what its <c>inspect</c> with every detail and its
    /// <c>read</c> with its fullest content report (<see cref="OperationRuns.State"/>), and what its
    /// <c>search</c> for the markers finds in its widest scope; null when evaluation mode stops one
    /// of them.
    /// </summary>
    private static string? ReadText(CliCatalog catalog, RuleWorkspace workspace, string product, string file)
    {
        JsonArray probes = OperationRuns.State(workspace, product, file, []);
        if (probes.Any(static probe => probe?["result"] is JsonValue result && result.TryGetValue(out string? error) && error == EvaluationStopped))
        {
            return null;
        }
        string state = probes.ToJsonString();
        if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} search") is not { } search)
        {
            return state;
        }
        List<string> args = [.. search.Tokens, file, "--pattern", ScenarioFixtures.MarkerPrefix + "[A-Za-z]+", "--regex"];
        foreach (string scope in new[] { "--scope", "--content" })
        {
            if (search.Option(scope) is { } option && option.AllowedValues.Contains("all"))
            {
                args.AddRange([scope, "all"]);
            }
        }
        RuleRun found = workspace.Json([.. args]);
        return found.Unjudged ? null : state + (found.ExitCode == 0 ? found.Document?.ToJsonString() : null);
    }

    /// <summary>Every file a convert result lists, in <c>output.path</c> or <c>outputs[].path</c>.</summary>
    private static IEnumerable<string> OutputFiles(JsonNode? document)
    {
        if (document?["output"]?["path"]?.GetValue<string>() is { } path)
        {
            yield return path;
        }
        foreach (JsonNode? output in document?["outputs"] as JsonArray ?? [])
        {
            if (output?["path"]?.GetValue<string>() is { } item)
            {
                yield return item;
            }
        }
    }

    /// <summary>The extensions a product writes a convert target with, from its format table.</summary>
    private static IEnumerable<string> Extensions(CliCatalog catalog, string product, string format) =>
        Formats(catalog, product)
            .Where(entry => entry["id"]!.GetValue<string>() == format)
            .SelectMany(static entry => entry["extensions"]!.AsArray().Select(static extension => extension!.GetValue<string>()));

    /// <summary>The products that take a file with the extension as input, in catalog order.</summary>
    private static string[] Readers(CliCatalog catalog, string extension) =>
        [
            .. catalog.Products.Select(static product => product.Id).Where(product => Formats(catalog, product).Any(entry =>
                entry["uses"]!.AsArray().Any(static use => use!.GetValue<string>() == "input")
                && entry["extensions"]!.AsArray().Any(item => string.Equals(item!.GetValue<string>(), extension, StringComparison.OrdinalIgnoreCase)))),
        ];

    /// <summary>
    /// The product that reads a converted file: the one routing assigns to its extension, else the
    /// converting product, else the first that takes it as input; null when none does.
    /// </summary>
    private static string? ReaderOf(CliCatalog catalog, string product, string file)
    {
        string extension = Path.GetExtension(file);
        string[] readers = Readers(catalog, extension);
        return catalog.Routes.TryGetValue(extension, out string? routed) && readers.Contains(routed) ? routed
            : readers.Contains(product) ? product
            : readers.FirstOrDefault();
    }

    private static IEnumerable<JsonNode> Formats(CliCatalog catalog, string product) =>
        catalog.Document["products"]!.AsArray()
            .Single(node => node!["id"]!.GetValue<string>() == product)!["formats"]!.AsArray().OfType<JsonNode>();
}
