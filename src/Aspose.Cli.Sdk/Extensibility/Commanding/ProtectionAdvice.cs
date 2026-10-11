using System.CommandLine;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The way back to a protected output, for the <c>ENCRYPTION_REMOVED</c> warning of a command:
/// read from the command tree, so it names only what this build can do. The command itself may
/// write a format that carries a password; an edit of any product whose vocabulary has an
/// operation that encrypts (<see cref="Operations.EncryptsOutputAttribute"/>) may encrypt a file of
/// the format written, with a command line that runs as given; otherwise no command of this build
/// encrypts that format.
/// </summary>
internal static class ProtectionAdvice
{
    private const string Placeholder = "<VARIABLE>";

    // The edits that can encrypt the document they publish: the operation document that does it.
    private static readonly ConditionalWeakTable<Command, Func<string, string?>> Encrypting = [];

    /// <summary>Declares that the edit <paramref name="command"/> encrypts with the document <paramref name="document"/> writes for a variable.</summary>
    internal static void Declare(Command command, Func<string, string?> document) => Encrypting.AddOrUpdate(command, document);

    /// <summary>The hint for outputs of <paramref name="formats"/> that lost the input's encryption.</summary>
    /// <param name="parse">The invocation, whose command and command tree are read.</param>
    /// <param name="formats">The format ids of the unencrypted copies.</param>
    /// <param name="output">The output file of the command, or null when it wrote none or several.</param>
    internal static string Hint(ParseResult parse, IReadOnlyList<string> formats, ResolvedOutput? output)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(formats);
        const string EncryptEnvironment = StandardOptionNames.Encrypt + StandardOptionNames.EnvironmentSuffix;
        Command current = parse.CommandResult.Command;
        StandardOptions? options = StandardOptions.Of(current);
        IReadOnlyList<FormatDescriptor> own = options?.Protects ?? [];
        (string Path, Command Command)[] commands = [.. Commands(parse.RootCommandResult.Command, DistributionInfo.CommandName)];
        // The input's password, when an environment variable holds it, protects the copy as it did the input.
        string? variable = options?.InputPassword?.EnvironmentName(parse) ?? options?.OtherPassword?.EnvironmentName(parse);
        var ways = new List<string>();
        if (own.Count > 0)
        {
            ways.Add($"write it as {string.Join(", ", own.Select(static format => format.Id).Distinct(StringComparer.Ordinal))} instead, "
                + $"which keeps the input's password or takes the one {EncryptEnvironment} names");
        }

        var unprotectable = new List<string>();
        foreach (string format in formats.Where(format => !own.Any(protectable => Is(protectable, format))))
        {
            string file = output is not null && Is(output.Format, format) ? Quoted(output.Path) : "<file>";
            if (EncryptingLine(commands, current, format, file, variable ?? Placeholder) is { } line)
            {
                ways.Add(variable is null
                    ? $"encrypt the {format} file afterwards, with {Placeholder} naming an environment variable that holds the password: `{line}`"
                    : $"encrypt the {format} file afterwards with the input's password: `{line}`");
            }
            else
            {
                unprotectable.Add(format);
            }
        }

        string cannot = unprotectable.Count == 0
            ? string.Empty
            : $"No command of this build can encrypt {string.Join(" or ", unprotectable)} files.";
        return ways.Count == 0
            ? cannot
            : $"If the output must stay protected, {string.Join(", or ", ways)}.{(cannot.Length == 0 ? string.Empty : " " + cannot)}";
    }

    // The command line of an edit that encrypts a file of the format in place.
    private static string? EncryptingLine(
        IEnumerable<(string Path, Command Command)> commands, Command current, string format, string file, string variable)
    {
        foreach ((string path, Command command) in commands)
        {
            if (command != current && !command.Hidden
                && StandardOptions.Of(command) is { OutputTarget.Kind: OutputKind.Mutation } options
                && options.Protects.Any(protectable => Is(protectable, format))
                && Encrypting.TryGetValue(command, out Func<string, string?>? document)
                && document(variable) is { } ops)
            {
                // Single quotes keep a Windows path whole in PowerShell and in POSIX shells alike.
                return $"{path} '{file}' {StandardOptionNames.InPlace} {StandardOptionNames.Ops} '{ops}'";
            }
        }

        return null;
    }

    private static bool Is(FormatDescriptor format, string id) => string.Equals(format.Id, id, StringComparison.OrdinalIgnoreCase);

    // A path as a command line names it: quoted when it holds a space.
    private static string Quoted(string path) => path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;

    private static IEnumerable<(string Path, Command Command)> Commands(Command command, string path)
    {
        foreach (Command child in command.Subcommands)
        {
            string childPath = $"{path} {child.Name}";
            yield return (childPath, child);
            foreach ((string Path, Command Command) descendant in Commands(child, childPath))
            {
                yield return descendant;
            }
        }
    }
}
