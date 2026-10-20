using System.CommandLine;
using System.Text.RegularExpressions;
using Aspose.Cli.Host.Commands;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// Capabilities lists no hidden command, so the command tree itself is checked: a command is
/// hidden exactly when its name starts with <c>__</c> (C-R04), the form the documentation checks
/// recognize, and a hidden command's options keep the lowercase kebab-case long names of every
/// other option (C-R06).
/// </summary>
public sealed partial class HiddenCommandTests
{
    [Fact]
    public void ACommandIsHiddenExactlyWhenItsNameStartsWithTwoUnderscores()
    {
        (string Path, Command Command)[] commands = [.. Walk(RootCommandFactory.Create(ActualCommandTree.Host, out _), string.Empty)];
        Assert.Contains(commands, static entry => entry.Command.Hidden);

        string[] problems =
        [
            .. commands
                .Where(static entry => entry.Command.Hidden != entry.Command.Name.StartsWith("__", StringComparison.Ordinal))
                .Select(static entry => entry.Command.Hidden
                    ? $"{entry.Path} is hidden, so its name starts with '__'."
                    : $"{entry.Path} starts with '__', so it is hidden."),
            .. commands
                .Where(static entry => entry.Command.Hidden)
                .SelectMany(static entry => entry.Command.Options
                    .Where(static option => !option.Name.StartsWith("--", StringComparison.Ordinal) || !Kebab().IsMatch(option.Name[2..]))
                    .Select(option => $"{entry.Path} {option.Name} is not a lowercase kebab-case long name.")),
            .. commands
                .Where(static entry => entry.Command.Hidden)
                .SelectMany(static entry => entry.Command.Options
                    .Where(static option => option.Aliases.Count > 0)
                    .Select(option => $"{entry.Path} {option.Name} has a short name; a hidden command's options have none.")),
        ];

        Assert.True(problems.Length == 0, string.Join(Environment.NewLine, problems));
    }

    private static IEnumerable<(string Path, Command Command)> Walk(Command command, string parent)
    {
        string path = parent.Length == 0 ? command.Name : $"{parent} {command.Name}";
        yield return (path, command);
        foreach (Command child in command.Subcommands)
        {
            foreach ((string Path, Command Command) entry in Walk(child, path))
            {
                yield return entry;
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Kebab();
}
