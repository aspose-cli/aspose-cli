using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The way back to a protected output, for the <c>ENCRYPTION_REMOVED</c> warning of a command:
/// read from the command tree, so it names only what this build can do. The command itself may
/// write a format that carries a password; an edit of any product whose vocabulary has an
/// operation that encrypts (<see cref="Operations.EncryptsOutputAttribute"/>) may encrypt a file of
/// the format written, or else a conversion that takes <c>--encrypt-env</c> may write an encrypted
/// copy of it, each with a command line that runs as given and names the output's own path;
/// otherwise no command of this build encrypts that format. When that conversion also reads the
/// input's format, and the command was given none of its own options, such as a page selection,
/// the copy is converted from the input once instead: saving the output again is a second
/// conversion, which can change it, as a document that asks every save to embed its fonts embeds
/// all of them in full when it is saved again. An output in a variant of a format
/// that cannot carry a password, such as an archival variant whose standard forbids encryption, is
/// never encrypted itself: only a copy beside it.
/// </summary>
internal static class ProtectionAdvice
{
    private const string Placeholder = "<password-env>";

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
    /// <param name="input">The command's input document, or null when it read none.</param>
    internal static string Hint(ParseResult parse, EncryptionLoss loss, ResolvedOutput? output, string? input)
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
        // The input, when another conversion can write the encrypted copy from it directly: only a
        // conversion given none of its own options, which that conversion might not take.
        (string Path, FormatDescriptor Format)? source = input is not null && options is { Target: not null } && WholeConversion(parse, options)
            && ProductReads.Of(current) is { } reads
            && (reads.FirstOrDefault(format => format.Names(input)) ?? (reads.Count == 1 ? reads[0] : null)) is { } read
                ? (input, read)
                : null;
        string[] kept = [.. loss.Formats.Where(format => own.Any(protectable => Is(protectable, format)))];
        string[] other = [.. loss.Formats.Except(kept, StringComparer.Ordinal)];
        var ways = new List<string>();
        var notes = new List<string>();
        // What the output cannot be, said before the ways to protect it.
        var limits = new List<string>();
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

            string file = output is not null && Is(output.Format, format) ? output.Path : "<file>";
            // The input's own protection: its passwords as placeholders, one for each, and its restrictions.
            string? line = EncryptingLine(commands, excluded: null, format, file, StandardOptionNames.InPlace, variable: null, loss.OpenPassword, loss.Restrictions);
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
        foreach (string lost in other)
        {
            // An output in a variant that cannot carry a password, such as an archival one, is a
            // copy of the protectable format that names the same file by its extension.
            string format = output is not null && Is(output.Format, lost) && !output.Format.Protectable
                && ProtectableNaming(commands, output.Path) is { } protectable
                    ? protectable.Id
                    : lost;
            // The output file is the copy when it is written in the lost format.
            bool written = output is not null && Is(output.Format, lost);
            string file = written ? output!.Path : "<file>";
            string password = variable is null
                ? $", with {Placeholder} naming an environment variable that holds the password"
                : " with the input's password";
            string variableName = variable ?? Placeholder;
            // A variant that cannot carry a password, as a standard that forbids encryption: encrypting the
            // file would end the variant, so only a copy beside it may be encrypted.
            if (written && !Is(output!.Format, format) && !output.Format.Protectable)
            {
                limits.Add($"A {output.Format.Id} file cannot be encrypted and stay {output.Format.Id}.");
                string copy = $"{StandardOptionNames.Out} '{EncryptedCopy(output.Path)}'";
                if (EncryptingLine(commands, current, format, file, copy, variableName, openPassword: true, restrictions: null) is { } copyLine)
                {
                    ways.Add($"write an encrypted {format} copy beside it{password}: `{copyLine}`");
                }

                continue;
            }

            if (EncryptingLine(commands, current, format, file, StandardOptionNames.InPlace, variableName, openPassword: true, restrictions: null) is { } line)
            {
                ways.Add($"encrypt the {format} file afterwards{password}: `{line}`");
            }
            else if (written && source is { } from && ConvertingLine(commands, current, format, file, variableName, from) is { } direct)
            {
                ways.Add($"write an encrypted {format} copy from the input directly{password}: `{direct}`");
            }
            else if (ConvertingLine(commands, current, format, file, variableName, source: null) is { } converted)
            {
                ways.Add($"write an encrypted copy of the {format} file{password}: `{converted}`");
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
        return string.Join(" ", limits.Append(said).Concat(notes).Where(static part => part.Length > 0));
    }

    // The name of the operation an edit of the format encrypts with, or null when no edit does.
    private static string? EncryptingOperation(IEnumerable<(string Path, Command Command)> commands, string format) =>
        Edits(commands, excluded: null, format)
            .Select(static edit => edit.Document(null, true, null))
            .OfType<string>()
            .Select(static document => JsonNode.Parse(document)?["ops"]?[0]?["op"]?.GetValue<string>())
            .FirstOrDefault(static name => name is not null);

    // The command line of an edit, other than the one excluded, that encrypts a file of the format
    // where the destination options put it: --in-place, or an --out.
    private static string? EncryptingLine(
        IEnumerable<(string Path, Command Command)> commands, Command? excluded, string format, string file, string destination, string? variable, bool openPassword, JsonObject? restrictions) =>
        Edits(commands, excluded, format)
            .Select(edit => (edit.Path, Ops: edit.Document(variable, openPassword, restrictions)))
            .Where(static edit => edit.Ops is not null)
            // Single quotes keep a Windows path whole in PowerShell and in POSIX shells alike.
            .Select(edit => $"{edit.Path} '{file}' {destination} {StandardOptionNames.Ops} '{edit.Ops}'")
            .FirstOrDefault();

    // The command line of a conversion, other than the one excluded, that writes an encrypted copy
    // of a file of the format, in that format, beside it: from that file, or, given a source, from
    // the source with its password, by a conversion that reads the source's format.
    private static string? ConvertingLine(
        IEnumerable<(string Path, Command Command)> commands, Command? excluded, string format, string file, string variable,
        (string Path, FormatDescriptor Format)? source)
    {
        foreach ((string path, Command command) in commands)
        {
            if (command != excluded && !command.Hidden
                && StandardOptions.Of(command) is { Input: not null, To: { } to, Encrypt: { } encrypt, OutputTarget.Kind: OutputKind.File } options
                && options.Protects.FirstOrDefault(protectable => Is(protectable, format)) is { } protectable)
            {
                string copy = file == "<file>" ? "<encrypted copy>" : EncryptedCopy(file);
                string target = $"{to.Name} {protectable.Id} {StandardOptionNames.Out} '{copy}' {encrypt.EnvironmentOption} {variable}";
                if (source is null)
                {
                    return $"{path} '{file}' {target}";
                }

                if (options.InputPassword is { } password
                    && ProductReads.Of(command) is { } reads
                    && reads.Any(read => Is(read, source.Value.Format.Id)))
                {
                    return $"{path} '{source.Value.Path}' {password.EnvironmentOption} {variable} {target}";
                }
            }
        }

        return null;
    }

    // Whether the invocation gave none of the command's own arguments and options, so that it needs
    // no option that another product's conversion of its input might not take.
    private static bool WholeConversion(ParseResult parse, StandardOptions options) =>
        options.Own.All(parameter => parameter switch
        {
            Option option => parse.GetResult(option) is not OptionResult { Implicit: false },
            Argument argument => parse.GetResult(argument) is not ArgumentResult { Tokens.Count: > 0 },
            _ => true,
        });

    // The first format some command protects that names the file by its extension.
    private static FormatDescriptor? ProtectableNaming(IEnumerable<(string Path, Command Command)> commands, string path) =>
        commands
            .Select(static entry => StandardOptions.Of(entry.Command))
            .SelectMany(static options => options?.Protects ?? [])
            .FirstOrDefault(protectable => protectable.Names(path));

    // The path of an encrypted copy beside a file: its name with '.encrypted' before the extension.
    private static string EncryptedCopy(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(path)}.encrypted{Path.GetExtension(path)}");

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
