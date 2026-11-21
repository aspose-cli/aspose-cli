using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

// An edit changes only what it names. Each product's rich fixture holds one marker text in each
// kind of content the product makes (ScenarioFixtures.RichMarkers); an edit whose operation
// touches no marker is judged by the markers its output still holds, and none may be gone.
internal static partial class ContractRules
{
    /// <summary>A probe of <see cref="OperationRuns.State"/> that evaluation mode stopped (<see cref="RuleRun.Unjudged"/>), as the state records it.</summary>
    private const string EvaluationStopped = "error EVALUATION_LIMIT";

    /// <summary>
    /// The kinds of content whose marker each product does not read from its own rich fixture,
    /// found once per test process; null when evaluation mode stops the read.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<string[]?>> UnreadSamples = new(StringComparer.Ordinal);

    // ----- C-R42: an edit keeps the content it does not name ---------------------------------------

    /// <summary>
    /// Every extension a product's <c>edit --out</c> writes (<see cref="EditExtensions"/>): the
    /// fixture is edited by the product's one-operation document, which touches no marker, and
    /// the product that reads the output (<see cref="ReaderOf"/>) must read every marker back.
    /// What the engine's save to another format loses is the engine's (AGENTS.md, Boundary), so
    /// <c>convert</c> has no case.
    /// </summary>
    private static IEnumerable<InvariantCase> R42(CliCatalog catalog)
    {
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            string own = ScenarioFixtures.PrimaryFormat(product.Id);
            foreach (string extension in EditExtensions(catalog, product.Id))
            {
                yield return Check("C-R42", $"{product.Id} edit {own} -> {extension}",
                    () => OutputLosses(catalog, product.Id, extension, workspace => EditTo(workspace, product.Id, extension)),
                    shard: product.Id);
            }
        }
    }

    /// <summary>
    /// The extensions a product's <c>edit --out</c> writes from its fixture, as the command itself
    /// states them: the <c>details.available</c> of its refusal of an extension no format has. The edit opens the product's own fixture with its
    /// one-operation document, since a product may judge <c>--out</c> against the format it
    /// opened. Empty for a product without <c>edit</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The refusal no longer lists the extensions, so the cases cannot be generated.</exception>
    private static string[] EditExtensions(CliCatalog catalog, string product)
    {
        if (!catalog.Commands.Any(command => command.Words == $"{product} edit"))
        {
            return [];
        }
        using var workspace = new RuleWorkspace();
        string input = "input." + ScenarioFixtures.PrimaryFormat(product);
        workspace.Seed(input, ScenarioFixtures.Read($"{product}.{ScenarioFixtures.PrimaryFormat(product)}"));
        workspace.Seed("edit.ops.json", ScenarioFixtures.Read($"{product}.ops"));
        RuleRun refused = workspace.Json(product, "edit", input, "--ops", "edit.ops.json", "--out", "output.unwritable");
        return refused.Document?["error"]?["details"]?["available"] is JsonArray { Count: > 0 } available
            ? [.. available.Select(static extension => extension!.GetValue<string>())]
            : throw new InvalidOperationException($"{product} edit does not list the extensions it writes when it refuses one: {refused.Describe()}");
    }

    /// <summary>Edits the product's rich fixture into <c>output&lt;extension&gt;</c> with its one-operation document.</summary>
    private static RuleRun EditTo(RuleWorkspace workspace, string product, string extension)
    {
        string input = SeedRich(workspace, product);
        workspace.Seed("edit.ops.json", ScenarioFixtures.Read($"{product}.ops"));
        return workspace.Json(product, "edit", input, "--ops", "edit.ops.json", "--out", "output" + extension);
    }

    /// <summary>What the edit <paramref name="write"/> left out of the output it wrote.</summary>
    private static IEnumerable<string> OutputLosses(CliCatalog catalog, string product, string target, Func<RuleWorkspace, RuleRun> write)
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
        RuleRun converted = write(workspace);
        if (converted.ExitCode != 0)
        {
            if (!converted.Unjudged)
            {
                yield return "the edit failed: " + converted.Describe();
            }
            else
            {
                Judged("not judged: evaluation mode stopped the edit");
            }
            yield break;
        }
        IReadOnlyList<RichKind> kinds = ScenarioFixtures.RichMarkers(product);
        string[] outputs = [.. OutputFiles(converted.Document)];
        var readers = new SortedSet<string>(StringComparer.Ordinal);
        var kept = new HashSet<RichKind>();
        foreach (string output in outputs)
        {
            if (ReaderOf(catalog, product, output) is not { } reader)
            {
                continue;
            }
            if (ReadText(catalog, workspace, reader, output) is not { } read)
            {
                Judged($"not judged: evaluation mode stopped {reader} reading {Path.GetFileName(output)}");
                yield break;
            }
            readers.Add(reader);
            Func<string, bool> holds = kinds.Any(kind => Holds(read, kind.Marker))
                ? marker => Holds(read, marker)
                : marker => ScenarioRunner.TextIn(output, marker) is not null;
            kept.UnionWith(kinds.Where(kind => holds(kind.Marker)));
        }
        if (readers.Count == 0)
        {
            yield return $"no product reads the outputs {string.Join(", ", outputs.Select(Path.GetFileName))}";
            yield break;
        }
        RichKind[] lost = [.. kinds.Where(kind => !kept.Contains(kind))];
        Judged($"judged: {Sentence([.. readers], "and")} reads {Names(kinds.Where(kept.Contains))} back; lost {Names(lost)}");
        if (lost.Length > 0)
        {
            yield return $"{Sentence([.. readers], "and")} reads no {Sentence([.. lost.Select(static kind => kind.Name)], "or")} marker from the {target} output";
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

    /// <summary>The names of the kinds in a sentence, or <c>none</c>.</summary>
    private static string Names(IEnumerable<RichKind> kinds) =>
        kinds.Select(static kind => kind.Name).ToArray() is { Length: > 0 } names ? Sentence(names, "and") : "none";

    /// <summary>
    /// Writes to the test's output whether the case was judged, and by what, so a passing case
    /// shows it was not skipped: in evaluation mode a read can stop with <c>EVALUATION_LIMIT</c>.
    /// </summary>
    private static void Judged(string outcome) => TestContext.Current.TestOutputHelper?.WriteLine(outcome);

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
                ? [.. ScenarioFixtures.RichMarkers(key).Where(kind => !Holds(read, kind.Marker)).Select(static kind => kind.Name)]
                : null;
        })).Value;
        if (unread is null)
        {
            Judged($"not judged: evaluation mode stopped {product} reading its rich fixture");
        }
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

    /// <summary>Every file a result lists, in <c>output.path</c> or <c>outputs[].path</c>.</summary>
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

    /// <summary>The products that take a file with the extension as input, in catalog order.</summary>
    private static string[] Readers(CliCatalog catalog, string extension) =>
        [
            .. catalog.Products.Select(static product => product.Id).Where(product => Formats(catalog, product).Any(entry =>
                entry["uses"]!.AsArray().Any(static use => use!.GetValue<string>() == "input")
                && entry["extensions"]!.AsArray().Any(item => string.Equals(item!.GetValue<string>(), extension, StringComparison.OrdinalIgnoreCase)))),
        ];

    /// <summary>
    /// The product that reads an output file: the product that wrote it, else the first that takes
    /// it as input; null when none does.
    /// </summary>
    private static string? ReaderOf(CliCatalog catalog, string product, string file)
    {
        string extension = Path.GetExtension(file);
        string[] readers = Readers(catalog, extension);
        return readers.Contains(product) ? product : readers.FirstOrDefault();
    }

    private static IEnumerable<JsonNode> Formats(CliCatalog catalog, string product) =>
        catalog.FormatNodes(product);
}
