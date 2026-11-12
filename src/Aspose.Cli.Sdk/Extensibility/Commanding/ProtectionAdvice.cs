using System.CommandLine;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The operation documents with which an edit only encrypts the document it publishes, each an
/// operation array its <c>--ops</c> takes, built from the environment variable that holds a password.
/// </summary>
/// <param name="AsOutputPassword">
/// Encrypts as the edit's output password option does, with the variable's password to open the
/// file; null when the edit declares none.
/// </param>
/// <param name="OwnerOnly">
/// Sets only an owner password, the variable's, so the file still opens without a password, and
/// grants readers no permission; null when the edit declares none.
/// </param>
/// <param name="Permissions">How a caller grants what the original did with <paramref name="OwnerOnly"/>; null without it.</param>
internal sealed record EncryptionOperations(Func<string, string>? AsOutputPassword, Func<string, string>? OwnerOnly, PermissionsReader? Permissions = null);

/// <summary>
/// The field of an operation that only sets an owner password which grants readers permissions,
/// and the product's command, with its options, that reads what a document grants.
/// </summary>
/// <param name="Field">The operation's field, such as <c>permissions</c>.</param>
/// <param name="Command">The command below the product, such as <c>inspect</c>.</param>
/// <param name="Options">The options and values it reads the permissions with.</param>
internal sealed record PermissionsReader(string Field, string Command, IReadOnlyList<(string Name, string Value)> Options);

/// <summary>
/// The way back to a protected output, for the <c>ENCRYPTION_REMOVED</c> warning of a command:
/// one step, after the reason for it, read from the command tree so it names only what this build
/// can do, as command lines that run as written (<see cref="ContinuationCommand"/>). The command
/// itself runs again with <c>--encrypt-env</c> and <c>--overwrite</c>, replacing what it wrote, or
/// writes a format that carries a password; otherwise an edit of any product whose vocabulary has
/// an operation that encrypts (<see cref="Operations.EncryptsOutputAttribute"/>) encrypts the
/// written file in place with the operation document it declares (<see cref="EncryptionOperations"/>),
/// or a conversion that takes <c>--encrypt-env</c> and protects the format written either converts
/// the input itself again, replacing the file, when it reads the input's format, or writes an
/// encrypted copy of the file. Converting the output again is a second conversion, which can change
/// it, as a document that asks every save to embed its fonts embeds all of them in full when it is
/// saved again; a conversion that selected pages splits them from the input first and converts the
/// part (<see cref="StandardInvocation.FromInput"/>). An output in a variant of a format that cannot
/// carry a password, such as an archival variant whose standard forbids encryption, is never
/// encrypted itself: only a copy of it. A step that writes an encrypted copy beside the unencrypted
/// file this command wrote ends by telling to delete that file once the copy exists. With no
/// command that encrypts a format, the hint says so; the platform Skill describes the other ways.
/// A command line that holds a secret given literally is never repeated, so the step is then told
/// in words.
/// </summary>
internal static class ProtectionAdvice
{
    private const string OwnerPasswordPlaceholder = "<owner-password-env>";

    // The placeholder for each of several files the command wrote.
    private const string EachFile = "<file>";

    // The placeholder for the input a command read among several.
    private const string OriginalPlaceholder = "<original>";

    private const string Protected = "If the output must stay protected, ";

    private const string EncryptEnvironment = StandardOptionNames.Encrypt + StandardOptionNames.EnvironmentSuffix;

    // The edits whose vocabulary has an operation that encrypts the document they publish.
    private static readonly ConditionalWeakTable<Command, EncryptionOperations> Encrypting = [];

    /// <summary>
    /// Declares that the edit <paramref name="command"/> can encrypt the document it publishes,
    /// with the operation documents that only encrypt it.
    /// </summary>
    internal static void Declare(Command command, EncryptionOperations operations) => Encrypting.AddOrUpdate(command, operations);

    /// <summary>The hint for the outputs that lost the input's encryption.</summary>
    /// <param name="parse">The invocation, whose command and command tree are read.</param>
    /// <param name="invocation">The command's invocation, its input and output, which its steps repeat.</param>
    /// <param name="loss">The format ids of the unencrypted copies and how the input was protected.</param>
    internal static string Hint(ParseResult parse, StandardInvocation invocation, EncryptionLoss loss)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(loss);
        Command current = parse.CommandResult.Command;
        StandardOptions? options = StandardOptions.Of(current);
        IReadOnlyList<FormatDescriptor> own = options?.Protects ?? [];
        (string[] Path, Command Command)[] commands = [.. Commands(parse.RootCommandResult.Command, [])];
        ResolvedOutput? output = invocation.ResolvedFile;
        string? input = invocation.ResolvedInput;
        // The input's password, when an environment variable holds it, protects the copy as it did the input.
        string? variable = options?.InputPassword?.EnvironmentName(parse) ?? options?.OtherPassword?.EnvironmentName(parse);
        // The input's format, when a conversion of another product can convert the input itself.
        FormatDescriptor? source = input is not null && options is { Target: not null } && ProductReads.Of(current) is { } reads
            ? reads.FirstOrDefault(format => format.Names(input)) ?? (reads.Count == 1 ? reads[0] : null)
            : null;
        string[] kept = [.. loss.Formats.Where(format => own.Any(protectable => Is(protectable, format)))];
        string[] other = [.. loss.Formats.Except(kept, StringComparer.Ordinal)];
        var context = new Context(invocation, options, output, variable, loss.OpenPassword);
        Step? step = null;
        var notes = new List<string>();
        // What the output cannot be, said before the step that protects it.
        var limits = new List<string>();
        // A copy in a format the command protects lost its password to the command itself, such
        // as to an operation that decrypts, or came from an encrypted document it read; a command
        // protects only what its --encrypt-env encrypts.
        foreach (string format in kept)
        {
            Encryptor? encryptor = EncryptorOf(commands, excluded: null, format);
            if (loss.Requested)
            {
                notes.Add($"The {format} output is not encrypted because the command decrypted it as it was asked to; that is the expected result."
                    + (encryptor is null ? string.Empty : $" To protect it, encrypt it with '{encryptor.Line}' instead of decrypting it."));
            }
            else if (loss.OpenPassword)
            {
                step ??= context.Again(
                    "Only the password of the document a command opens carries over to its output, not one of a document it takes content from.",
                    "which writes the output again encrypted with a password to open it that grants every permission",
                    encryptor);
            }
            else if (encryptor is { Converts: false } edit)
            {
                // An input with only an owner password opens without one, as an --encrypt-env
                // output would not; only an edit's operation that encrypts takes an owner password alone.
                step ??= context.InPlace(edit, format,
                    "The input had only an owner password, which restricts what readers may do but asks for no password to open it.");
            }
            else if (encryptor is not null)
            {
                step ??= context.Again(
                    $"The input opens without a password, but only a password to open it protects a {format} file.",
                    "which writes the output again encrypted with a password to open it, although the input opens without one",
                    encryptor: null);
            }
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

            if (EncryptorOf(commands, current, format) is not { } encryptor)
            {
                unprotectable.Add(format);
                continue;
            }

            bool written = output is not null && Is(output.Format, lost);
            step ??= written && variantOf is null && source is not null && encryptor.Converts
                && ProductReads.Of(encryptor.Command)?.Any(read => Is(read, source.Id)) == true
                ? context.FromInput(encryptor, format, source.Id)
                : null;
            step ??= variantOf is not null ? context.Copy(encryptor, format, reason: null)
                : !encryptor.Converts ? context.InPlace(encryptor, format, $"This command cannot encrypt {format} files.")
                : context.Copy(encryptor, format, $"This command cannot encrypt {format} files.");
        }

        string? cannot = unprotectable.Count == 0 ? null : $"No command of this build can encrypt {string.Join(" or ", unprotectable)} files.";
        if (step is null && own.Count > 0 && other.Length > 0)
        {
            // The formats no command encrypts are the reason to write one that carries a password.
            step = context.Instead(own, input, cannot);
            cannot = null;
        }

        return string.Join(" ", limits.Append(step?.Text(output is null)).Concat(notes).Append(cannot).OfType<string>().Where(static part => part.Length > 0));
    }

    /// <summary>
    /// The command line, for a hint, that encrypts <paramref name="file"/> in place with the edit
    /// of any product that encrypts the format as an output password option does, with the password
    /// <paramref name="variable"/> holds; null when no edit declares that operation document.
    /// </summary>
    internal static ContinuationCommand? EncryptInPlace(Command root, string format, string file, string variable) =>
        EncryptorOf([.. Commands(root, [])], excluded: null, format) is { Converts: false, Operations.AsOutputPassword: { } encrypt } edit
            ? new ContinuationCommand(edit.Path).Argument(file).Flag(StandardOptionNames.InPlace).Document(StandardOptionNames.Ops, encrypt(variable))
            : null;

    /// <summary>
    /// What a step that names a placeholder for a password's variable says of it, which the caller
    /// replaces with a variable of its choice; nothing for a variable it names.
    /// </summary>
    internal static string Naming(string variable, string password = "that password") =>
        variable.StartsWith('<') ? $", after replacing {variable} with the name of the environment variable that holds {password}" : string.Empty;

    // The command, other than the one excluded, that encrypts a file of the format: an edit whose
    // vocabulary encrypts, else a conversion that writes the format with --encrypt-env; null when
    // none does.
    private static Encryptor? EncryptorOf(IEnumerable<(string[] Path, Command Command)> commands, Command? excluded, string format)
    {
        (string[] Path, Command Command, StandardOptions Options)[] protecting =
        [
            .. commands
                .Where(entry => entry.Command != excluded && !entry.Command.Hidden)
                .Select(static entry => (entry.Path, entry.Command, Options: StandardOptions.Of(entry.Command)))
                .Where(entry => entry.Options?.Protects.Any(protectable => Is(protectable, format)) == true)
                .Select(static entry => (entry.Path, entry.Command, entry.Options!)),
        ];
        foreach ((string[] path, Command command, StandardOptions found) in protecting)
        {
            if (found.OutputTarget?.Kind == OutputKind.Mutation && Encrypting.TryGetValue(command, out EncryptionOperations? operations))
            {
                return new Encryptor(path, command, found, Converts: false, operations);
            }
        }

        foreach ((string[] path, Command command, StandardOptions found) in protecting)
        {
            if (found is { Input: not null, To: not null, OutputTarget.Kind: OutputKind.File })
            {
                return new Encryptor(path, command, found, Converts: true, Operations: null);
            }
        }

        return null;
    }

    // The first format some command protects that names the file by its extension.
    private static FormatDescriptor? ProtectableNaming(IEnumerable<(string[] Path, Command Command)> commands, string path) =>
        commands
            .Select(static entry => StandardOptions.Of(entry.Command))
            .SelectMany(static options => options?.Protects ?? [])
            .FirstOrDefault(protectable => protectable.Names(path));

    private static bool Is(FormatDescriptor format, string id) => string.Equals(format.Id, id, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<(string[] Path, Command Command)> Commands(Command command, string[] path)
    {
        foreach (Command child in command.Subcommands)
        {
            string[] childPath = [.. path, child.Name];
            yield return (childPath, child);
            foreach ((string[] Path, Command Command) descendant in Commands(child, childPath))
            {
                yield return descendant;
            }
        }
    }

    // A file beside another, named by its stem, a marker and an extension.
    private static string Beside(string path, string marker, string extension) =>
        Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + marker + extension);

    /// <summary>A command that encrypts a format: its path below the root, its options, whether it converts, and the edit's operation documents.</summary>
    private sealed record Encryptor(string[] Path, Command Command, StandardOptions Options, bool Converts, EncryptionOperations? Operations)
    {
        /// <summary>The command path as a hint names the command alone, as in <c>aspose-cli &lt;product&gt; edit</c>.</summary>
        public string Line => string.Join(' ', [DistributionInfo.CommandName, .. Path]);

        /// <summary>The option that names the variable of the password the command encrypts with.</summary>
        public string EncryptOption => Options.Encrypt?.EnvironmentOption ?? EncryptEnvironment;
    }

    /// <summary>
    /// One way to protect the output: the reason for it, what to run and what that does, and
    /// whether it writes an encrypted copy beside the unencrypted file this command wrote.
    /// </summary>
    private sealed record Step(string? Reason, string Way, bool Copies)
    {
        /// <summary>What the hint says after the step and the deletion it ends with, or null.</summary>
        public string? Note { get; init; }

        public string Text(bool several) => string.Join(" ", new[]
        {
            Reason,
            Protected + Way + ".",
            !Copies ? null
                : several ? "Once the encrypted copies exist, delete the unencrypted files this command wrote."
                : "Once the encrypted copy exists, delete the unencrypted file this command wrote.",
            Note,
        }.OfType<string>());
    }

    /// <summary>The invocation a hint's steps repeat or act on the output of.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="options">The command's common parameters.</param>
    /// <param name="output">The output file, or null when the command wrote none or several.</param>
    /// <param name="variable">The variable of the input's password, or null.</param>
    /// <param name="openPassword">Whether an encrypted input needed a password to open; when not, it had only an owner password.</param>
    private sealed class Context(StandardInvocation invocation, StandardOptions? options, ResolvedOutput? output, string? variable, bool openPassword)
    {
        // The variable a step encrypts with: the input's password variable, else a placeholder.
        private string Password => variable ?? ContinuationCommand.PasswordPlaceholder;

        // The file a step acts on: the output, or each of the files the command wrote.
        private string File => output?.Path ?? EachFile;

        private string Each => output is null ? $" on each file this command wrote as {EachFile}" : string.Empty;

        /// <summary>
        /// The command again with the input's password variable as its output password and
        /// <c>--overwrite</c>, replacing what it wrote; for an edit in place, which would apply its
        /// operations twice, the edit that encrypts the format in place instead.
        /// </summary>
        public Step Again(string reason, string effect, Encryptor? encryptor)
        {
            if (output is { InPlace: true })
            {
                return encryptor is { Converts: false } edit && Encrypting(edit) is not null
                    ? InPlace(edit, output.Format.Id, reason)
                    : new Step(reason, $"run the edit again on the original with {EncryptEnvironment} naming an environment variable that holds a password, {effect}", Copies: false);
            }

            Option<bool>? overwrite = options?.Overwrite;
            string option = options?.Encrypt?.EnvironmentOption ?? EncryptEnvironment;
            if (invocation.Again([.. options?.Encrypt?.Options ?? [], overwrite]) is not { } again)
            {
                string replacing = overwrite is null ? string.Empty : $" and {StandardOptionNames.Overwrite}";
                return new Step(reason, $"run the command again with {option} naming an environment variable that holds a password{replacing}, {effect}", Copies: false);
            }

            again.Command.PasswordVariable(option, variable).Flag(StandardOptionNames.Overwrite, overwrite is not null);
            return new Step(reason, $"run {again.Command.Quoted()}{again.Repeating}, {effect}{Naming(Password)}", Copies: false);
        }

        /// <summary>
        /// The edit that encrypts the output in place (<see cref="Encrypting"/>): with only an owner
        /// password when the input had no password to open it, otherwise as an output password option does.
        /// </summary>
        public Step InPlace(Encryptor edit, string format, string reason)
        {
            if (Encrypting(edit) is not { } encrypting)
            {
                return new Step(reason, $"encrypt the {format} file with '{edit.Line} {StandardOptionNames.InPlace}'"
                    + (openPassword ? string.Empty : " and an owner password only, which keeps it opening without a password"), Copies: false);
            }

            ContinuationCommand command = new ContinuationCommand(edit.Path).Argument(File).Flag(StandardOptionNames.InPlace).Document(StandardOptionNames.Ops, encrypting.Document);
            return new Step(reason, $"run {command.Quoted()}{Each}, which encrypts it in place with {encrypting.Effect}{encrypting.Naming}", Copies: false)
            {
                Note = encrypting.Note,
            };
        }

        // The operation document an edit encrypts with, what it encrypts with, what the step says of
        // its placeholder, and what it says after the step: only an owner password for an input that
        // needed no password to open, as that input was, which grants readers no permission until the
        // caller grants what the original did; otherwise a password to open the file that grants
        // every permission, as an output password option does. Null when the edit declares neither.
        private (string Document, string Effect, string Naming, string? Note)? Encrypting(Encryptor edit)
        {
            if (!openPassword && edit.Operations?.OwnerOnly is { } restrict)
            {
                string owner = variable ?? OwnerPasswordPlaceholder;
                return (restrict(owner),
                    "an owner password only: it still opens without a password, but the command grants readers no permission without the owner password",
                    Naming(owner, "the owner password"),
                    edit.Operations.Permissions is { } reader ? Granting(edit, reader) : null);
            }

            return edit.Operations?.AsOutputPassword is { } encrypt
                ? (encrypt(Password), "a password to open it that grants every permission", Naming(Password), null)
                : null;
        }

        // How a caller grants readers what the original allowed: in the operation's field, as the
        // product's command reads them from the input, or from the original a placeholder stands
        // for when the command read several documents.
        private string Granting(Encryptor edit, PermissionsReader reader)
        {
            ContinuationCommand read = invocation.ContinuationOf(invocation.ResolvedInput ?? OriginalPlaceholder).ContinuationAt(edit.Path[0], reader.Command);
            foreach ((string name, string value) in reader.Options)
            {
                read.Option(name, value);
            }

            return $"To grant readers what the original allowed, set the {reader.Field} field of the operation as {read.Quoted()} reports them"
                + (invocation.ResolvedInput is null ? $", with {OriginalPlaceholder} the input that had only an owner password." : ".");
        }

        /// <summary>
        /// The conversion of the input itself again, with an output password, replacing the file
        /// this command wrote; null when the invocation gave the command options of its own that
        /// the conversion cannot repeat.
        /// </summary>
        public Step? FromInput(Encryptor converter, string format, string source)
        {
            if (invocation.FromInput(converter.Path, format, output!.Path) is not { } conversion)
            {
                return null;
            }

            conversion.Commands[^1].PasswordVariable(converter.EncryptOption, variable);
            string effect = conversion.Part is null
                ? $"which converts the {source} input itself and replaces the {format} file with an encrypted one"
                : $"which split the selected pages from the {source} input and convert them, replacing the {format} file with an encrypted one";
            return new Step($"This command cannot encrypt {format} files, and converting the one it wrote again could change it.",
                $"run {ContinuationCommand.Steps(conversion.Commands)}, {effect}{Naming(Password)}{conversion.Leftover}", Copies: false);
        }

        /// <summary>An encrypted copy of the file this command wrote, by the edit or the conversion that encrypts its format.</summary>
        public Step Copy(Encryptor encryptor, string format, string? reason)
        {
            (string Document, string Effect, string Naming, string? Note)? edit = encryptor.Converts ? null : Encrypting(encryptor);
            ContinuationCommand? command = null;
            if (output is not null)
            {
                string copy = Beside(output.Path, ".encrypted", Path.GetExtension(output.Path));
                command = edit is { } encrypting
                    ? new ContinuationCommand(encryptor.Path).Argument(output.Path).Option(StandardOptionNames.Out, copy).Document(StandardOptionNames.Ops, encrypting.Document)
                    : encryptor.Converts
                        ? new ContinuationCommand(encryptor.Path).Argument(output.Path)
                            .Option(StandardOptionNames.To, format).Option(StandardOptionNames.Out, copy).PasswordVariable(encryptor.EncryptOption, variable)
                        : null;
            }

            (string effect, string naming) = edit is { } found ? (found.Effect, found.Naming) : ("a password to open it", Naming(Password));
            return command is null
                ? new Step(reason, reason is null ? $"encrypt a {format} copy of it with '{encryptor.Line}'" : $"encrypt the {format} file with '{encryptor.Line}'", Copies: true)
                : new Step(reason, $"run {command.Quoted()}, which writes an encrypted {format} copy of it with {effect}{naming}", Copies: true) { Note = edit?.Note };
        }

        /// <summary>
        /// The command again, writing a format it protects instead, encrypted with the input's
        /// password variable: the input's own format when it is one, else the first.
        /// </summary>
        public Step Instead(IReadOnlyList<FormatDescriptor> own, string? input, string? reason)
        {
            FormatDescriptor format = own.FirstOrDefault(protectable => input is not null && protectable.Names(input)) ?? own[0];
            string extension = format.PreferredExtension ?? format.Extensions[0];
            RepeatedInvocation? again = null;
            // An edit in place already changed its input, and a created file is named by an
            // argument; a conversion's own options may apply only to the format it was asked for.
            if (output is { InPlace: false } && options is { CreatedFile: null, Encrypt: { } encrypt }
                && (options.To is null || !invocation.GaveOwnParameters))
            {
                string path = Path.ChangeExtension(output.Path, extension);
                if (input is not null && FilePaths.Same(path, input))
                {
                    path = Beside(output.Path, ".out", extension);
                }

                again = invocation.Again([options.To, options.OutputFile?.Option, options.Overwrite, .. encrypt.Options]);
                if (again is not null && options.To is not null)
                {
                    again.Command.Option(StandardOptionNames.To, format.Id);
                }

                again?.Command.Option(StandardOptionNames.Out, path).PasswordVariable(encrypt.EnvironmentOption, variable);
            }

            return again is null
                ? new Step(reason, $"write it as {string.Join(", ", own.Select(static protectable => protectable.Id).Distinct(StringComparer.Ordinal))} instead, "
                    + $"which keeps the input's password or takes the one {EncryptEnvironment} names", Copies: true)
                : new Step(reason, $"run {again.Command.Quoted()}{again.Repeating}, which writes it as {format.Id} encrypted with a password to open it{Naming(Password)}", Copies: true);
        }
    }
}
