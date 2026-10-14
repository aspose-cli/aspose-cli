using System.CommandLine;
using System.Runtime.CompilerServices;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// The names a command declares a caller may mistake for it (a command definition's <c>MistakenFor</c>),
/// such as a command that no longer exists. They are names of no command: the parser refuses
/// them, and the error suggests every command that declares the name first.
/// </summary>
public static class CommandMistakes
{
    private static readonly ConditionalWeakTable<Command, IReadOnlyList<string>> Declared = [];

    /// <summary>Declares the names <paramref name="command"/> is mistaken for.</summary>
    internal static void Declare(Command command, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || name == command.Name)
            {
                throw new InvalidOperationException($"Command '{command.Name}' is mistaken for '{name}', which is blank or its own name.");
            }
        }

        if (names.Count > 0)
        {
            Declared.AddOrUpdate(command, [.. names]);
        }
    }

    /// <summary>The names a caller may mistake for <paramref name="command"/>; empty when it declares none.</summary>
    /// <param name="command">The command to inspect.</param>
    public static IReadOnlyList<string> MistakenNames(this Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Declared.TryGetValue(command, out IReadOnlyList<string>? names) ? names : [];
    }

    /// <summary>
    /// Throws when a command of <paramref name="parent"/> is mistaken for a name one of its
    /// siblings answers to: that name runs the sibling, so the declaration would never apply.
    /// </summary>
    internal static void EnsureUnclaimed(Command parent)
    {
        string[] names = [.. parent.Subcommands.SelectMany(static command => command.Aliases.Prepend(command.Name))];
        foreach (Command command in parent.Subcommands)
        {
            if (command.MistakenNames().FirstOrDefault(name => names.Contains(name, StringComparer.Ordinal)) is { } claimed)
            {
                throw new InvalidOperationException(
                    $"Command '{parent.Name} {command.Name}' is mistaken for '{claimed}', which a command of '{parent.Name}' answers to.");
            }
        }
    }
}
