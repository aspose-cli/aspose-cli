using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// What writes, operations and windows do when run. Each case runs the CLI in its product's shard;
// the operation samples are OperationSamples, the runs and comparisons OperationRuns.
internal static partial class ContractRules
{
    private const string NoEffectWarning = "OP_NO_EFFECT";

    /// <summary>The unit a read command counts, by the option that selects its units.</summary>
    private static readonly Dictionary<string, string> SelectionUnits = new(StringComparer.Ordinal)
    {
        ["--pages"] = "page",
        ["--slides"] = "slide",
        ["--blocks"] = "block",
        ["--range"] = "cell",
    };

    /// <summary>A selection of every unit of the fixture, by selection option.</summary>
    private static readonly Dictionary<string, string> WholeSelections = new(StringComparer.Ordinal)
    {
        ["--pages"] = "1-",
        ["--slides"] = "1-",
        ["--blocks"] = "1-",
        ["--range"] = "A1:B3",
    };

    /// <summary>The operations that give a product's fixture a second page or sheet; null when the fixture has two units or the product merges.</summary>
    private static readonly Dictionary<string, string?> SecondUnit = new(StringComparer.Ordinal)
    {
        ["cells"] = """[{"op":"add_sheet","name":"Extra"},{"op":"set_values","sheet":"Extra","range":"A1","values":[["Second sheet"]]}]""",
        ["pdf"] = null,
        ["slides"] = null,
        ["words"] = """[{"op":"insert_break","at":{"block":2},"position":"after","kind":"page"}]""",
    };

    private static readonly ConcurrentDictionary<string, Lazy<(JsonArray Applied, string? Failure)>> Batches = new(StringComparer.Ordinal);

    // ----- C-R11: a dry run writes nothing and says what it checked ------------------------------

    /// <summary>
    /// Every command that writes files (<see cref="Writes"/>) and declares <c>--dry-run</c>, run
    /// with it: it succeeds, says <c>dryRun: true</c>, writes nothing and lists what it checked, an
    /// edit its operations in <c>applied[]</c>. Whether a writing command declares <c>--dry-run</c>
    /// at all is <c>R11Declared</c>'s question. The generic dry-run invariant already holds an
    /// edit's dry run to writing nothing, so an edit is held here only to its list.
    /// </summary>
    private static IEnumerable<InvariantCase> R11Run(CliCatalog catalog)
    {
        foreach (TreeCommand tree in CommandTree.Of(catalog).Where(static command => !command.Hidden && Writes(command) && command.Has("--dry-run")))
        {
            CliCommand command = tree.Command;
            Invocation invocation = Invocation.For(command);
            if (command.Verb == "compare" && command.Has("--out"))
            {
                // A compare writes only the redline --out names.
                invocation.Add("--out", "output." + ScenarioFixtures.PrimaryFormat(command.Product!));
            }
            invocation.Add("--dry-run");
            var json = new List<JsonAssertion>();
            if (command.Has("--ops"))
            {
                json.Add(new JsonAssertion { Path = "applied[0].op", Exists = true });
            }
            ScenarioExpectation expect = command.Verb == "edit"
                ? new ScenarioExpectation { Json = json }
                : new ScenarioExpectation
                {
                    ExitCode = [0],
                    Json = [new JsonAssertion { Path = "dryRun", Value = true }, .. json],
                    Files = new ScenarioFiles { NothingWritten = true },
                };
            yield return InvariantCases.Case("C-R11", $"{command} --dry-run", InvariantCases.ShardOf(command), slow: false, invocation, expect);
        }
    }

    // ----- C-R12: one naming scheme for multi-file outputs ---------------------------------------

    /// <summary>
    /// Every render, split and image convert of a product writes <c>&lt;stem&gt;.&lt;n&gt;.&lt;ext&gt;</c>,
    /// <c>n</c> padded to the digits of the file count, or <c>&lt;stem&gt;.&lt;sheet&gt;.&lt;ext&gt;</c>
    /// per sheet, and a render of one unit writes <c>--out</c> itself. A render without an
    /// <c>--all-&lt;units&gt;</c> option renders every unit by default. The stem is the <c>--out</c>
    /// file's, or the input's in an <c>--out-dir</c>.
    /// </summary>
    private static IEnumerable<InvariantCase> R12Run(CliCatalog catalog)
    {
        foreach (string product in catalog.Products.Select(static product => product.Id).Where(ScenarioFixtures.Products.Contains))
        {
            string extension = ScenarioFixtures.PrimaryFormat(product);
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} render") is { } render)
            {
                string[] image = ImageTarget(render);
                if (render.Options.FirstOrDefault(static option => option.Name.StartsWith("--all-", StringComparison.Ordinal)) is { } all)
                {
                    yield return OutputNames(product, $"{render} {all.Name}", units: 2,
                        [.. render.Tokens, "<two>", all.Name, .. image, "--out", "page.png"],
                        static names => [.. names.Select(static name => $"page.{name}.png")], perSheet: all.Name == "--all-sheets");
                }
                else
                {
                    yield return OutputNames(product, $"{render} of every unit by default", units: 2,
                        [.. render.Tokens, "<two>", .. image, "--out", "page.png"],
                        static names => [.. names.Select(static name => $"page.{name}.png")]);
                }
                // The seed document may hold more than one unit, so a selection names the first.
                string[] first = render.Options.FirstOrDefault(static option => option.Name is "--pages" or "--slides") is { } selection
                    ? [selection.Name, "1"]
                    : [];
                yield return OutputNames(product, $"{render} one unit", units: 1,
                    [.. render.Tokens, "<one>", .. first, .. image, "--out", "page.png"], static _ => ["page.png"]);
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} split") is { } split)
            {
                string[] parting = split.Option("--by") is { } by && by.AllowedValues.Contains("pages")
                    ? ["--by", "pages", .. split.Has("--every") ? new[] { "--every", "1" } : []]
                    : [];
                yield return OutputNames(product, $"{split} {string.Join(' ', parting)}", units: 2,
                    [.. split.Tokens, "<two>", .. parting, "--out-dir", "parts"],
                    names => [.. names.Select(name => $"parts/two.{name}.{extension}")]);
                if (split.Has("--every") && catalog.Commands.Any(command => command.Words == $"{product} merge"))
                {
                    yield return OutputNames(product, $"{split} {string.Join(' ', parting)} of ten pages", units: 10,
                        [.. split.Tokens, "<ten>", .. parting, "--out-dir", "parts"],
                        names => [.. names.Select(name => $"parts/ten.{name}.{extension}")], slow: true);
                }
            }
            if (catalog.Commands.FirstOrDefault(command => command.Words == $"{product} convert") is { } convert
                && convert.Option("--to") is { } to && to.AllowedValues.Contains("png"))
            {
                yield return OutputNames(product, $"{convert} --to png", units: 2,
                    [.. convert.Tokens, "<two>", "--to", "png", "--out", "page.png"],
                    static names => [.. names.Select(static name => $"page.{name}.png")]);
            }
        }
    }

    private static string[] ImageTarget(CliCommand render) =>
        render.Option("--to") is { } to && to.AllowedValues.Contains("png") ? ["--to", "png"] : [];

    /// <summary>
    /// A case that runs <paramref name="args"/> on a document of <paramref name="units"/> pages,
    /// slides or sheets (<c>&lt;one&gt;</c>, <c>&lt;two&gt;</c> or <c>&lt;ten&gt;</c> in the
    /// arguments) and compares the files it writes with <paramref name="expected"/> of the unit
    /// names: the numbers padded to the digits of the count, or the sheet names.
    /// </summary>
    private static InvariantCase OutputNames(
        string product, string subject, int units, string[] args, Func<IEnumerable<string>, string[]> expected, bool perSheet = false, bool slow = false) =>
        Check("C-R12", subject, () => OutputNameProblems(product, units, args, expected, perSheet), shard: product, slow: slow);

    private static IEnumerable<string> OutputNameProblems(string product, int units, string[] args, Func<IEnumerable<string>, string[]> expected, bool perSheet)
    {
        using var workspace = new RuleWorkspace();
        string input = OperationRuns.SeedInput(workspace, product);
        (string? document, string[] sheets, string? failure) = MultiUnit(workspace, product, input, units);
        if (failure is not null)
        {
            yield return failure;
            yield break;
        }
        if (document is null)
        {
            yield break;
        }
        IReadOnlyDictionary<string, string> before = workspace.Snapshot();
        string token = units switch { 1 => "<one>", 2 => "<two>", _ => "<ten>" };
        RuleRun run = workspace.Json([.. args.Select(arg => arg == token ? document! : arg)]);
        if (run.ExitCode != 0)
        {
            if (!run.Unjudged)
            {
                yield return run.Describe();
            }
            yield break;
        }
        int digits = units.ToString(CultureInfo.InvariantCulture).Length;
        IEnumerable<string> names = perSheet
            ? sheets
            : Enumerable.Range(1, units).Select(number => number.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'));
        string[] wanted = [.. expected(names).Order(StringComparer.Ordinal)];
        string[] written = [.. RuleWorkspace.Written(before, workspace.Snapshot()).Where(static path => !path.StartsWith('<'))];
        if (!written.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            yield return $"wrote {string.Join(", ", written)}; expected {string.Join(", ", wanted)}";
        }
    }

    /// <summary>
    /// The fixture with one, two or ten pages, slides or sheets, with its sheet names; neither a
    /// document nor a failure when evaluation mode cannot build it.
    /// </summary>
    private static (string? Document, string[] Sheets, string? Failure) MultiUnit(RuleWorkspace workspace, string product, string input, int units)
    {
        string extension = ScenarioFixtures.PrimaryFormat(product);
        if (units == 1)
        {
            return (input, [], null);
        }
        string name = (units == 2 ? "two." : "ten.") + extension;
        RuleRun? run = null;
        if (CliCatalog.Current.Commands.Any(command => command.Words == $"{product} merge"))
        {
            run = workspace.Json([product, "merge", .. Enumerable.Repeat(input, units), "--out", name]);
        }
        else if (units == 2 && SecondUnit[product] is { } operations)
        {
            run = OperationRuns.Edit(workspace, product, input, name, JsonNode.Parse(operations)!.AsArray());
        }
        else if (units == 2)
        {
            File.Copy(workspace.File(input), workspace.File(name));
        }
        else
        {
            return (null, [], $"{product} cannot build a document of {units} units");
        }
        if (run is { ExitCode: not 0 })
        {
            return (null, [], run.Unjudged ? null : "building the document failed: " + run.Describe());
        }
        // The sheets the product sees, which in evaluation mode include the warning sheets it adds.
        RuleRun inspected = workspace.Json(product, "inspect", name);
        return (name, [.. OperationRuns.Sheets(inspected.Document)], null);
    }

    // ----- C-R15: a read or search over its budget truncates and continues -----------------------

    /// <summary>
    /// Every read and search, with each <c>--max-&lt;unit&gt;</c> budget at 1: it succeeds with a
    /// truncated window that names the next call, in the unit the command selects by (a search
    /// counts hits), both for its default selection and for a selection of every unit.
    /// </summary>
    private static IEnumerable<InvariantCase> R15Run(CliCatalog catalog)
    {
        foreach (CliCommand command in catalog.Commands.Where(static command => !command.Hidden && command.Product is not null
            && (command.Tokens.Contains("query") || command.Verb is "read" or "search")))
        {
            bool search = command.Has("--pattern");
            string? selection = SelectionUnits.Keys.FirstOrDefault(command.Has);
            string unit = search ? "hit" : selection is null ? "?" : SelectionUnits[selection];
            foreach (CliOption budget in command.Options.Where(static option => option.Name.StartsWith("--max-", StringComparison.Ordinal)
                && option.Name != "--max-input-bytes"))
            {
                string[][] selections = selection is null ? [[]] : [[], [selection, WholeSelections[selection]]];
                foreach (string[] selected in selections)
                {
                    Invocation invocation = Invocation.For(command, "--pattern");
                    if (search)
                    {
                        // A pattern with several matches in every fixture.
                        invocation.Add("--pattern", "o");
                    }
                    invocation.Add(selected).Add(budget.Name, "1");
                    string subject = $"{command}{(selected.Length == 0 ? string.Empty : " " + string.Join(' ', selected))} {budget.Name} 1";
                    yield return InvariantCases.Case("C-R15", subject, command.Product!, slow: false, invocation, new ScenarioExpectation
                    {
                        ExitCode = [0],
                        Json =
                        [
                            new JsonAssertion { Path = "window.truncated", Value = true },
                            new JsonAssertion { Path = "window.next", Exists = true },
                            new JsonAssertion { Path = "window.unit", Value = unit },
                        ],
                    });
                }
            }
        }
    }

    // ----- C-R19: set_ replaces, add_ adds ---------------------------------------------------------

    /// <summary>
    /// Every <c>set_</c> operation applied twice in one batch leaves the state that applying it
    /// once leaves; every <c>add_</c> operation applied twice leaves another document than once,
    /// unless its sample says why it cannot be repeated. A <c>set_</c> compares states, not bytes.
    /// </summary>
    private static IEnumerable<InvariantCase> R19Run(CliCatalog catalog)
    {
        foreach (IGrouping<string, OperationSample> product in FirstPerOperation().GroupBy(static sample => sample.Product))
        {
            OperationSample[] sets = [.. product.Where(static sample => sample.Op.StartsWith("set_", StringComparison.Ordinal))];
            OperationSample[] adds = [.. product.Where(static sample => sample.Op.StartsWith("add_", StringComparison.Ordinal) && sample.Unrepeatable is null)];
            foreach (OperationSample sample in sets)
            {
                yield return Check("C-R19", $"{sample.Subject} applied twice", () => Twice(sample, replaces: true),
                    shard: sample.Product, slow: sample != sets[0]);
            }
            foreach (OperationSample sample in adds)
            {
                yield return Check("C-R19", $"{sample.Subject} applied twice", () => Twice(sample, replaces: false),
                    shard: sample.Product, slow: sample != adds[0]);
            }
        }
    }

    private static IEnumerable<OperationSample> FirstPerOperation() =>
        OperationSamples.All.Where(static sample => sample.Label is null)
            .GroupBy(static sample => (sample.Product, sample.Op)).Select(static group => group.First());

    private static IEnumerable<string> Twice(OperationSample sample, bool replaces)
    {
        using var workspace = new RuleWorkspace();
        (string? source, JsonObject operation, string? failure) = OperationRuns.Prepare(workspace, sample);
        if (failure is not null)
        {
            yield return failure;
            yield break;
        }
        if (source is null)
        {
            yield break;
        }
        string extension = OperationRuns.Extension(sample.Product);
        string[] open = OperationRuns.Open(sample);
        RuleRun once = OperationRuns.Edit(workspace, sample.Product, source!, "once." + extension,
            [OperationRuns.WithId(operation, "first")], open);
        RuleRun twice = OperationRuns.Edit(workspace, sample.Product, source!, "twice." + extension,
            [OperationRuns.WithId(operation, "first"), OperationRuns.WithId(operation, "second")], open);
        if (once.Unjudged || twice.Unjudged)
        {
            yield break;
        }
        if (once.ExitCode != 0 || twice.ExitCode != 0)
        {
            yield return $"applied {(once.ExitCode != 0 ? "once" : "twice")}, it fails: {(once.ExitCode != 0 ? once : twice).Describe()}";
            yield break;
        }
        if (replaces)
        {
            if (OperationRuns.FirstDifference(
                OperationRuns.State(workspace, sample.Product, "once." + extension, open),
                OperationRuns.State(workspace, sample.Product, "twice." + extension, open)) is { } difference)
            {
                yield return $"applied twice, it leaves another state than applied once, from {difference}";
            }
        }
        else if (!OperationRuns.Differ(workspace, sample.Product, "once." + extension, "twice." + extension, open, OperationRuns.Comparison.Accumulation))
        {
            yield return $"applied twice, it leaves {OperationRuns.Sameness(sample.Product)} as applied once: the second replaces the first";
        }
    }

    // ----- C-R21: what an operation writes reads back ----------------------------------------------

    /// <summary>
    /// Every operation with a state field that <see cref="StateFields"/> gives a read path: its
    /// sample's edit, then the one command whose result the path's schema describes, before and
    /// after. Each state field the sample writes must hold the written value under the read name
    /// at a place that did not hold it before. Whether the read name is the op field's name, and
    /// the fields no read reports, are <c>R21Declared</c>'s questions; a state the table says reads
    /// back by position is not judged. An operation with read paths but no sample fails.
    /// </summary>
    private static IEnumerable<InvariantCase> R21Run(CliCatalog catalog)
    {
        var first = new HashSet<string>(StringComparer.Ordinal);
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            foreach (string op in product.Ops)
            {
                string prefix = $"{product.Id} {op}.";
                (string Field, string Path)[] fields =
                [
                    .. StateFields.Where(pair => pair.Value is not null && pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                        .Select(pair => (pair.Key[prefix.Length..], pair.Value!)),
                ];
                if (fields.Length == 0)
                {
                    continue;
                }
                OperationSample[] samples = [.. OperationSamples.All.Where(item => item.Product == product.Id && item.Op == op)];
                yield return samples.Length == 0
                    ? Check("C-R21", $"{product.Id} {op} reads back", () => [$"{product.Id} {op} has state fields with read paths but no operation sample"])
                    : Check("C-R21", $"{product.Id} {op} reads back", () => ReadsBack(samples, fields), shard: product.Id, slow: !first.Add(product.Id));
            }
        }
    }

    /// <summary>
    /// Runs every sample of the operation that writes one of its state fields with a read path;
    /// a state field no sample writes is a problem of its own, so no field goes unjudged.
    /// </summary>
    private static IEnumerable<string> ReadsBack(OperationSample[] samples, (string Field, string Path)[] fields)
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (OperationSample sample in samples)
        {
            JsonObject operation = OperationRuns.Operation(sample);
            (string Field, string Path)[] writes = [.. fields.Where(field => Written(operation, field.Field.Split('.')).Any())];
            if (writes.Length == 0)
            {
                continue;
            }
            covered.UnionWith(writes.Select(static field => field.Field));
            foreach (string problem in ReadsBack(sample, writes))
            {
                yield return problem;
            }
        }
        foreach ((string field, _) in fields.Where(field => !covered.Contains(field.Field)))
        {
            yield return $"{field} is written by no sample";
        }
    }

    private static IEnumerable<string> ReadsBack(OperationSample sample, (string Field, string Path)[] fields)
    {
        using var workspace = new RuleWorkspace();
        (string? source, JsonObject operation, string? failure) = OperationRuns.Prepare(workspace, sample);
        if (failure is not null)
        {
            yield return failure;
            yield break;
        }
        if (source is null)
        {
            yield break;
        }
        string output = "output." + OperationRuns.Extension(sample.Product);
        string[] open = OperationRuns.Open(sample);
        RuleRun edit = OperationRuns.Edit(workspace, sample.Product, source, output, [OperationRuns.WithId(operation)], open);
        if (edit.ExitCode != 0)
        {
            if (!edit.Unjudged)
            {
                yield return edit.Describe();
            }
            yield break;
        }
        var reads = new Dictionary<string, (JsonArray Before, JsonArray After)>(StringComparer.Ordinal);
        foreach ((string field, string path) in fields)
        {
            JsonNode?[] written = [.. Written(operation, field.Split('.'))];
            string schemaId = path[..path.IndexOf('#', StringComparison.Ordinal)];
            string read = path[(path.LastIndexOfAny(['/', '#']) + 1)..];
            if (!reads.TryGetValue(schemaId, out (JsonArray Before, JsonArray After) state))
            {
                state = (OperationRuns.Read(workspace, sample.Product, source, open, schemaId), OperationRuns.Read(workspace, sample.Product, output, open, schemaId));
                reads[schemaId] = state;
            }
            foreach (JsonNode? value in written)
            {
                (string Location, JsonNode? Value)[] hits = [.. Locate(state.After, read).Where(hit => OperationRuns.Holds(value, hit.Value))];
                if (hits.Any(hit => IsNew(state.Before, state.After, hit.Location, value)))
                {
                    continue;
                }
                string command = state.After[0]!["command"]!.GetValue<string>();
                string problem = hits.Length == 0
                    ? $"{field} {OperationRuns.Show(value)} does not read back as {read} in {command}"
                    : $"{field} {OperationRuns.Show(value)} does not read back as a new {read}: {hits[0].Location} held it before";
                if (value?.GetValueKind() is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array
                    && Elsewhere(state.Before, state.After, value).FirstOrDefault() is { } elsewhere)
                {
                    // Where a text or structured value did arrive: the evidence of another name or shape.
                    problem += $" (it reads back as {elsewhere})";
                }
                yield return problem;
            }
        }
    }

    /// <summary>The values an operation writes at a dotted field path, one per item of a list on the way.</summary>
    private static IEnumerable<JsonNode?> Written(JsonNode? node, string[] path)
    {
        if (path.Length == 0)
        {
            yield return node;
            yield break;
        }
        if (node is JsonArray items)
        {
            foreach (JsonNode? item in items)
            {
                foreach (JsonNode? value in Written(item, path))
                {
                    yield return value;
                }
            }
        }
        else if (node is JsonObject obj && obj.TryGetPropertyValue(path[0], out JsonNode? next))
        {
            foreach (JsonNode? value in Written(next, path[1..]))
            {
                yield return value;
            }
        }
    }

    /// <summary>Every place a dotted read path ends, at any depth of any result, as <c>command: path</c>.</summary>
    private static IEnumerable<(string Location, JsonNode? Value)> Locate(JsonArray state, string read)
    {
        string[] segments = read.Split('.');
        foreach (JsonNode? item in state)
        {
            string command = item!["command"]!.GetValue<string>();
            foreach ((string path, string name, JsonNode? value) in OperationRuns.Walk(item["result"], string.Empty))
            {
                if (name != segments[0])
                {
                    continue;
                }
                JsonNode? current = value;
                bool found = true;
                foreach (string segment in segments.Skip(1))
                {
                    if (current is JsonObject obj && obj.TryGetPropertyValue(segment, out JsonNode? next))
                    {
                        current = next;
                    }
                    else
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    yield return ($"{command}: {string.Join('.', [path, .. segments.Skip(1)])}", current);
                }
            }
        }
    }

    /// <summary>
    /// Whether a place holds a written value that it did not hold before: the place did not
    /// exist, held another value, or belongs to another object than before, such as a sheet that
    /// moved into its position.
    /// </summary>
    private static bool IsNew(JsonArray before, JsonArray after, string location, JsonNode? value)
    {
        (JsonNode? old, bool existed) = At(before, location);
        if (!existed || !OperationRuns.Holds(value, old))
        {
            return true;
        }
        string parent = ParentOf().Replace(location, string.Empty);
        return !JsonNode.DeepEquals(At(before, parent).Value, At(after, parent).Value);
    }

    private static (JsonNode? Value, bool Exists) At(JsonArray state, string location)
    {
        int colon = location.IndexOf(": ", StringComparison.Ordinal);
        string command = location[..colon];
        JsonNode? result = state.FirstOrDefault(item => item!["command"]!.GetValue<string>() == command)?["result"];
        bool exists = ScenarioRunner.TryResolve(result, location[(colon + 2)..], out JsonNode? value);
        return (value, exists && result is not null);
    }

    /// <summary>The new places, in document order, that hold a written value under any name.</summary>
    private static IEnumerable<string> Elsewhere(JsonArray before, JsonArray after, JsonNode? value)
    {
        foreach (JsonNode? item in after)
        {
            string command = item!["command"]!.GetValue<string>();
            foreach ((string path, _, JsonNode? candidate) in OperationRuns.Walk(item["result"], string.Empty))
            {
                string location = $"{command}: {path}";
                if (OperationRuns.Holds(value, candidate) && IsNew(before, after, location, value))
                {
                    yield return location;
                }
            }
        }
    }

    [GeneratedRegex(@"(\.[^.\[]+|\[\d+\])$")]
    private static partial Regex ParentOf();

    // ----- C-R30: an outcome tells the truth -------------------------------------------------------

    /// <summary>
    /// Every operation sample: <c>itemsAffected</c> is 0 exactly when the result warns
    /// <see cref="NoEffectWarning"/>, and an operation that affected items wrote an output that
    /// differs from its input (<see cref="OperationRuns.Differ"/>). Per product, one batch of the
    /// samples shows that no <c>applied[]</c> entry carries <c>index</c> and that every target
    /// reads <c>&lt;unit&gt;/&lt;address&gt;</c>.
    /// </summary>
    private static IEnumerable<InvariantCase> R30Run(CliCatalog catalog)
    {
        var first = new HashSet<string>(StringComparer.Ordinal);
        foreach (OperationSample sample in OperationSamples.All)
        {
            yield return Check("C-R30", sample.Subject, () => Outcome(sample), shard: sample.Product,
                slow: sample.Label is null && !first.Add(sample.Product));
        }
        string[] products = [.. catalog.Products.Select(static product => product.Id).Where(ScenarioFixtures.Products.Contains)];
        foreach (string product in products)
        {
            yield return Check("C-R30", $"{product} operations have samples", () => SampleCoverage(catalog.Product(product)));
            yield return Check("C-R30", $"{product} applied[] has no index", () => Batch(product, IndexProblems), shard: product);
            yield return Check("C-R30", $"{product} targets read <unit>/<address>",
                () => Batch(product, applied => TargetProblems(applied, products)), shard: product);
        }
    }

    private static IEnumerable<string> Outcome(OperationSample sample)
    {
        using var workspace = new RuleWorkspace();
        (string? source, JsonObject operation, string? failure) = OperationRuns.Prepare(workspace, sample);
        if (failure is not null)
        {
            yield return failure;
            yield break;
        }
        if (source is null)
        {
            yield break;
        }
        string output = "output." + OperationRuns.Extension(sample.Product);
        string[] open = OperationRuns.Open(sample);
        RuleRun edit = OperationRuns.Edit(workspace, sample.Product, source!, output, [OperationRuns.WithId(operation)], open);
        if (edit.ExitCode != 0)
        {
            if (!edit.Unjudged)
            {
                yield return edit.Describe();
            }
            yield break;
        }
        if (edit.Document?["applied"]?[0]?["itemsAffected"] is not JsonValue count)
        {
            yield return "the result has no applied[0].itemsAffected";
            yield break;
        }
        int affected = count.GetValue<int>();
        // The evaluation warning says nothing about the operation's effect.
        string[] warnings = [.. ScenarioContract.Codes(edit.Document?["warnings"]).Where(static code => code != "EVAL_MODE")];
        bool noEffect = warnings.Contains(NoEffectWarning, StringComparer.Ordinal);
        if (affected == 0 && !noEffect)
        {
            yield return $"itemsAffected is 0 without an {NoEffectWarning} warning" + (warnings.Length == 0 ? string.Empty : $" (warns {string.Join(", ", warnings)})");
        }
        else if (affected > 0 && noEffect)
        {
            yield return $"itemsAffected is above 0 with an {NoEffectWarning} warning";
        }
        if (affected > 0 && sample.Unseen is null)
        {
            JsonArray? before = sample.OnFixture ? OperationRuns.State(workspace, sample.Product, source!, open, fixture: true) : null;
            if (!OperationRuns.Differ(workspace, sample.Product, source!, output, open, OperationRuns.Comparison.Effect, before))
            {
                // The count is left out: it is not what the rule judges, and it varies with the license mode.
                yield return $"itemsAffected is above 0 but the output has {OperationRuns.Sameness(sample.Product)} as the input";
            }
        }
    }

    /// <summary>
    /// Checks the applied entries of one best-effort edit of the product's fixture with every
    /// <c>set_</c>, <c>add_</c> and <c>format_</c> sample that needs no setup: operations that
    /// neither remove nor restructure what a later one addresses. The edit runs once per test
    /// process and the product's batch cases share it.
    /// </summary>
    private static IEnumerable<string> Batch(string product, Func<JsonArray, IEnumerable<string>> problems)
    {
        (JsonArray applied, string? failure) = Batches.GetOrAdd(product, static key => new Lazy<(JsonArray, string?)>(() =>
        {
            using var workspace = new RuleWorkspace();
            string input = OperationRuns.SeedInput(workspace, key);
            JsonArray operations = [.. OperationSamples.All
                .Where(sample => sample.Product == key && sample.OnFixture && sample.Label is null
                    && sample.Op.Split('_')[0] is "set" or "add" or "format")
                .Select(static sample => (JsonNode)OperationRuns.Operation(sample, sample.Op))];
            RuleRun run = OperationRuns.Edit(workspace, key, input, "batch." + OperationRuns.Extension(key), operations, "--best-effort");
            return run.Document?["applied"] is JsonArray entries && run.ExitCode is 0 or 8
                ? ((JsonArray)entries.DeepClone(), null)
                : ([], "the batch edit failed: " + run.Describe());
        })).Value;
        return failure is null ? problems(applied) : [failure];
    }

    /// <summary>Every operation of the product has a sample or an exemption with its reason, and every exemption still applies.</summary>
    private static IEnumerable<string> SampleCoverage(CliProduct product)
    {
        HashSet<string> sampled = [.. OperationSamples.All.Where(sample => sample.Product == product.Id).Select(static sample => sample.Op)];
        foreach (string op in product.Ops.Where(op => !sampled.Contains(op) && !OperationSamples.Exempt.ContainsKey($"{product.Id} {op}")))
        {
            yield return $"{op} has no operation sample";
        }
        foreach (string op in sampled.Where(op => !product.Ops.Contains(op)).Order(StringComparer.Ordinal))
        {
            yield return $"the sample of {op} names no operation of the catalog";
        }
        foreach (string key in OperationSamples.Exempt.Keys.Where(key => key.StartsWith(product.Id + " ", StringComparison.Ordinal)))
        {
            string op = key[(product.Id.Length + 1)..];
            if (sampled.Contains(op) || !product.Ops.Contains(op))
            {
                yield return $"the exemption of {op} no longer applies";
            }
        }
    }

    private static IEnumerable<string> IndexProblems(JsonArray applied)
    {
        if (applied.Count == 0)
        {
            yield return "the batch result has no applied[] entry";
        }
        string[] indexed = [.. applied.Where(static entry => entry is JsonObject obj && obj.ContainsKey("index"))
            .Select(static entry => entry!["op"]!.GetValue<string>())];
        if (indexed.Length > 0)
        {
            yield return indexed.Length == applied.Count
                ? "every applied[] entry carries index"
                : "applied[] entries carry index: " + string.Join(", ", indexed);
        }
    }

    private static IEnumerable<string> TargetProblems(JsonArray applied, string[] products)
    {
        if (applied.Count == 0)
        {
            yield return "the batch result has no applied[] entry";
        }
        string[] untargeted = [.. applied.Where(static entry => entry?["targets"] is not JsonArray).Select(static entry => entry?["op"]?.GetValue<string>() ?? "?")];
        if (untargeted.Length > 0)
        {
            yield return "applied[] entries without targets: " + string.Join(", ", untargeted);
        }
        string[] outside =
        [
            .. applied.Where(static entry => entry?["targets"] is JsonArray).Select(entry => (
                    Op: entry!["op"]!.GetValue<string>(),
                    Target: entry["targets"]!.AsArray().Select(static target => target!.GetValue<string>())
                        .FirstOrDefault(target => !IsUnitAddress(target, products))))
                .Where(static item => item.Target is not null)
                .Select(static item => $"{item.Op} {item.Target}")
                .Order(StringComparer.Ordinal),
        ];
        if (outside.Length > 0)
        {
            yield return "targets outside <unit>/<address>: " + string.Join("; ", outside);
        }
    }

    /// <summary>A target that names a unit, such as <c>slide</c>, then its address; a product name is not a unit.</summary>
    private static bool IsUnitAddress(string target, string[] products) =>
        UnitAddress().Match(target) is { Success: true } match && !products.Contains(match.Groups["unit"].Value);

    [GeneratedRegex(@"^(?<unit>[a-z]+(?:-[a-z]+)*)/\S")]
    private static partial Regex UnitAddress();
}
