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

    /// <summary>Runs one invocation in a fresh workspace under the run's license mode and reports every file path of its result that is not absolute.</summary>
    private static IEnumerable<string> RelativePaths(Invocation invocation)
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
            .Run(workspace.Path, string.Empty, [.. invocation.Args, "--output", "json"]);
        if (result.ExitCode != 0 || JsonNode.Parse(result.StdOut) is not JsonObject document)
        {
            return [$"the sample exited {result.ExitCode} without a result"];
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
