using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;

namespace Aspose.Cli.Invariants.Tests;

// C-R31 by running a sample: the file objects a result reports carry absolute paths.
internal static partial class ContractRules
{
    /// <summary>The verbs sampled per product: a read, a write of one file and a write of several.</summary>
    private static readonly string[] PathSampleVerbs = ["inspect", "edit", "render"];

    /// <summary>The members that hold a file object, or a list of them, in a result.</summary>
    private static readonly HashSet<string> FileMembers = new(StringComparer.Ordinal) { "input", "source", "left", "right", "output", "outputs" };

    private static IEnumerable<InvariantCase> FilePathSamples(CliCatalog catalog)
    {
        foreach (CliProduct product in catalog.Products.Where(static product => ScenarioFixtures.Products.Contains(product.Id)))
        {
            foreach (string verb in PathSampleVerbs)
            {
                CliCommand? command = catalog.Commands.FirstOrDefault(item => item.Words == $"{product.Id} {verb}");
                if (command is not null)
                {
                    Invocation invocation = Invocation.For(command);
                    yield return Check("C-R31", $"{command} paths", () => RelativePaths(invocation), shard: InvariantCases.ShardOf(command));
                }
            }
        }
    }

    /// <summary>
    /// The run of each value of the option that chooses an outputs list's unit
    /// (<see cref="UnitsARunChooses"/>): every item of its <c>outputs</c> names exactly one unit
    /// beside the file fields and <c>encrypted</c>, the same in every item, as the schema check
    /// cannot tell. Every value must write some, or that value was not judged.
    /// </summary>
    private static IEnumerable<InvariantCase> ChosenUnits(CliCatalog catalog)
    {
        foreach (UnitChoice choice in UnitsARunChooses.Values)
        {
            if (catalog.Commands.FirstOrDefault(item => item.Words == choice.Command) is not { } command
                || command.Option(choice.Option) is not { AllowedValues.Count: > 0 } option)
            {
                continue;
            }

            yield return Check("C-R31", $"{command} units", () =>
            {
                var problems = new List<string>();
                foreach (string value in option.AllowedValues)
                {
                    Invocation invocation = Invocation.For(command, omit: choice.Option).Add(choice.Option, value);
                    if (choice.Fixture is { } fixture)
                    {
                        foreach (string input in invocation.Inputs)
                        {
                            invocation.Files[input] = new ScenarioFile { Fixture = fixture };
                        }
                    }
                    if (RunSample(invocation, out string? failure) is not { } document)
                    {
                        problems.Add($"{choice.Option} {value}: {failure}");
                        continue;
                    }
                    string[][] units =
                    [
                        .. (document["outputs"] as JsonArray ?? []).Select(static item =>
                            item is JsonObject output ? output.Select(static pair => pair.Key).Where(static name => !FileCore.Contains(name) && name != "encrypted").ToArray() : []),
                    ];
                    string[] distinct = [.. units.SelectMany(static names => names).Distinct(StringComparer.Ordinal)];
                    if (units.Length == 0)
                    {
                        problems.Add($"{choice.Option} {value}: no outputs");
                    }
                    else if (units.Any(static names => names.Length != 1) || distinct.Length != 1)
                    {
                        problems.Add($"{choice.Option} {value}: outputs[] name {string.Join(", ", distinct)}, not one unit the same in every item");
                    }
                }
                return problems;
            }, shard: InvariantCases.ShardOf(command));
        }
    }

    /// <summary>Runs one invocation in a fresh workspace under the run's license mode and returns its result, or null with why.</summary>
    private static JsonObject? RunSample(Invocation invocation, out string? failure)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        foreach ((string relative, ScenarioFile file) in invocation.Files)
        {
            string target = workspace.File(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, ScenarioFixtures.Read(file.Fixture!));
        }
        Dictionary<string, string?> variables = invocation.Env.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value);
        var environment = CliEnvironment.Evaluation(Path.GetDirectoryName(workspace.ConfigDirectory)!, variables);
        CliResult result = new CliProcess(CliRunner.ExecutablePath, environment)
            .Run(workspace.Path, string.Empty, [.. invocation.Args, "--output-format", "json"]);
        if (result.ExitCode != 0 || JsonNode.Parse(result.StdOut) is not JsonObject document)
        {
            failure = $"the sample exited {result.ExitCode} without a result";
            return null;
        }
        failure = null;
        return document;
    }

    /// <summary>Runs one invocation and reports every file path of its result that is not absolute.</summary>
    private static IEnumerable<string> RelativePaths(Invocation invocation)
    {
        if (RunSample(invocation, out string? failure) is not { } document)
        {
            return [failure!];
        }
        var problems = new List<string>();
        Visit(document, string.Empty, member: null);
        return problems;

        void Visit(JsonNode? node, string path, string? member)
        {
            switch (node)
            {
                case JsonObject item:
                    if (member is not null && item["path"] is JsonValue value && value.TryGetValue(out string? text) && !Path.IsPathFullyQualified(text))
                    {
                        problems.Add($"{path}.path is not absolute: {text}");
                    }
                    foreach ((string name, JsonNode? child) in item)
                    {
                        Visit(child, path.Length == 0 ? name : $"{path}.{name}", FileMembers.Contains(name) ? name : null);
                    }
                    break;
                case JsonArray array:
                    for (int index = 0; index < array.Count; index++)
                    {
                        Visit(array[index], $"{path}[{index}]", member);
                    }
                    break;
            }
        }
    }
}
