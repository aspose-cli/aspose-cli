using System.CommandLine;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;

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

    // The edits that can encrypt the document they publish: the operation document that does it,
    // for a variable (null for a placeholder per field), whether the document is to need a
    // password to open, and the fields that restrict it.
    private static readonly ConditionalWeakTable<Command, Func<string?, bool, JsonObject?, string?>> Encrypting = [];

    /// <summary>Declares that the edit <paramref name="command"/> encrypts with the document <paramref name="document"/> writes.</summary>
    internal static void Declare(Command command, Func<string?, bool, JsonObject?, string?> document) => Encrypting.AddOrUpdate(command, document);

    /// <summary>The hint for the outputs that lost the input's encryption.</summary>
    /// <param name="parse">The invocation, whose command and command tree are read.</param>
    /// <param name="loss">The format ids of the unencrypted copies and how the input was protected.</param>
    /// <param name="output">The output file of the command, or null when it wrote none or several.</param>
    internal static string Hint(ParseResult parse, EncryptionLoss loss, ResolvedOutput? output)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(loss);
        const string EncryptEnvironment = StandardOptionNames.Encrypt + StandardOptionNames.EnvironmentSuffix;
        Command current = parse.CommandResult.Command;
        StandardOptions? options = StandardOptions.Of(current);
        IReadOnlyList<FormatDescriptor> own = options?.Protects ?? [];
        (string Path, Command Command)[] commands = [.. Commands(parse.RootCommandResult.Command, DistributionInfo.CommandName)];
        // The input's password, when an environment variable holds it, protects the copy as it did the input.
        string? variable = options?.InputPassword?.EnvironmentName(parse) ?? options?.OtherPassword?.EnvironmentName(parse);
        string[] kept = [.. loss.Formats.Where(format => own.Any(protectable => Is(protectable, format)))];
        string[] other = [.. loss.Formats.Except(kept, StringComparer.Ordinal)];
        var ways = new List<string>();
        var notes = new List<string>();
        // A copy in a format the command protects lost its password to the command itself, such
        // as to an operation that decrypts, or came from an encrypted document it read.
        foreach (string format in kept)
        {
            if (loss.Requested)
            {
                string? operation = EncryptingOperation(commands, format);
                notes.Add($"The {format} output is not encrypted because the command decrypted it as it was asked to; that is the expected result."
                    + (operation is null ? string.Empty : $" To protect it, encrypt it with {operation} instead of decrypting it."));
                continue;
            }

            string file = output is not null && Is(output.Format, format) ? Quoted(output.Path) : "<file>";
            // The input's own protection: its passwords as placeholders, one for each, and its restrictions.
            string? line = EncryptingLine(commands, excluded: null, format, file, variable: null, loss.OpenPassword, loss.Restrictions);
            if (!loss.OpenPassword)
            {
                if (line is not null)
                {
                    ways.Add($"encrypt the {format} file afterwards with an owner password only, which restricts it as the input was and keeps it opening without a password"
                        + (options?.Encrypt is not null ? $" ({EncryptEnvironment} would make it need a password to open)" : string.Empty)
                        + $", with each <...> naming the environment variable that holds that password: `{line}`");
                }

                continue;
            }

            if (options?.Encrypt is not null)
            {
                ways.Add((variable is null
                        ? $"run the command again with {EncryptEnvironment} naming an environment variable that holds a password"
                        : $"run the command again with {EncryptEnvironment} {variable}")
                    + ", which encrypts the output with that password to open it and grants every permission");
            }

            if (line is not null)
            {
                ways.Add($"to restore the input's own open and owner passwords and its permissions, encrypt the {format} file afterwards, "
                    + $"with each <...> naming the environment variable that holds that password: `{line}`");
            }
        }

        if (own.Count > 0 && other.Length > 0)
        {
            ways.Add($"write it as {string.Join(", ", own.Select(static format => format.Id).Distinct(StringComparer.Ordinal))} instead, "
                + $"which keeps the input's password or takes the one {EncryptEnvironment} names");
        }

        var unprotectable = new List<string>();
        foreach (string format in other)
        {
            string file = output is not null && Is(output.Format, format) ? Quoted(output.Path) : "<file>";
            if (EncryptingLine(commands, current, format, file, variable ?? Placeholder, openPassword: true, restrictions: null) is { } line)
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

        if (unprotectable.Count > 0)
        {
            notes.Add($"No command of this build can encrypt {string.Join(" or ", unprotectable)} files.");
        }

        string said = ways.Count == 0 ? string.Empty : $"If the output must stay protected, {string.Join(", or ", ways.Distinct(StringComparer.Ordinal))}.";
        return string.Join(" ", notes.Prepend(said).Where(static part => part.Length > 0));
    }

    // The name of the operation an edit of the format encrypts with, or null when no edit does.
    private static string? EncryptingOperation(IEnumerable<(string Path, Command Command)> commands, string format) =>
        Edits(commands, excluded: null, format)
            .Select(static edit => edit.Document(null, true, null))
            .OfType<string>()
            .Select(static document => JsonNode.Parse(document)?["ops"]?[0]?["op"]?.GetValue<string>())
            .FirstOrDefault(static name => name is not null);

    // The command line of an edit, other than the one excluded, that encrypts a file of the format in place.
    private static string? EncryptingLine(
        IEnumerable<(string Path, Command Command)> commands, Command? excluded, string format, string file, string? variable, bool openPassword, JsonObject? restrictions) =>
        Edits(commands, excluded, format)
            .Select(edit => (edit.Path, Ops: edit.Document(variable, openPassword, restrictions)))
            .Where(static edit => edit.Ops is not null)
            // Single quotes keep a Windows path whole in PowerShell and in POSIX shells alike.
            .Select(edit => $"{edit.Path} '{file}' {StandardOptionNames.InPlace} {StandardOptionNames.Ops} '{edit.Ops}'")
            .FirstOrDefault();

    // The edits, other than the one excluded, that can encrypt a file of the format in place.
    private static IEnumerable<(string Path, Func<string?, bool, JsonObject?, string?> Document)> Edits(
        IEnumerable<(string Path, Command Command)> commands, Command? excluded, string format)
    {
        foreach ((string path, Command command) in commands)
        {
            if (command != excluded && !command.Hidden
                && StandardOptions.Of(command) is { OutputTarget.Kind: OutputKind.Mutation } options
                && options.Protects.Any(protectable => Is(protectable, format))
                && Encrypting.TryGetValue(command, out Func<string?, bool, JsonObject?, string?>? document))
            {
                yield return (path, document);
            }
        }
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
