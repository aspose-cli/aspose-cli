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
    /// The keys C-R25 replaces, which the dictionary the error schema publishes must not hold:
    /// budgets state <c>limit</c>, <c>actual</c> and <c>unit</c> (not <c>observed</c>,
    /// <c>sizeBytes</c>, <c>limitBytes</c>, <c>requestedCells</c>, <c>maxCells</c>, <c>maxPixels</c>,
    /// <c>requestedSheet</c>, <c>firstSheet</c>); collections state <c>available</c> (not
    /// <c>supported</c>); operation errors state <c>id</c> and <c>op</c> (not the batch position
    /// <c>index</c>).
    /// </summary>
    private static readonly string[] ReplacedDetailsKeys =
        ["observed", "sizeBytes", "limitBytes", "requestedCells", "maxCells", "maxPixels", "requestedSheet", "firstSheet", "supported", "index"];

    /// <summary>
    /// The schema of the error envelope's <c>error</c> object, which a failed operation of a
    /// best-effort batch carries too.
    /// </summary>
    private static (string SchemaId, JsonObject Node) ErrorPayload() =>
        PublishedSchema.Resolve(ErrorSchema, PublishedSchema.Document(ErrorSchema)["properties"]!["error"]!.AsObject());

    /// <summary>The <c>error.details</c> schema, whose properties are the dictionary of details keys.</summary>
    private static JsonObject? PublishedDetails() => ErrorPayload().Node["properties"]?["details"] as JsonObject;

    /// <summary>The dictionary of details keys, as the error schema publishes it; empty when it publishes none.</summary>
    private static readonly Lazy<HashSet<string>> DetailsKeys = new(() =>
        PublishedDetails()?["properties"] is JsonObject keys
            ? [.. keys.Select(static key => key.Key)]
            : []);

    private static IEnumerable<InvariantCase> R25(CliCatalog catalog)
    {
        yield return Check("C-R25", "error envelope shape", () =>
        [
            .. EnvelopeShapeProblems(catalog, ErrorSchema, definition: null, "the error envelope",
                required: ["schema", "schemaVersion", "error"], properties: ["schema", "schemaVersion", "error"]),
            .. EnvelopeShapeProblems(catalog, ErrorPayload().SchemaId, definition: null, "error",
                required: ["code", "message"], properties: ["code", "message", "details", "hint", "docs"]),
        ]);
        yield return Check("C-R25", "error schema publishes the details dictionary", () => PublishedDictionaryProblems(catalog));
        foreach (DetailsSample sample in DetailsSamples(catalog))
        {
            yield return Check("C-R25", $"{sample.Subject} error details", () => DetailsProblems(sample), sample.Shard);
        }
    }

    /// <summary>
    /// The problems of the dictionary the error schema publishes: it lists keys, each with a
    /// description, closes <c>error.details</c> to them and holds none of the replaced keys.
    /// </summary>
    private static IEnumerable<string> PublishedDictionaryProblems(CliCatalog catalog)
    {
        _ = catalog;
        JsonObject? details = PublishedDetails();
        if (details?["properties"] is not JsonObject published || published.Count == 0)
        {
            yield return "error.details publishes no key dictionary";
            yield break;
        }
        foreach ((string key, JsonNode? property) in published.OrderBy(static property => property.Key, StringComparer.Ordinal))
        {
            if (property?["description"]?.GetValue<string>() is not { Length: > 0 })
            {
                yield return $"details key '{key}' has no description";
            }
        }
        foreach (string key in ReplacedDetailsKeys.Where(published.ContainsKey))
        {
            yield return $"details key '{key}' is one the rule replaces";
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
    /// an output or created file extension no format has, an input or raster over its
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
    /// Runs a failing sample and checks its error details against C-R25: the envelope matches the
    /// error schema, every key, and every key of each <c>errors</c> item, is in the dictionary, no value is null, <c>errors</c> lists
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
        JsonNode? envelope = null;
        try
        {
            envelope = JsonNode.Parse(result.StdErr);
        }
        catch (JsonException)
        {
        }
        if (envelope?["error"] is not JsonObject error)
        {
            return [$"the sample exited {result.ExitCode} without an error envelope on stderr"];
        }
        string code = error["code"]?.GetValue<string>() ?? "?";
        var problems = new List<string>();
        if (ScenarioContract.Validate(ErrorSchema, envelope) is { } invalid)
        {
            problems.Add($"{code}: the error envelope does not match {ErrorSchema}: {invalid}");
        }
        if (error["details"] is not JsonObject details)
        {
            return problems;
        }
        problems.AddRange(details.Select(static member => member.Key).Where(static key => !DetailsKeys.Value.Contains(key))
            .Select(key => $"{code} details.{key} is not in the details dictionary"));
        if (details["errors"] is JsonArray errors)
        {
            problems.AddRange(errors.OfType<JsonObject>().SelectMany(static item => item.Select(static member => member.Key))
                .Distinct(StringComparer.Ordinal)
                .Where(static key => !DetailsKeys.Value.Contains(key))
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
