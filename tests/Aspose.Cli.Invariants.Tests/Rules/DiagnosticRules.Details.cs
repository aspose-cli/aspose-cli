using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// C-R25: the error envelope and the dictionary of its details keys.
internal static partial class ContractRules
{
    private const string ErrorSchema = "v2/common/error";

    /// <summary>
    /// The dictionary of <c>error.details</c> keys, one key per concept, each with the description
    /// the error schema publishes for it. It was drawn from the keys the SDK, Host and products
    /// write today, less those the rule replaces: budgets state <c>limit</c>, <c>actual</c> and
    /// <c>unit</c> (not <c>observed</c>, <c>sizeBytes</c>, <c>limitBytes</c>, <c>requestedCells</c>,
    /// <c>maxCells</c>, <c>maxPixels</c>, <c>requestedSheet</c>, <c>firstSheet</c>); collections state
    /// <c>available</c> (not <c>supported</c>); operation errors state <c>id</c> and <c>op</c> (not the
    /// batch position <c>index</c>); a positional argument is <c>argument</c>, not <c>option</c>.
    /// </summary>
    private static readonly Dictionary<string, string> DetailsKeys = new(StringComparer.Ordinal)
    {
        ["actual"] = "The measured amount of a budget, in unit.",
        ["actualExists"] = "Whether the output existed when publication found it changed.",
        ["actualSha256"] = "The SHA-256 of the output publication found.",
        ["actualSizeBytes"] = "The size of the output publication found.",
        ["allowedFields"] = "The fields the operation accepts.",
        ["argument"] = "The positional argument the error is about.",
        ["available"] = "The names or values that exist or are accepted, at most 50.",
        ["availableCount"] = "How many names or values exist or are accepted.",
        ["cause"] = "Why an operation field was refused.",
        ["declared"] = "The product the command or extension declares.",
        ["detected"] = "The products whose formats the content matches.",
        ["diagnosticId"] = "The host diagnostic id of a startup or process failure.",
        ["directory"] = "The directory the error is about.",
        ["dpi"] = "The resolution of a render.",
        ["engineMessage"] = "The document engine's own message.",
        ["errors"] = "Every problem, when there are several.",
        ["expectedBytes"] = "The size an input had when it was admitted.",
        ["expectedExists"] = "Whether the output existed when the command read it.",
        ["expectedSha256"] = "The SHA-256 the file had when the command read it.",
        ["expectedSizeBytes"] = "The size the output had when the command read it.",
        ["extension"] = "The file extension the error is about.",
        ["extensions"] = "The file extensions the command accepts.",
        ["feature"] = "The feature the product or format does not support.",
        ["feed"] = "The release feed the update read.",
        ["field"] = "The operation field the error is about.",
        ["format"] = "The format id the error is about.",
        ["height"] = "The height of a render, in pixels.",
        ["id"] = "The id of the failing operation.",
        ["input"] = "The input the error is about.",
        ["limit"] = "The budget's limit, in unit.",
        ["line"] = "The line of a text input the error is about.",
        ["observedBytes"] = "The size an input had when it was read again.",
        ["observedSha256"] = "The SHA-256 an input had when it was read again.",
        ["offset"] = "The byte offset of the problem in an input.",
        ["op"] = "The op name of the failing operation.",
        ["operation"] = "The product feature the error is about.",
        ["option"] = "The option the error is about, such as --pages.",
        ["originalError"] = "The error that stopped a publication: its code and details.",
        ["path"] = "The absolute path of the file the error is about.",
        ["phase"] = "The stage of processing that failed.",
        ["pid"] = "The id of the process the error is about.",
        ["port"] = "The loopback port the error is about.",
        ["problems"] = "The problems the engine reported.",
        ["product"] = "The product the error is about.",
        ["profile"] = "The conformance profile the error is about.",
        ["quiescenceConfirmed"] = "Whether the stopped worker was confirmed to have ended.",
        ["range"] = "The range or selection as the caller wrote it.",
        ["reason"] = "Why the value or file was refused.",
        ["recoveryComplete"] = "Whether every publication target was restored.",
        ["requested"] = "The name, number or value as the caller wrote it.",
        ["resource"] = "The budget's resource kind.",
        ["sample"] = "An excerpt of the input the error is about.",
        ["sheet"] = "The worksheet the error is about.",
        ["source"] = "The source of a value, such as a variable or file.",
        ["stage"] = "The stage of the worker that failed.",
        ["subject"] = "What was looked up, such as sheet or page.",
        ["suggestions"] = "The accepted names closest to the request, best first.",
        ["target"] = "The target the error is about.",
        ["targets"] = "The publication targets and their state.",
        ["timeoutSeconds"] = "The timeout that elapsed, in seconds.",
        ["unit"] = "The unit of a budget's limit and actual.",
        ["variable"] = "The environment variable the error is about.",
        ["width"] = "The width of a render, in pixels.",
    };

    private static IEnumerable<InvariantCase> R25(CliCatalog catalog)
    {
        yield return Check("C-R25", "error envelope shape", () =>
        [
            .. EnvelopeShapeProblems(catalog, ErrorSchema, definition: null, "the error envelope",
                required: ["schema", "schemaVersion", "error"], properties: ["schema", "schemaVersion", "error"]),
            .. EnvelopeShapeProblems(catalog, ErrorSchema, definition: "errorPayload", "error",
                required: ["code", "message"], properties: ["code", "message", "details", "hint", "docs"]),
        ]);
        yield return Check("C-R25", "error schema publishes the details dictionary", () => PublishedDictionaryProblems(catalog));
        foreach (DetailsSample sample in DetailsSamples(catalog))
        {
            yield return Check("C-R25", $"{sample.Subject} error details", () => DetailsProblems(sample), sample.Shard);
        }
    }

    /// <summary>The differences between the details the error schema describes and the dictionary.</summary>
    private static IEnumerable<string> PublishedDictionaryProblems(CliCatalog catalog)
    {
        JsonNode? details = JsonNode.Parse(catalog.SchemaText(ErrorSchema))?["$defs"]?["errorPayload"]?["properties"]?["details"];
        if (details?["properties"] is not JsonObject published)
        {
            yield return $"error.details publishes no key dictionary; the {DetailsKeys.Count} keys of the dictionary have no description in the error schema";
            yield break;
        }
        foreach (string key in DetailsKeys.Keys.Where(key => !published.ContainsKey(key)).Order(StringComparer.Ordinal))
        {
            yield return $"details key '{key}' is not published";
        }
        foreach ((string key, JsonNode? property) in published.OrderBy(static property => property.Key, StringComparer.Ordinal))
        {
            if (!DetailsKeys.ContainsKey(key))
            {
                yield return $"published details key '{key}' is not in the dictionary";
            }
            else if (property?["description"]?.GetValue<string>() is not { Length: > 0 })
            {
                yield return $"details key '{key}' has no description";
            }
        }
        if (details["additionalProperties"]?.GetValue<bool>() != false)
        {
            yield return "error.details allows keys outside the dictionary";
        }
    }

    /// <summary>
    /// A failing invocation whose error details a C-R25 case checks, after the commands of
    /// <see cref="Setup"/> prepare its files in the same workspace. An <see cref="EvaluationOnly"/>
    /// sample fails only without a license, so a licensed run has nothing to check.
    /// </summary>
    private sealed record DetailsSample(string Subject, string Shard, Invocation Invocation)
    {
        public IReadOnlyList<string[]> Setup { get; init; } = [];

        public bool EvaluationOnly { get; init; }
    }

    /// <summary>
    /// One failing invocation of each kind a caller meets, per product with a fixture and for the
    /// platform: a missing or corrupt input, an unknown option or command, a missing argument, an
    /// invalid option value, an unknown op or field and several invalid operations, a missing target,
    /// an output or created file extension no format has, an input, read range or raster over its
    /// budget, an evaluation export of a later worksheet and an output in another product's format.
    /// </summary>
    private static IEnumerable<DetailsSample> DetailsSamples(CliCatalog catalog)
    {
        yield return new DetailsSample("platform unknown command", InvariantCases.Platform, new Invocation(["no-such-command"]));
        yield return new DetailsSample("platform unknown schema id", InvariantCases.Platform, new Invocation(["schema", "no-such-schema"]));
        yield return new DetailsSample("platform unknown docs topic", InvariantCases.Platform, new Invocation(["docs", "no-such-topic"]));
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            string id = product.Id;
            string extension = ScenarioFixtures.PrimaryFormat(id);
            CliCommand inspect = catalog.Command($"{id} inspect");
            string[] globals = [.. catalog.Command(string.Empty).Options.Select(static option => option.Name)];

            Invocation missing = Invocation.For(inspect);
            missing.Files.Remove(missing.Input!);
            missing.Replace(missing.Input!, "missing." + extension);
            yield return new DetailsSample($"{id} missing input", id, missing);

            yield return new DetailsSample($"{id} input over budget", id, Invocation.For(inspect).Add("--max-input-bytes", "10"));

            Invocation corrupt = Invocation.For(inspect);
            corrupt.Files[corrupt.Input!] = new ScenarioFile { Text = "This is not a document.\n" };
            yield return new DetailsSample($"{id} corrupt input", id, corrupt);

            yield return new DetailsSample($"{id} unknown option", id, Invocation.For(inspect).Add("--no-such-option"));
            yield return new DetailsSample($"{id} unknown command", id, new Invocation([id, "no-such-command"]));
            yield return new DetailsSample($"{id} missing argument", id, new Invocation(inspect.Tokens));

            if (catalog.Commands.Where(command => command.Product == id && !command.Hidden)
                    .SelectMany(static command => command.Options.Select(option => (command, option)))
                    .FirstOrDefault(pair => pair.option.AllowedValues.Count > 0 && pair.option.Type != "boolean" && !globals.Contains(pair.option.Name))
                is { command: { } valued, option: { } option })
            {
                yield return new DetailsSample($"{id} invalid option value", id, Invocation.For(valued, option.Name).Add(option.Name, "no-such-value"));
            }

            if (product.Ops.Count > 0)
            {
                yield return new DetailsSample($"{id} unknown op", id, EditWith(id, extension, new JsonObject { ["op"] = "no_such_op" }));
                yield return new DetailsSample($"{id} unknown field", id, EditWith(id, extension, new JsonObject { ["op"] = product.Ops[0], ["noSuchField"] = "x" }));
                yield return new DetailsSample($"{id} several invalid operations", id, EditWith(id, extension,
                    new JsonObject { ["op"] = product.Ops[0], ["noSuchField"] = "x" },
                    new JsonObject { ["op"] = product.Ops[^1], ["otherMissingField"] = "x" }));
            }

            CliCommand render = catalog.Command($"{id} render");
            if (render.Options.FirstOrDefault(static option => option.Name is "--pages" or "--slides" or "--sheet") is { } selection)
            {
                string value = selection.Name == "--sheet" ? "NoSuchSheet" : "99";
                yield return new DetailsSample($"{id} missing target", id, Invocation.For(render, selection.Name).Add(selection.Name, value));
            }

            yield return new DetailsSample($"{id} unknown output extension", id, Invocation.For(render).Add("--out", "out.no-such-extension"));

            CliCommand create = catalog.Command($"{id} create");
            Invocation created = Invocation.For(create);
            if (created.Args.Contains("created." + extension))
            {
                created.Replace("created." + extension, "created.no-such-extension");
                yield return new DetailsSample($"{id} unknown created extension", id, created);
            }

            // A raster over the pixel budget: the widest render of a 4:3 document.
            if (render.Has("--width") && create.Option("--size") is { } size && size.AllowedValues.Contains("4x3"))
            {
                yield return new DetailsSample($"{id} raster over budget", id, new Invocation([id, "render", "wide." + extension, "--width", "20000"]))
                {
                    Setup = [[id, "create", "wide." + extension, "--size", "4x3"]],
                };
            }

            // Evaluation mode exports only the first worksheet to a text format.
            if (product.Ops.Contains("add_sheet") && catalog.Command($"{id} convert").Has("--sheet"))
            {
                Invocation edit = EditWith(id, extension, new JsonObject { ["op"] = "add_sheet", ["name"] = "Second" });
                var export = new Invocation([id, "convert", edit.Input!, "--to", "csv", "--sheet", "Second"]);
                foreach ((string file, ScenarioFile seed) in edit.Files)
                {
                    export.Files[file] = seed;
                }
                yield return new DetailsSample($"{id} evaluation export of a later sheet", id, export)
                {
                    Setup = [[.. edit.Args.Where(static arg => arg != "--dry-run"), "--in-place"]],
                    EvaluationOnly = true,
                };
            }

            if (catalog.Commands.FirstOrDefault(command => command.Product == id && !command.Hidden && command.Has("--range")) is { } ranged)
            {
                yield return new DetailsSample($"{id} range over budget", id, Invocation.For(ranged, "--range").Add("--range", "A1:XFD1048576"));
            }

            CliCommand convert = catalog.Command($"{id} convert");
            string[] writable = [.. product.LoadFormats, .. product.ConvertFormats, .. product.RenderFormats];
            if (catalog.Products.Where(other => other.Id != id)
                    .SelectMany(static other => other.LoadFormats)
                    .FirstOrDefault(format => !writable.Contains(format, StringComparer.OrdinalIgnoreCase)) is { } foreign
                && convert.Option("--to") is { } to)
            {
                string target = to.AllowedValues.Contains(extension) ? extension : to.AllowedValues[0];
                yield return new DetailsSample($"{id} unwritable output", id,
                    Invocation.For(convert, "--to").Add("--to", target, "--out", "out." + foreign));
            }
        }
    }

    private static Invocation EditWith(string product, string extension, params JsonObject[] operations)
    {
        string input = "input." + extension;
        var invocation = new Invocation([product, "edit", input, "--ops", "ops.json", "--dry-run"]);
        invocation.Files[input] = new ScenarioFile { Fixture = $"{product}.{extension}" };
        invocation.Inputs.Add(input);
        invocation.Files["ops.json"] = new ScenarioFile { Text = new JsonObject { ["ops"] = new JsonArray([.. operations]) }.ToJsonString() };
        return invocation;
    }

    /// <summary>
    /// Runs a failing sample and checks its error details against C-R25: every key, and every key
    /// of each <c>errors</c> item, is in the dictionary, no value is null, <c>errors</c> lists
    /// several problems, <c>path</c> is absolute and <c>option</c> names an option. A sample that no
    /// longer fails, or fails without an error envelope, is reported, so it cannot pass unchecked.
    /// </summary>
    private static IEnumerable<string> DetailsProblems(DetailsSample sample)
    {
        if (sample.EvaluationOnly && ScenarioLicense.Licensed)
        {
            return [];
        }
        (CliResult result, string? setupFailure) = RunSample(sample);
        if (setupFailure is not null)
        {
            return [$"the sample's setup failed: {setupFailure}"];
        }
        if (result.ExitCode == 0)
        {
            return ["the sample no longer fails (exit 0); choose another invocation of this kind"];
        }
        JsonObject? error = null;
        try
        {
            error = JsonNode.Parse(result.StdErr)?["error"] as JsonObject;
        }
        catch (JsonException)
        {
        }
        if (error is null)
        {
            return [$"the sample exited {result.ExitCode} without an error envelope on stderr"];
        }
        if (error["details"] is not JsonObject details)
        {
            return [];
        }
        string code = error["code"]?.GetValue<string>() ?? "?";
        var problems = new List<string>();
        problems.AddRange(details.Select(static member => member.Key).Where(static key => !DetailsKeys.ContainsKey(key))
            .Select(key => $"{code} details.{key} is not in the details dictionary"));
        if (details["errors"] is JsonArray errors)
        {
            problems.AddRange(errors.OfType<JsonObject>().SelectMany(static item => item.Select(static member => member.Key))
                .Distinct(StringComparer.Ordinal)
                .Where(static key => !DetailsKeys.ContainsKey(key))
                .Select(key => $"{code} details.errors[].{key} is not in the details dictionary"));
            if (errors.Count == 1)
            {
                problems.Add($"{code} details.errors lists a single error");
            }
        }
        problems.AddRange(NullMembers(details, "details").Select(path => $"{code} {path} is null"));
        if (details["path"] is JsonValue path && path.TryGetValue(out string? text) && !Path.IsPathFullyQualified(text))
        {
            problems.Add($"{code} details.path '{text}' is not absolute");
        }
        if (details["option"] is JsonValue option && option.TryGetValue(out string? name) && !name.StartsWith('-'))
        {
            problems.Add($"{code} details.option '{name}' names an argument; details.argument names one");
        }
        return problems;
    }

    private static IEnumerable<string> NullMembers(JsonNode? node, string path) => node switch
    {
        null => [path],
        JsonObject members => members.SelectMany(member => NullMembers(member.Value, $"{path}.{member.Key}")),
        JsonArray items => items.SelectMany((item, index) => NullMembers(item, $"{path}[{index}]")),
        _ => [],
    };

    /// <summary>Seeds a workspace, runs the sample's setup and then the sample itself.</summary>
    private static (CliResult Result, string? SetupFailure) RunSample(DetailsSample sample)
    {
        using var workspace = new RuleWorkspace(sample.Invocation);
        foreach (string[] setup in sample.Setup)
        {
            CliResult prepared = workspace.Execute([.. setup, "--output-format", "json"]);
            if (prepared.ExitCode != 0)
            {
                return (prepared, $"'{string.Join(' ', setup)}' exited {prepared.ExitCode}");
            }
        }
        return (workspace.Run("--output-format", "json"), null);
    }
}
