using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

// What a command leaves out of its output, it names. Each product's rich fixture holds one marker
// text in each kind of content the product makes (ScenarioFixtures.RichMarkers); a command that
// writes the fixture's content to a file is judged by the markers that file still holds. Each
// kind whose marker is gone must be named by its token (RichKind.Content in a LOSSY_CONVERSION's
// details.content, RichKind.Part in a TEXT_NOT_REPLACED's details.part); any other warning, an
// evaluation notice included, excuses nothing, and a kind without a token is never excused.
internal static partial class ContractRules
{
    /// <summary>The warning that names, in <c>details.content</c>, the kinds of content an output leaves out.</summary>
    private const string LossyConversion = "LOSSY_CONVERSION";

    /// <summary>The warning that names, in <c>details.part</c>, the part of the document where an operation left the text it found.</summary>
    private const string TextNotReplaced = "TEXT_NOT_REPLACED";

    /// <summary>A probe of <see cref="OperationRuns.State"/> that evaluation mode stopped (<see cref="RuleRun.Unjudged"/>), as the state records it.</summary>
    private const string EvaluationStopped = "error EVALUATION_LIMIT";

    /// <summary>The text <c>replace_text</c> puts in place of <see cref="ScenarioFixtures.MarkerPrefix"/>.</summary>
    private const string Replacement = "Replaced";

    /// <summary>
    /// The kinds of content whose marker each product does not read from its own rich fixture,
    /// found once per test process; null when evaluation mode stops the read.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<string[]?>> UnreadSamples = new(StringComparer.Ordinal);

    // ----- C-R42: a convert or an edit that loses content names it ---------------------------------

    /// <summary>
    /// Every <c>convert --to</c> target of a product with a rich fixture, when some product takes a
    /// file of that format as input, and every extension its <c>edit --out</c> writes
    /// (<see cref="EditExtensions"/>): the fixture is converted, or edited by the product's
    /// one-operation document, which touches no marker, and for each kind of content whose marker
    /// the product that reads the output (<see cref="ReaderOf"/>) does not read back, a
    /// <c>LOSSY_CONVERSION</c> of the result must name the kind's token (<see cref="RichKind.Content"/>)
    /// in <c>details.content</c>. A file from which the reader reads no marker at all is one it
    /// cannot read, such as the frameset of a Cells MHTML archive that Words reads as an empty
    /// document, so the markers that file holds as text count instead. A convert target no product
    /// reads is skipped; today these are the Cells
    /// <c>json</c> and <c>xps</c>, the PDF <c>tiff</c> and <c>xps</c>, the Slides <c>gif</c> and
    /// <c>xps</c>, and the Words <c>openxps</c>, <c>pcl</c>, <c>ps</c> and <c>xps</c>.
    /// </summary>
    private static IEnumerable<InvariantCase> R42(CliCatalog catalog)
    {
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            string own = ScenarioFixtures.PrimaryFormat(product.Id);
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product.Id} convert")?.Option("--to") is { } to)
            {
                foreach (string target in to.AllowedValues.Where(target => Extensions(catalog, product.Id, target).Any(extension => Readers(catalog, extension).Length > 0)))
                {
                    yield return Check("C-R42", $"{product.Id} convert {own} -> {target}",
                        () => OutputLosses(catalog, product.Id, target, "convert", workspace => workspace.Json(product.Id, "convert", SeedRich(workspace, product.Id), "--to", target)),
                        shard: product.Id);
                }
            }
            foreach (string extension in EditExtensions(catalog, product.Id))
            {
                yield return Check("C-R42", $"{product.Id} edit {own} -> {extension}",
                    () => OutputLosses(catalog, product.Id, extension, "edit", workspace => EditTo(workspace, product.Id, extension)),
                    shard: product.Id);
            }
        }
    }

    /// <summary>
    /// The extensions a product's <c>edit --out</c> writes, as the command itself states them:
    /// <c>capabilities</c> does not list what a command writes, and only some of these formats
    /// follow from a format's uses, so they are the <c>details.available</c> of its refusal of an
    /// extension no format has, which comes before the input is read. Empty for a product without
    /// <c>edit</c>.
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
        workspace.Seed(input, "Not a document: the refusal comes before the input is read.");
        RuleRun refused = workspace.Json(product, "edit", input, "--ops", """{"ops":[]}""", "--out", "output.unwritable");
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

    /// <summary>What <paramref name="write"/>, the <paramref name="verb"/> of the case, left out of the output it wrote and did not name.</summary>
    private static IEnumerable<string> OutputLosses(CliCatalog catalog, string product, string target, string verb, Func<RuleWorkspace, RuleRun> write)
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
                yield return $"the {verb} failed: " + converted.Describe();
            }
            else
            {
                Judged($"not judged: evaluation mode stopped the {verb}");
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
        string[] named = [.. Named(converted.Document, LossyConversion, "content")];
        Judged($"judged: {Sentence([.. readers], "and")} reads {Names(kinds.Where(kept.Contains))} back; lost {Names(lost)}; {LossyConversion} names [{string.Join(", ", named)}]");
        RichKind[] unnamed = [.. lost.Where(kind => kind.Content is { } token && !named.Contains(token, StringComparer.Ordinal))];
        if (unnamed.Length > 0)
        {
            yield return $"{Sentence([.. readers], "and")} reads no {Sentence([.. unnamed.Select(static kind => kind.Name)], "or")} marker from the {target} output, "
                + $"and no {LossyConversion} names {Sentence([.. unnamed.Select(static kind => kind.Content!).Distinct(StringComparer.Ordinal)], "or")} in details.content";
        }
        RichKind[] unexcused = [.. lost.Where(static kind => kind.Content is null)];
        if (unexcused.Length > 0)
        {
            yield return $"{Sentence([.. readers], "and")} reads no {Sentence([.. unexcused.Select(static kind => kind.Name)], "or")} marker from the {target} output, which no warning can excuse";
        }
    }

    // ----- C-R43: a replacement that leaves a match in place names where ---------------------------

    /// <summary>
    /// Every product with a rich fixture and a <c>replace_text</c> operation replaces
    /// <see cref="ScenarioFixtures.MarkerPrefix"/>, the start of every marker, in the scope that
    /// covers the most content (<c>all</c> where the operation has a scope): for each kind of
    /// content whose marker the product still reads back from the output, a
    /// <c>TEXT_NOT_REPLACED</c> of the result must name the kind's part (<see cref="RichKind.Part"/>)
    /// in <c>details.part</c>.
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
            else
            {
                Judged("not judged: evaluation mode stopped the edit");
            }
            yield break;
        }
        if (ReadText(catalog, workspace, product.Id, output) is not { } read)
        {
            Judged($"not judged: evaluation mode stopped {product.Id} reading {output}");
            yield break;
        }
        RichKind[] left = [.. ScenarioFixtures.RichMarkers(product.Id).Where(kind => Holds(read, kind.Marker))];
        string[] named = [.. Named(edit.Document, TextNotReplaced, "part")];
        Judged($"judged: {product.Id} still reads {Names(left)}; {TextNotReplaced} names [{string.Join(", ", named)}]");
        RichKind[] unnamed = [.. left.Where(kind => kind.Part is { } token && !named.Contains(token, StringComparer.Ordinal))];
        if (unnamed.Length > 0)
        {
            yield return $"{product.Id} still reads the {Sentence([.. unnamed.Select(static kind => kind.Name)], "and")} marker{(unnamed.Length == 1 ? string.Empty : "s")} after the replacement, "
                + $"and no {TextNotReplaced} names {Sentence([.. unnamed.Select(static kind => kind.Part!).Distinct(StringComparer.Ordinal)], "or")} in details.part";
        }
        RichKind[] unexcused = [.. left.Where(static kind => kind.Part is null)];
        if (unexcused.Length > 0)
        {
            yield return $"{product.Id} still reads the {Sentence([.. unexcused.Select(static kind => kind.Name)], "and")} marker{(unexcused.Length == 1 ? string.Empty : "s")} after the replacement, which no warning can excuse";
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

    /// <summary>
    /// The tokens the result's warnings of <paramref name="code"/> name in <c>details.&lt;key&gt;</c>,
    /// a token or a list of them, each once.
    /// </summary>
    private static IEnumerable<string> Named(JsonNode? document, string code, string key) =>
        (document?["warnings"] as JsonArray ?? [])
            .Where(warning => warning?["code"]?.GetValue<string>() == code)
            .SelectMany(warning => warning?["details"]?[key] switch
            {
                JsonArray tokens => tokens.Select(static token => token?.GetValue<string>()),
                JsonValue token => [token.GetValue<string>()],
                _ => [],
            })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

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
