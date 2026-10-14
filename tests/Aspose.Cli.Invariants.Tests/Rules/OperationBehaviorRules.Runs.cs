using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// Runs operation samples and compares documents for the behavior rules. A document's state is
/// what the product's own commands say about it: <c>inspect</c> with every <c>--detail</c> section
/// and <c>--sample</c>, and every read command (each sheet of a workbook) with its fullest scope,
/// without the envelope, the file identity and timestamps. Two documents differ when their states
/// differ, when their package parts differ (Office formats, relationship order and the saved time
/// aside) or, for other formats, when their rendered pages differ.
/// </summary>
internal static partial class OperationRuns
{
    /// <summary>A small valid image for the operations that place one.</summary>
    private const string ImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC";

    private static readonly string[] EnvelopeKeys = ["schema", "schemaVersion", "source", "input", "license", "warnings"];

    private static readonly ConcurrentDictionary<string, Lazy<JsonArray>> FixtureStates = new(StringComparer.Ordinal);

    /// <summary>The probe command whose result names each read schema id, as the probes learn them.</summary>
    private static readonly ConcurrentDictionary<string, string> SchemaCommands = new(StringComparer.Ordinal);

    public static string Extension(string product) => ScenarioFixtures.PrimaryFormat(product);

    /// <summary>The options that open an encrypted sample's documents.</summary>
    public static string[] Open(OperationSample sample) => sample.Encrypted ? ["--password-env", RuleWorkspace.SecretVariable] : [];

    /// <summary>Seeds the fixture as <c>input.&lt;format&gt;</c> and every file a sample may read.</summary>
    public static string SeedInput(RuleWorkspace workspace, string product)
    {
        string extension = Extension(product);
        byte[] fixture = ScenarioFixtures.Read($"{product}.{extension}");
        workspace.Seed("input." + extension, fixture);
        workspace.Seed("other." + extension, fixture);
        workspace.Seed("image.png", Convert.FromBase64String(ImageBase64));
        workspace.Seed("note.txt", "A note.\n");
        return "input." + extension;
    }

    /// <summary>An edit of <paramref name="input"/> with the given operations, written to <paramref name="output"/>.</summary>
    public static RuleRun Edit(RuleWorkspace workspace, string product, string input, string output, JsonArray operations, params string[] options)
    {
        string ops = Path.GetFileNameWithoutExtension(output) + ".ops.json";
        workspace.Seed(ops, new JsonObject { ["ops"] = operations }.ToJsonString());
        return workspace.Json([product, "edit", input, "--ops", ops, "--out", output, .. options]);
    }

    /// <summary>
    /// Seeds the workspace and applies the sample's setup: the document the operation edits and
    /// the operation, with the id of the shape it addresses when the setup added that shape, or
    /// the failure of the setup edit. Neither a document nor a failure means evaluation mode
    /// cannot run the setup (<see cref="RuleRun.Unjudged"/>).
    /// </summary>
    public static (string? Document, JsonObject Operation, string? Failure) Prepare(RuleWorkspace workspace, OperationSample sample)
    {
        JsonObject operation = JsonNode.Parse(sample.Json)!.AsObject();
        string input = SeedInput(workspace, sample.Product);
        if (sample.FromHtml is { } html)
        {
            // A document the fixture cannot be, such as a PDF with a form, built from HTML.
            workspace.Seed("source.html", html);
            input = "from-html." + Extension(sample.Product);
            RuleRun created = workspace.Json(sample.Product, "create", input, "--from-html", "source.html");
            if (created.ExitCode != 0)
            {
                return (null, operation, created.Unjudged ? null : "creating the document failed: " + created.Describe());
            }
        }
        if (sample.Setup is null)
        {
            return (input, operation, null);
        }
        string based = "base." + Extension(sample.Product);
        RuleRun setup = Edit(workspace, sample.Product, input, based, JsonNode.Parse(sample.Setup)!.AsArray(), [.. sample.SetupArgs ?? []]);
        if (setup.ExitCode != 0)
        {
            return (null, operation, setup.Unjudged ? null : "the setup edit failed: " + setup.Describe());
        }
        if (sample.Shape is { } type)
        {
            // The read of the operation's slide lists each shape with its id and kind.
            CliCommand read = CliCatalog.Current.Commands.First(command => IsRead(command, sample.Product) && command.Has("--slides"));
            string slide = operation["slide"]!.ToJsonString();
            RuleRun slides = workspace.Json([.. read.Tokens, based, "--slides", slide]);
            JsonNode? shape = (slides.Document?["slides"]?[0]?["shapes"] as JsonArray ?? [])
                .FirstOrDefault(item => item?["kind"]?.GetValue<string>() == type);
            if (shape?["shapeId"] is not { } id)
            {
                return (null, operation, $"the setup added no {type} on slide {slide}: {(slides.ExitCode == 0 ? "none listed" : slides.Describe())}");
            }
            operation["shapeId"] = id.DeepClone();
        }
        return (based, operation, null);
    }

    /// <summary>A copy of an operation with the given id.</summary>
    public static JsonObject WithId(JsonObject operation, string? id = null)
    {
        var copy = (JsonObject)operation.DeepClone();
        if (id is not null)
        {
            copy["id"] = id;
        }
        return copy;
    }

    /// <summary>A sample's operation with the given id, for a batch without setup.</summary>
    public static JsonObject Operation(OperationSample sample, string? id = null) => WithId(JsonNode.Parse(sample.Json)!.AsObject(), id);

    /// <summary>
    /// The state of a document. The unedited fixture's state is computed once per product and
    /// shared, so it must be asked for with <paramref name="fixture"/>.
    /// </summary>
    public static JsonArray State(RuleWorkspace workspace, string product, string file, string[] open, bool fixture = false)
    {
        if (fixture && open.Length == 0)
        {
            return FixtureStates.GetOrAdd(product, key => new Lazy<JsonArray>(() =>
            {
                using var own = new RuleWorkspace();
                return Probe(own, key, SeedInput(own, key), []);
            })).Value;
        }
        return Probe(workspace, product, file, open);
    }

    private static JsonArray Probe(RuleWorkspace workspace, string product, string file, string[] open)
    {
        CliCatalog catalog = CliCatalog.Current;
        var state = new JsonArray();
        string[] sheets = [];
        foreach (CliCommand command in Probes(catalog, product))
        {
            (JsonArray results, string[] seen) = ProbeWith(workspace, command, file, open, sheets);
            sheets = command.Verb == "inspect" ? seen : sheets;
            foreach (JsonNode? result in results)
            {
                state.Add(result!.DeepClone());
            }
        }
        return state;
    }

    /// <summary>
    /// What the one command that writes a read schema says about a document, as a state of that
    /// command alone: the read of C-R21, which runs no other probe. The command is found once per
    /// test process, by the schema each probe command's result names.
    /// </summary>
    public static JsonArray Read(RuleWorkspace workspace, string product, string file, string[] open, string schemaId)
    {
        CliCatalog catalog = CliCatalog.Current;
        string[] sheets = [];
        if (SchemaCommands.TryGetValue(schemaId, out string? known))
        {
            CliCommand command = catalog.Command(known);
            // A read of one sheet reads the active one, which the samples edit.
            return ProbeWith(workspace, command, file, open, sheets).Results;
        }
        foreach (CliCommand command in Probes(catalog, product))
        {
            // A read of one sheet reads the active one here too, so that the first read of a
            // schema in a test process reports what every later one does.
            (JsonArray results, _) = ProbeWith(workspace, command, file, open, sheets);
            if (SchemaCommands.TryGetValue(schemaId, out string? found) && found == command.Words)
            {
                return results;
            }
        }
        return [Labeled("no command", JsonValue.Create($"no {product} command writes {schemaId}"))];
    }

    /// <summary>The product's inspect, then each of its read commands.</summary>
    private static IEnumerable<CliCommand> Probes(CliCatalog catalog, string product) =>
        [catalog.Command($"{product} inspect"), .. catalog.Commands.Where(command => IsRead(command, product))];

    /// <summary>
    /// One probe command on a document: inspect with every detail and its preview, a read with its
    /// fullest scope and notes, once per sheet when it reads one sheet; with the sheets an inspect saw.
    /// </summary>
    private static (JsonArray Results, string[] Sheets) ProbeWith(RuleWorkspace workspace, CliCommand command, string file, string[] open, string[] sheets)
    {
        List<string> args = [.. command.Tokens, file, .. open];
        if (command.Option("--detail") is { AllowedValues.Count: > 0 } detail)
        {
            args.Add("--detail");
            args.AddRange(detail.AllowedValues);
        }
        if (command.Has("--sample"))
        {
            args.Add("--sample");
        }
        // The deepest projection a read offers: its --content depth, or a --scope that names one.
        foreach (string depth in new[] { "--content", "--scope" })
        {
            if (command.Option(depth) is { } projection && projection.AllowedValues.Contains("full"))
            {
                args.AddRange([depth, "full"]);
                break;
            }
        }
        if (command.Has("--notes"))
        {
            args.Add("--notes");
        }
        var results = new JsonArray();
        if (command.Has("--sheet") && sheets.Length > 0)
        {
            foreach (string sheet in sheets)
            {
                RuleRun run = workspace.Json([.. args, "--sheet", sheet]);
                Learn(command, run);
                results.Add(Labeled($"{command.Words} ({sheet})", Normalize(run, file)));
            }
            return (results, []);
        }
        RuleRun single = workspace.Json([.. args]);
        Learn(command, single);
        results.Add(Labeled(command.Words, Normalize(single, file)));
        return (results, [.. Sheets(single.Document)]);
    }

    private static void Learn(CliCommand command, RuleRun run)
    {
        if (run.Document?["schema"]?.GetValue<string>() is { } uri && SchemaId().Match(uri) is { Success: true } match)
        {
            SchemaCommands.TryAdd(match.Groups["id"].Value, command.Words);
        }
    }

    /// <summary>A product command that reads a document without writing or searching it: a <c>read</c>.</summary>
    private static bool IsRead(CliCommand command, string product) =>
        command.Product == product && !command.Hidden && command.Verb == "read"
        && command.Arguments.Any(static argument => argument.Required && argument.InputKind == "file")
        && !command.Has("--pattern") && !command.Has("--out") && !command.Has("--out-dir");

    public static IEnumerable<string> Sheets(JsonNode? inspected) =>
        Walk(inspected, string.Empty)
            .Where(static item => item.Name == "sheets" && item.Value is JsonArray)
            .SelectMany(static item => item.Value!.AsArray())
            .Select(static sheet => sheet?["name"]?.GetValue<string>())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

    private static JsonObject Labeled(string label, JsonNode? document) => new() { ["command"] = label, ["result"] = document };

    /// <summary>A result without its envelope, its file identity and its timestamps; a failure as its error code.</summary>
    private static JsonNode? Normalize(RuleRun run, string file)
    {
        if (run.ExitCode != 0)
        {
            return JsonValue.Create("error " + (run.Error ?? run.ExitCode.ToString(CultureInfo.InvariantCulture)));
        }
        string name = Path.GetFileName(file);
        return Strip(run.Document, name);

        static JsonNode? Strip(JsonNode? node, string name) => node switch
        {
            JsonObject obj => new JsonObject(obj.Where(static pair => !EnvelopeKeys.Contains(pair.Key))
                .Select(pair => KeyValuePair.Create(pair.Key, Strip(pair.Value, name)))),
            JsonArray array => new JsonArray([.. array.Select(item => Strip(item, name))]),
            JsonValue value when value.TryGetValue(out string? text) => JsonValue.Create(
                text == name || text.EndsWith("\\" + name, StringComparison.OrdinalIgnoreCase) ? "<file>"
                : IsoDateTime().IsMatch(text) ? "<date>"
                : text),
            null => null,
            _ => node.DeepClone(),
        };
    }

    /// <summary>The first difference between two states, as the command and the path where they part; null when they are equal.</summary>
    public static string? FirstDifference(JsonArray left, JsonArray right)
    {
        static Dictionary<string, JsonNode?> Results(JsonArray state) => state.ToDictionary(
            static item => item!["command"]!.GetValue<string>(), static item => item!["result"], StringComparer.Ordinal);
        Dictionary<string, JsonNode?> a = Results(left);
        Dictionary<string, JsonNode?> b = Results(right);
        foreach (string command in a.Keys.Concat(b.Keys).Distinct(StringComparer.Ordinal))
        {
            if (!a.TryGetValue(command, out JsonNode? x) || !b.TryGetValue(command, out JsonNode? y))
            {
                return $"{command} runs for one document only";
            }
            if (Difference(x, y, string.Empty) is { } path)
            {
                return $"{command}: {(path.Length == 0 ? "the result" : path)}";
            }
        }
        return null;

        static string? Difference(JsonNode? x, JsonNode? y, string path)
        {
            if (JsonNode.DeepEquals(x, y))
            {
                return null;
            }
            if (x is JsonObject a && y is JsonObject b)
            {
                foreach (string key in a.Select(static pair => pair.Key).Union(b.Select(static pair => pair.Key)))
                {
                    if (Difference(a[key], b[key], Join(path, key)) is { } found)
                    {
                        return found;
                    }
                }
            }
            if (x is JsonArray c && y is JsonArray d && c.Count == d.Count)
            {
                for (int index = 0; index < c.Count; index++)
                {
                    if (Difference(c[index], d[index], $"{path}[{index}]") is { } found)
                    {
                        return found;
                    }
                }
            }
            return path;
        }
    }

    // ----- output comparisons ------------------------------------------------------------------

    /// <summary>
    /// How two documents are compared: whether an edit changed its input (<see cref="Effect"/>), or
    /// whether an operation applied twice added to what it added once (<see cref="Accumulation"/>),
    /// where the generated identifiers of the second copy do not count as a difference.
    /// </summary>
    public enum Comparison
    {
        Effect,
        Accumulation,
    }

    /// <summary>The evidence two documents are the same, in one phrase, for a message.</summary>
    public static string Sameness(string product) => IsPackage(product)
        ? "the same package parts and the same inspect and read results"
        : "the same file content, inspect and read results and rendered pages";

    /// <summary>Whether two documents differ; <paramref name="leftState"/> is the left document's state when it is already known.</summary>
    public static bool Differ(
        RuleWorkspace workspace, string product, string left, string right, string[] open, Comparison comparison, JsonArray? leftState = null)
    {
        if (IsPackage(product))
        {
            if (!PackageParts(workspace.File(left), comparison).SequenceEqual(PackageParts(workspace.File(right), comparison)))
            {
                return true;
            }
        }
        else if (comparison == Comparison.Accumulation && !MaskedBytes(workspace.File(left)).SequenceEqual(MaskedBytes(workspace.File(right))))
        {
            return true;
        }
        JsonArray before = leftState ?? State(workspace, product, left, open);
        if (!JsonNode.DeepEquals(before, State(workspace, product, right, open)))
        {
            return true;
        }
        return !IsPackage(product) && !Rendered(workspace, product, left, open).SequenceEqual(Rendered(workspace, product, right, open));
    }

    private static bool IsPackage(string product) => Extension(product) is "xlsx" or "docx" or "pptx";

    /// <summary>
    /// The parts of an Office package in name order, each normalized: relationships sorted, the
    /// saved time removed and, for <see cref="Comparison.Accumulation"/>, the digits of attribute
    /// values masked, so that the identifiers a copy is given do not count.
    /// </summary>
    private static IEnumerable<string> PackageParts(string path, Comparison comparison)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        var parts = new List<string>();
        foreach (ZipArchiveEntry entry in archive.Entries.OrderBy(static entry => entry.FullName, StringComparer.Ordinal))
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            string content = reader.ReadToEnd();
            if (entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
            {
                content = string.Concat(Relationship().Matches(content).Select(static match => match.Value).Order(StringComparer.Ordinal));
            }
            content = SavedTime().Replace(content, string.Empty);
            if (comparison == Comparison.Accumulation)
            {
                content = AttributeValue().Replace(content, static match => Digits().Replace(match.Value, "#"));
            }
            parts.Add(entry.FullName + "\n" + content);
        }
        return parts;
    }

    /// <summary>A file's bytes without the document ids and dates a save writes.</summary>
    private static byte[] MaskedBytes(string path)
    {
        string text = Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(path));
        return Encoding.Latin1.GetBytes(SavedIdentity().Replace(text, string.Empty));
    }

    /// <summary>The hashes of every page the product renders to PNG.</summary>
    private static IEnumerable<string> Rendered(RuleWorkspace workspace, string product, string file, string[] open)
    {
        CliCommand render = CliCatalog.Current.Command($"{product} render");
        string output = "rendered-" + Path.GetFileNameWithoutExtension(file) + ".png";
        List<string> args = [.. render.Tokens, file, .. open, "--to", "png", "--out", output, "--overwrite"];
        args.AddRange(render.Options.Select(static option => option.Name).Where(static name => name.StartsWith("--all-", StringComparison.Ordinal)));
        RuleRun run = workspace.Json([.. args]);
        if (run.ExitCode != 0)
        {
            return ["error " + run.Error];
        }
        return Outputs(run.Document).Select(static path => FileHashes.Sha256(path));
    }

    /// <summary>Every file a result lists in <c>output.path</c>, <c>outputs[].path</c> or <c>outputs[].output.path</c>.</summary>
    public static IEnumerable<string> Outputs(JsonNode? document)
    {
        if (document?["output"]?["path"]?.GetValue<string>() is { } path)
        {
            yield return path;
        }
        foreach (JsonNode? output in document?["outputs"] as JsonArray ?? [])
        {
            if ((output?["path"] ?? output?["output"]?["path"])?.GetValue<string>() is { } item)
            {
                yield return item;
            }
        }
    }

    // ----- JSON helpers ------------------------------------------------------------------------

    /// <summary>Every property below a node in document order, with its path.</summary>
    public static IEnumerable<(string Path, string Name, JsonNode? Value)> Walk(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string key, JsonNode? value) in obj)
                {
                    string at = Join(path, key);
                    yield return (at, key, value);
                    foreach (var inner in Walk(value, at))
                    {
                        yield return inner;
                    }
                }
                break;
            case JsonArray array:
                for (int index = 0; index < array.Count; index++)
                {
                    foreach (var inner in Walk(array[index], $"{path}[{index}]"))
                    {
                        yield return inner;
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Whether a read value holds a written one: an object holds every written property (a read
    /// may say more), an array holds as many items, each holding its written item, and a scalar
    /// is equal, numbers by value.
    /// </summary>
    public static bool Holds(JsonNode? written, JsonNode? read) => written switch
    {
        JsonObject obj => read is JsonObject other && obj.All(pair => other.ContainsKey(pair.Key) && Holds(pair.Value, other[pair.Key])),
        JsonArray array => read is JsonArray items && items.Count == array.Count && array.Select((item, index) => Holds(item, items[index])).All(static held => held),
        JsonValue value when value.GetValueKind() == JsonValueKind.Number =>
            read is JsonValue number && number.GetValueKind() == JsonValueKind.Number && value.GetValue<double>() == number.GetValue<double>(),
        _ => JsonNode.DeepEquals(written, read),
    };

    public static string Show(JsonNode? value) => ScenarioContract.Excerpt(value?.ToJsonString() ?? "null");

    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    [GeneratedRegex("<Relationship [^>]*/>")]
    private static partial Regex Relationship();

    [GeneratedRegex("<dcterms:modified[^>]*>[^<]*</dcterms:modified>")]
    private static partial Regex SavedTime();

    [GeneratedRegex("=\"[^\"]*\"")]
    private static partial Regex AttributeValue();

    [GeneratedRegex("[0-9]+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"/ID\s*\[[^\]]*\]|/(?:CreationDate|ModDate)\s*\([^)]*\)|<xmp:[A-Za-z]+Date>[^<]*</xmp:[A-Za-z]+Date>|<xmpMM:[A-Za-z]+ID>[^<]*</xmpMM:[A-Za-z]+ID>")]
    private static partial Regex SavedIdentity();

    [GeneratedRegex(@"/aspose-cli/(?<id>v\d+/.+)\.schema\.json$")]
    private static partial Regex SchemaId();

    [GeneratedRegex(@"^\d{4}-\d\d-\d\dT\d\d:\d\d")]
    private static partial Regex IsoDateTime();
}
