using System.CommandLine;
using System.CommandLine.Parsing;
using Aspose.Cli.Host.Tests;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Extensibility;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Architecture;

public sealed class CommandHelpExampleTests
{
    [Fact]
    public void EveryHelpExampleAndLink_ParsesAgainstTheCommandTree()
    {
        Command root = ActualCommandTree.Parser.Parse([]).ParseResult.RootCommandResult.Command;
        string executable = DistributionInfo.CommandName + " ";
        string[] lines = Descendants(root)
            .SelectMany(static command => command.TryGetHelpMetadata(out CommandHelpMetadata? metadata)
                ? metadata!.Examples.Concat(metadata.LearnMore.Select(static link => link.Command))
                : [])
            .ToArray();

        Assert.Contains(lines, static line => line.Contains(" slides ", StringComparison.Ordinal));
        Assert.All(lines, line =>
        {
            Assert.StartsWith(executable, line, StringComparison.Ordinal);
            ParseResult parse = root.Parse([.. CommandLineParser.SplitCommandLine(line[executable.Length..])]);
            Assert.True(parse.Errors.Count == 0, $"{line}: {string.Join("; ", parse.Errors.Select(static error => error.Message))}");
        });
    }

    [Fact]
    public void WithExamples_WritesEachExampleAfterTheExecutableName()
    {
        var command = new Command("run").WithExamples(["run --fast", $"{DistributionInfo.CommandName} run"]);

        Assert.True(command.TryGetHelpMetadata(out CommandHelpMetadata? metadata));
        Assert.Equal([$"{DistributionInfo.CommandName} run --fast", $"{DistributionInfo.CommandName} run"], metadata!.Examples);
    }

    private static IEnumerable<Command> Descendants(Command command) =>
        command.Subcommands.SelectMany(Descendants).Prepend(command);
}
