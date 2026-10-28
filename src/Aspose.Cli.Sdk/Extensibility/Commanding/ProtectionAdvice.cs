using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The way back to a protected output, for the <c>ENCRYPTION_REMOVED</c> warning of a command:
/// read from the command tree, so it names only what this build can do. The command itself may
/// run again with <c>--encrypt-env</c> or write a format that carries a password; otherwise the
/// hint names, by its command path, an edit of any product whose vocabulary has an operation that
/// encrypts (<see cref="Operations.EncryptsOutputAttribute"/>) or a conversion that takes
/// <c>--encrypt-env</c> and protects the format written; the product's Skill says how to use it.
/// With neither, no command of this build encrypts that format. When that conversion also reads the
/// input's format, and the command was given none of its own options, such as a page selection,
/// the hint converts the input itself instead: converting the output again is a second
/// conversion, which can change it, as a document that asks every save to embed its fonts embeds
/// all of them in full when it is saved again. An output in a variant of a format
/// that cannot carry a password, such as an archival variant whose standard forbids encryption, is
/// never encrypted itself: only a copy of it. An edit encrypts the written file in place; a way
/// that writes an encrypted file beside it, as a conversion does, ends by telling to delete the
/// unencrypted file once the copy exists.
/// </summary>
internal static class ProtectionAdvice
{
    private static readonly object Declared = new();

    // The edits whose vocabulary has an operation that encrypts the document they publish.
    private static readonly ConditionalWeakTable<Command, object> Encrypting = [];

    /// <summary>Declares that the edit <paramref name="command"/> can encrypt the document it publishes.</summary>
    internal static void Declare(Command command) => Encrypting.AddOrUpdate(command, Declared);

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
        // The input's format, when a conversion of another product can convert the input itself:
        // only for a conversion given none of its own options, which that one might not take.
        FormatDescriptor? source = input is not null && options is { Target: not null } && WholeConversion(parse, options)
            && ProductReads.Of(current) is { } reads
                ? reads.FirstOrDefault(format => format.Names(input)) ?? (reads.Count == 1 ? reads[0] : null)
                : null;
        string[] kept = [.. loss.Formats.Where(format => own.Any(protectable => Is(protectable, format)))];
        string[] other = [.. loss.Formats.Except(kept, StringComparer.Ordinal)];
        var ways = new List<string>();
        // Whether a way writes an encrypted file beside the unencrypted one this command wrote,
        // rather than writing that output again or encrypting it in place, as an edit does.
        bool copies = false;
        var notes = new List<string>();
        // What the output cannot be, said before the ways to protect it.
        var limits = new List<string>();
        // A copy in a format the command protects lost its password to the command itself, such
        // as to an operation that decrypts, or came from an encrypted document it read; a command
        // protects only what its --encrypt-env encrypts.
        foreach (string format in kept)
        {
            (string Path, Command Command, bool Converts)? encryptor = Encryptor(commands, excluded: null, format);
            string again = variable is null
                ? $"run the command again with {EncryptEnvironment} naming an environment variable that holds a password"
                : $"run the command again with {EncryptEnvironment} {variable}";
            if (loss.Requested)
            {
                notes.Add($"The {format} output is not encrypted because the command decrypted it as it was asked to; that is the expected result."
                    + (encryptor is null ? string.Empty : $" To protect it, encrypt it with '{encryptor.Value.Path}' instead of decrypting it."));
            }
            else if (loss.OpenPassword)
            {
                ways.Add(again + ", which encrypts the output with that password to open it and grants every permission");
            }
            else if (encryptor is { Converts: false } edit)
            {
                // An input with only an owner password opens without one, as an --encrypt-env
                // output would not; only an edit's operation that encrypts takes an owner password alone.
                ways.Add($"encrypt the {format} file with '{edit.Path} {StandardOptionNames.InPlace}' and an owner password only, which restricts it as the input was and keeps it opening without a password");
            }
            else if (encryptor is not null)
            {
                ways.Add(again + ", which encrypts the output with that password to open it, although the input opens without one");
            }
        }

        if (own.Count > 0 && other.Length > 0)
        {
            ways.Add($"write it as {string.Join(", ", own.Select(static format => format.Id).Distinct(StringComparer.Ordinal))} instead, "
                + $"which keeps the input's password or takes the one {EncryptEnvironment} names");
            copies = true;
        }

        var unprotectable = new List<string>();
        foreach (string lost in other)
        {
            // An output in a variant that cannot carry a password, such as an archival one, is a
            // copy of another, protectable format that names the same file by its extension; encrypting
            // the file would end the variant, as a standard that forbids encryption.
            FormatDescriptor? variantOf = output is not null && Is(output.Format, lost) && !output.Format.Protectable
                && ProtectableNaming(commands, output.Path) is { } protectable && !Is(protectable, lost)
                    ? protectable
                    : null;
            string format = variantOf?.Id ?? lost;
            if (variantOf is not null)
            {
                limits.Add($"A {output!.Format.Id} file cannot be encrypted and stay {output.Format.Id}.");
            }

            bool written = output is not null && Is(output.Format, lost);
            if (Encryptor(commands, current, format) is not { } encryptor)
            {
                unprotectable.Add(format);
                continue;
            }

            if (written && variantOf is null && source is not null && encryptor.Converts
                && ProductReads.Of(encryptor.Command)?.Any(read => Is(read, source.Id)) == true)
            {
                ways.Add($"convert the {source.Id} input itself to {format}, not the {format} file this command wrote, "
                    + $"with '{encryptor.Path} {EncryptEnvironment}'");
                copies = true;
            }
            else if (variantOf is not null)
            {
                // Encrypting the variant in place would end it: the copy keeps it.
                ways.Add($"encrypt a {format} copy of it with '{encryptor.Path}'");
                copies = true;
            }
            else if (!encryptor.Converts)
            {
                ways.Add($"encrypt the {format} file with '{encryptor.Path} {StandardOptionNames.InPlace}'");
            }
            else
            {
                ways.Add($"encrypt the {format} file with '{encryptor.Path}'");
                copies = true;
            }
        }

        if (unprotectable.Count > 0)
        {
            notes.Add($"No command of this build can encrypt {string.Join(" or ", unprotectable)} files.");
        }

        string said = ways.Count == 0 ? string.Empty : $"If the output must stay protected, {string.Join(", or ", ways.Distinct(StringComparer.Ordinal))}."
            + (!copies ? string.Empty
                : output is null ? " Once the encrypted copies exist, delete the unencrypted files this command wrote."
                : " Once the encrypted copy exists, delete the unencrypted file this command wrote.");
        return string.Join(" ", limits.Append(said).Concat(notes).Where(static part => part.Length > 0));
    }

    // The command, other than the one excluded, that encrypts a file of the format: an edit whose
    // vocabulary encrypts, else a conversion that writes the format with --encrypt-env; null when
    // none does.
    private static (string Path, Command Command, bool Converts)? Encryptor(IEnumerable<(string Path, Command Command)> commands, Command? excluded, string format)
    {
        (string Path, Command Command, StandardOptions Options)[] protecting =
        [
            .. commands
                .Where(entry => entry.Command != excluded && !entry.Command.Hidden)
                .Select(static entry => (entry.Path, entry.Command, Options: StandardOptions.Of(entry.Command)))
                .Where(entry => entry.Options?.Protects.Any(protectable => Is(protectable, format)) == true)
                .Select(static entry => (entry.Path, entry.Command, entry.Options!)),
        ];
        foreach ((string path, Command command, StandardOptions found) in protecting)
        {
            if (found.OutputTarget?.Kind == OutputKind.Mutation && Encrypting.TryGetValue(command, out _))
            {
                return (path, command, false);
            }
        }

        foreach ((string path, Command command, StandardOptions found) in protecting)
        {
            if (found is { Input: not null, To: not null, OutputTarget.Kind: OutputKind.File })
            {
                return (path, command, true);
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
