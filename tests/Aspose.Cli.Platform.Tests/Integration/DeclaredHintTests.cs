using System.CommandLine;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>
/// The hints a command declares by hand, for an option it does not have (a command definition's
/// <c>RefusedOptions</c>) and for a name it is mistaken for (<c>mistakenFor</c>), appear only when
/// a caller makes that mistake, and no capability lists them. Each mistake is made once here, on
/// its product's own document, and what its hint names must run as written.
/// </summary>
public sealed class DeclaredHintTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public DeclaredHintTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EveryDeclaredHint_IsShownForItsMistake_AndRunsAsWritten()
    {
        Command root = ActualCommandTree.Parser.Parse([]).ParseResult.RootCommandResult.Command;
        Mistake[] mistakes = [.. Mistakes(root, [])];

        // A walk that finds no declaration would pass for every hint; the build declares both kinds.
        Assert.Contains(mistakes, static mistake => mistake.Kind == "refused option");
        Assert.Contains(mistakes, static mistake => mistake.Kind == "mistaken name");

        // The workspace's own run throws at the first hint that does not run; this one collects every one.
        string configRoot = Path.GetDirectoryName(_workspace.ConfigDirectory)!;
        var cli = new CliProcess(CliRunner.ExecutablePath, CliEnvironment.Evaluation(configRoot, null));
        var problems = new List<string>();
        foreach (Mistake mistake in mistakes)
        {
            string[] args = [.. mistake.Args, "--output-format", "json"];
            CliResult result = cli.Run(_workspace.Path, args);
            string line = string.Join(' ', mistake.Args);
            string? shown = JsonNode.Parse(result.StdErr)?["error"]?["hint"]?.GetValue<string>();
            if (mistake.Hint is not null && shown?.Contains(mistake.Hint, StringComparison.Ordinal) != true)
            {
                problems.Add($"{mistake.Kind} '{line}': the error's hint '{shown}' does not give the declared '{mistake.Hint}'.");
            }

            HintRun run = new(args, _workspace.Path, configRoot, Variables: null);
            problems.AddRange(ExecutableHints.Problems(run, result).Select(problem => $"{mistake.Kind} '{line}': {problem}"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Every mistake a command below <paramref name="parent"/> declares: each name of an
    /// option it refuses, given to it, and each name it is mistaken for, written in its place.
    /// Each runs on its product's own document when the product has one.
    /// </summary>
    private IEnumerable<Mistake> Mistakes(Command parent, IReadOnlyList<string> path)
    {
        foreach (Command command in parent.Subcommands)
        {
            string[] own = [.. path, command.Name];
            string[] input = Input(own[0]);
            foreach (RefusedOption refused in command.RefusedOptions())
            {
                foreach (string name in refused.Names)
                {
                    yield return new Mistake("refused option", [.. own, .. input, name], refused.Hint);
                }
            }

            foreach (string name in command.MistakenNames())
            {
                yield return new Mistake("mistaken name", [.. path, name, .. input], command.MistakenNameHint());
            }

            foreach (Mistake mistake in Mistakes(command, own))
            {
                yield return mistake;
            }
        }
    }

    /// <summary>The product's own document, written into the workspace once; none for a command of no product.</summary>
    private string[] Input(string product)
    {
        if (!ScenarioFixtures.Products.Contains(product, StringComparer.Ordinal))
        {
            return [];
        }

        string file = "input." + ScenarioFixtures.PrimaryFormat(product);
        if (!File.Exists(_workspace.File(file)))
        {
            File.WriteAllBytes(_workspace.File(file), ScenarioFixtures.Read($"{product}.{ScenarioFixtures.PrimaryFormat(product)}"));
        }
        return [file];
    }

    /// <summary>A mistake a command declares, the command line that makes it and its declared hint; null for the advice to read help.</summary>
    private sealed record Mistake(string Kind, IReadOnlyList<string> Args, string? Hint);
}
