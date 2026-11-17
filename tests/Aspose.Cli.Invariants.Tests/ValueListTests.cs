using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

/// <summary>
/// An option that lists its values takes only one of them, whatever the parser made of the
/// token: a switch written where the value belongs, which the parser hands the option as its
/// value, is refused as any value outside the list is.
/// </summary>
public sealed class ValueListTests
{
    [Fact]
    [Category(TestCategory.Slow)]
    public void EveryOptionWithAList_RefusesASwitchGivenAsItsValue()
    {
        CliCommand root = CliCatalog.Current.Commands.Single(static command => command.Words.Length == 0);
        string flag = root.Options.First(static option => option.Type == "boolean" && option.Recursive && !option.Hidden).Name;
        CliCommand[] commands = [.. CliCatalog.Current.Commands.Where(static command => command.Words.Length > 0 && command.Subcommands.Count == 0)];
        (CliCommand Command, CliOption Option)[] listed =
        [
            .. commands.SelectMany(static command => command.Options.Select(option => (command, option)))
                .Concat(root.Options.Select(option => (commands[0], option)))
                .Where(static pair => pair.option.AllowedValues.Count > 0 && pair.option.Type != "boolean" && !pair.option.Hidden),
        ];
        var failures = new List<string>();
        foreach ((CliCommand command, CliOption option) in listed)
        {
            using var workspace = new TempWorkspace();
            CliResult result = workspace.Run([.. Invocation.For(command, omit: option.Name).Args, option.Name, flag]);
            string? code = JsonNode.Parse(result.StdErr.Length > 0 ? result.StdErr : "{}")?["error"]?["code"]?.GetValue<string>();
            if (result.ExitCode != 2 || code != "OPTION_INVALID")
            {
                failures.Add($"{command} {option.Name} {flag}: exit {result.ExitCode}, {code ?? "no error"}");
            }
        }

        Assert.NotEmpty(listed);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
