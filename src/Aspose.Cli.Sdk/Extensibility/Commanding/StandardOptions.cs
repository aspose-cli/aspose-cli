using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// What a command does, in the terms that decide its common parameters. The command
/// supplies every noun and description; <see cref="StandardOptions"/> decides which arguments
/// and options that means, in one fixed order.
/// </summary>
public sealed record CommandTraits
{
    /// <summary>The document the command reads: the first argument and its password options.</summary>
    public InputDocument? Input { get; init; }

    /// <summary>A second document read beside <see cref="Input"/>, such as the other side of a comparison.</summary>
    public InputDocument? Other { get; init; }

    /// <summary>
    /// The subject of <c>--password</c> for input files the command declares among its own
    /// parameters, such as the list of documents a merge reads or an optional file argument;
    /// <see cref="Input"/> brings its own password instead.
    /// </summary>
    public string? PasswordSubject { get; init; }

    /// <summary>Where the command publishes its result.</summary>
    public OutputTarget? Output { get; init; }

    /// <summary>The password the command can put on its output (<c>--encrypt</c>).</summary>
    public EncryptedOutput? Encrypt { get; init; }

    /// <summary>Whether the result depends on the fonts available to the engine (<c>--font-dir</c>).</summary>
    public bool UsesFonts { get; init; }

    /// <summary>The format the command writes its <see cref="Output"/> file in (<c>--to</c>).</summary>
    public TargetFormat? Target { get; init; }
}

/// <summary>
/// The format a convert or render command writes, chosen by <c>--to</c> by a format id or
/// alias in any case.
/// </summary>
public sealed class TargetFormat
{
    private TargetFormat(FormatUse use, string description, IReadOnlyList<FormatDescriptor> formats, string? defaultFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(formats);
        Use = use;
        Description = description;
        Formats = formats;
        Default = defaultFormat;
    }

    internal FormatUse Use { get; }

    internal string Description { get; }

    internal IReadOnlyList<FormatDescriptor> Formats { get; }

    internal string? Default { get; }

    /// <summary>A required <c>--to</c> among the product's convert formats.</summary>
    /// <param name="description">Help for <c>--to</c>.</param>
    /// <param name="formats">The product's format declarations; those with the convert use are offered.</param>
    public static TargetFormat Convert(string description, IReadOnlyList<FormatDescriptor> formats) =>
        new(FormatUse.Convert, description, formats, defaultFormat: null);

    /// <summary>
    /// An optional <c>--to</c> among the product's render formats; when it is omitted, the
    /// <c>--out</c> extension names the format, and otherwise <paramref name="defaultFormat"/>.
    /// </summary>
    /// <param name="description">Help for <c>--to</c>.</param>
    /// <param name="formats">The product's format declarations; those with the render use are offered.</param>
    /// <param name="defaultFormat">The render format id used when nothing else names one.</param>
    public static TargetFormat Render(string description, IReadOnlyList<FormatDescriptor> formats, string defaultFormat = "png") =>
        new(FormatUse.Render, description, formats, defaultFormat);
}

/// <summary>A document a command reads.</summary>
/// <param name="Description">Help for the document argument.</param>
/// <param name="PasswordSubject">The document as the password help names it, such as "the report".</param>
/// <param name="Argument">
/// The argument name. With a second document, each document's password options are named
/// after it (<c>--left-password</c>); a single document uses <c>--password</c>.
/// </param>
public sealed record InputDocument(
    string Description,
    string PasswordSubject,
    string Argument = "file");

/// <summary>Where a command publishes its result.</summary>
public sealed class OutputTarget
{
    private OutputTarget(OutputKind kind, string? fileDescription, string? directoryDescription, bool required)
    {
        Kind = kind;
        FileDescription = fileDescription;
        DirectoryDescription = directoryDescription;
        Required = required;
    }

    internal OutputKind Kind { get; }

    internal string? FileDescription { get; }

    internal string? DirectoryDescription { get; }

    internal bool Required { get; }

    /// <summary>
    /// One file named by <c>--out</c>, beside <c>--overwrite</c>. Unless it is required, an
    /// omitted <c>--out</c> is derived from the input.
    /// </summary>
    public static OutputTarget File(string description, bool required = false) =>
        new(OutputKind.File, Help(description), null, required);

    /// <summary>A set of files in the directory named by the required <c>--out-dir</c>, beside <c>--overwrite</c>.</summary>
    public static OutputTarget Directory(string description) =>
        new(OutputKind.Directory, null, Help(description), required: true);

    /// <summary>
    /// Either a set of files in the directory named by <c>--out-dir</c> or one file named by
    /// <c>--out</c> beside <c>--overwrite</c>; both are optional and the command picks the mode.
    /// </summary>
    public static OutputTarget FileOrDirectory(string fileDescription, string directoryDescription) =>
        new(OutputKind.FileOrDirectory, Help(fileDescription), Help(directoryDescription), required: false);

    /// <summary>A new file named by the required <c>file</c> argument, beside <c>--overwrite</c>.</summary>
    public static OutputTarget CreatedFile(string description) =>
        new(OutputKind.CreatedFile, Help(description), null, required: true);

    /// <summary>
    /// The edited input document, published to <c>--out</c> (by default beside the input) or
    /// atomically in place with <c>--in-place</c> and an optional <c>--backup</c>.
    /// </summary>
    internal static OutputTarget Mutation { get; } = new(
        OutputKind.Mutation,
        "Output path. Default: the input path with '.out' inserted before the extension.",
        null,
        required: false);

    private static string Help(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return description;
    }
}

internal enum OutputKind { File, Directory, FileOrDirectory, CreatedFile, Mutation }

/// <summary>The password a command can put on its output.</summary>
/// <param name="Subject">The output as the password help names it, such as "the output report".</param>
/// <param name="ProtectableFormats">The output format ids that can carry a password.</param>
public sealed record EncryptedOutput(
    string Subject,
    IReadOnlyList<string> ProtectableFormats);

/// <summary>
/// The one mapping from <see cref="CommandTraits"/> to the common arguments and options. It
/// fixes their order: the document arguments first, then the target format, the command's own
/// parameters, the output, the input passwords, the output password and the font directories.
/// A new common option is added here, and every command that declares the trait gets it.
/// Product commands are built on it through <see cref="StandardCommand"/>, which runs them
/// through the host pipeline; only a host command that runs through its own pipeline builds
/// its command with <see cref="CreateCommand"/> and reads each invocation through
/// <see cref="Bind"/>, and analyzer <c>APCLI011</c> keeps products from doing so.
/// </summary>
public sealed class StandardOptions
{
    private const string CreatedFileArgument = "file";

    // The target format of every --to option, so a command can find the commands that write a format.
    private static readonly ConditionalWeakTable<Option, TargetFormat> Targets = new();

    /// <summary>Maps the traits to their common parameters.</summary>
    /// <exception cref="ArgumentException">The traits contradict each other.</exception>
    public StandardOptions(CommandTraits traits)
    {
        ArgumentNullException.ThrowIfNull(traits);
        if (traits.Other is not null && traits.Input is null)
        {
            throw new ArgumentException("A second input document needs a first one.", nameof(traits));
        }

        if (traits.Input is not null && traits.Output?.Kind == OutputKind.CreatedFile)
        {
            throw new ArgumentException("A command that names the file it creates reads no input document.", nameof(traits));
        }

        if (traits.Input is null && traits.Output?.Kind == OutputKind.Mutation)
        {
            throw new ArgumentException("A mutation publishes the input document it edits.", nameof(traits));
        }

        if (traits.Input is not null && traits.PasswordSubject is not null)
        {
            throw new ArgumentException("The input document brings its own password.", nameof(traits));
        }

        if (traits.PasswordSubject is { } subject)
        {
            InputPassword = new PasswordOptions(StandardOptionNames.Password, subject);
        }

        if (traits.Input is { } input)
        {
            Input = Argument(input);
            InputPassword = traits.Other is null
                ? new PasswordOptions(StandardOptionNames.Password, input.PasswordSubject)
                : PairedPassword(input);
        }

        if (traits.Other is { } other)
        {
            if (string.Equals(other.Argument, traits.Input!.Argument, StringComparison.Ordinal))
            {
                throw new ArgumentException("The two input documents need distinct argument names.", nameof(traits));
            }

            Other = Argument(other);
            OtherPassword = PairedPassword(other);
        }

        if (traits.Output is { } output)
        {
            if (output.DirectoryDescription is { } directory)
            {
                OutputDirectory = new OutputDirectoryOption(directory, output.Required);
            }

            if (output.Kind == OutputKind.CreatedFile)
            {
                CreatedFile = new Argument<string>(CreatedFileArgument)
                {
                    Description = output.FileDescription,
                }.WithInput(InputKind.None);
            }
            else if (output.FileDescription is { } file)
            {
                OutputFile = new OutputFileOption(file, output.Required);
            }

            Overwrite = new Option<bool>(StandardOptionNames.Overwrite)
            {
                Description = "Replace the output file if it already exists.",
            };
            if (output.Kind == OutputKind.Mutation)
            {
                InPlace = new Option<bool>(StandardOptionNames.InPlace)
                {
                    Description = "Modify the input file itself atomically.",
                };
                Backup = new Option<bool>(StandardOptionNames.Backup)
                {
                    Description = "Create a non-overwriting backup before an in-place write.",
                };
            }
        }

        if (traits.Encrypt is { } encrypt)
        {
            ArgumentNullException.ThrowIfNull(encrypt.ProtectableFormats);
            Encrypt = new PasswordOptions(StandardOptionNames.Encrypt, encrypt.Subject, allowStdin: false);
            ProtectableFormats = encrypt.ProtectableFormats;
        }

        Fonts = traits.UsesFonts ? new FontDirectoryOptions() : null;
        if (traits.Target is { } target)
        {
            if (OutputFile is null)
            {
                throw new ArgumentException("A command that chooses its output format publishes it through --out.", nameof(traits));
            }

            To = TargetOption(target);
            Target = target;
        }
    }

    internal Argument<string>? Input { get; }

    internal Argument<string>? Other { get; }

    internal Argument<string>? CreatedFile { get; }

    internal OutputFileOption? OutputFile { get; }

    internal OutputDirectoryOption? OutputDirectory { get; }

    internal Option<bool>? Overwrite { get; }

    internal Option<bool>? InPlace { get; }

    internal Option<bool>? Backup { get; }

    internal PasswordOptions? InputPassword { get; }

    internal PasswordOptions? OtherPassword { get; }

    internal PasswordOptions? Encrypt { get; }

    internal IReadOnlyList<string> ProtectableFormats { get; } = [];

    internal FontDirectoryOptions? Fonts { get; }

    internal Option<string>? To { get; }

    internal TargetFormat? Target { get; }

    /// <summary>
    /// Creates the command: the document arguments, the command's own arguments, then the
    /// target format, the command's own options in declaration order and the common options.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">The command help.</param>
    /// <param name="parameters">The command's own arguments and options, each kind in help order.</param>
    /// <exception cref="ArgumentException">An own option repeats a common option.</exception>
    public Command CreateCommand(string name, string description, IReadOnlyList<Symbol> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var command = new Command(name, description);
        AddArgument(command, Input);
        AddArgument(command, Other);
        AddArgument(command, CreatedFile);
        AddOption(command, To);
        foreach (Symbol parameter in parameters)
        {
            switch (parameter)
            {
                case Argument argument:
                    command.Arguments.Add(argument);
                    break;
                case Option option:
                    command.Options.Add(option);
                    break;
                default:
                    throw new ArgumentException($"'{parameter.Name}' is neither an argument nor an option.", nameof(parameters));
            }
        }

        OutputDirectory?.AddTo(command);
        OutputFile?.AddTo(command);
        AddOption(command, Overwrite);
        AddOption(command, InPlace);
        AddOption(command, Backup);
        InputPassword?.AddTo(command);
        OtherPassword?.AddTo(command);
        Encrypt?.AddTo(command);
        Fonts?.AddTo(command);
        if (command.Options
                .SelectMany(static option => option.Aliases.Prepend(option.Name))
                .GroupBy(static name => name, StringComparer.Ordinal)
                .FirstOrDefault(static names => names.Count() > 1) is { } repeated)
        {
            throw new ArgumentException(
                $"Option '{repeated.Key}' is declared more than once; the command template owns the common options.",
                nameof(parameters));
        }

        return command;
    }

    /// <summary>Reads one invocation of a command created by <see cref="CreateCommand"/>.</summary>
    /// <param name="parse">The parsed command line.</param>
    /// <param name="paths">Resolves paths against the invocation directory.</param>
    /// <param name="inputs">The invocation's bounded input reader.</param>
    /// <param name="readEnvironment">Reads a named environment variable.</param>
    public StandardInvocation Bind(
        ParseResult parse,
        PathResolver paths,
        InputSource inputs,
        Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        return new StandardInvocation(this, parse, paths, inputs, readEnvironment, standardInputAvailable: true);
    }

    private static void AddArgument(Command command, Argument? argument)
    {
        if (argument is not null)
        {
            command.Arguments.Add(argument);
        }
    }

    private static void AddOption(Command command, Option? option)
    {
        if (option is not null)
        {
            command.Options.Add(option);
        }
    }

    private static Argument<string> Argument(InputDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Argument);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Description);
        return new Argument<string>(document.Argument)
        {
            Description = document.Description,
        }.WithInput(InputKind.File);
    }

    private static Option<string> TargetOption(TargetFormat target)
    {
        IReadOnlyList<string> ids = target.Formats.IdsFor(target.Use);
        if (ids.Count == 0)
        {
            throw new ArgumentException($"The product declares no {target.Use.ToString().ToLowerInvariant()} format.", nameof(target));
        }

        if (target.Default is { } defaultFormat && !ids.Contains(defaultFormat, StringComparer.Ordinal))
        {
            throw new ArgumentException($"The default format '{defaultFormat}' is not a {target.Use.ToString().ToLowerInvariant()} format.", nameof(target));
        }

        string[] names = [.. ids.SelectMany(id => target.Formats.Named(target.Use, id)!.Aliases.Prepend(id))];
        var option = new Option<string>(StandardOptionNames.To)
        {
            Description = target.Description,
            Required = target.Default is null,
        }.WithInput(InputKind.None);
        if (target.Default is { } fallback)
        {
            option.DefaultValueFactory = _ => fallback;
        }

        option.CompletionSources.Add([.. ids]);
        option.Validators.Add(result =>
        {
            if (result.Tokens.Count > 0 && target.Formats.Named(target.Use, result.Tokens[^1].Value) is null)
            {
                result.AddError($"Argument '{result.Tokens[^1].Value}' not recognized. Must be one of: {string.Join(", ", names)}.");
            }
        });
        Targets.Add(option, target);
        return option;
    }

    /// <summary>The format a <c>--to</c> option created here chooses among, or null for any other option.</summary>
    internal static TargetFormat? TargetOf(Option option) =>
        Targets.TryGetValue(option, out TargetFormat? target) ? target : null;

    // Two documents cannot share standard input, so neither password reads it.
    private static PasswordOptions PairedPassword(InputDocument document) =>
        new($"--{document.Argument}-password", document.PasswordSubject, allowStdin: false);
}

/// <summary>
/// The common values of one command invocation. Each value is resolved when the handler first
/// reads it, so the handler decides the order: checks that need no input first, then the
/// documents, secrets and outputs. The rules every command shares are enforced here: an output,
/// whether named or derived from the input, never names a file that any of the command's file
/// parameters or file-backed JSON sources names (a bounded edit also refuses one that its
/// operations read), a password is refused for a format that cannot carry one before its secret
/// is read, and standard input already carrying data is never read for a password.
/// </summary>
public class StandardInvocation
{
    private readonly StandardOptions _options;
    private readonly ParseResult _parse;
    private readonly Lazy<string?> _inputPassword;
    private readonly Lazy<string?> _otherPassword;
    private string? _input;
    private string? _other;

    internal StandardInvocation(
        StandardOptions options,
        ParseResult parse,
        PathResolver paths,
        InputSource inputs,
        Func<string, string?> readEnvironment,
        bool standardInputAvailable)
    {
        _options = options;
        _parse = parse;
        Paths = paths;
        Inputs = inputs;
        ReadEnvironment = readEnvironment;
        _inputPassword = new(() => Declared(_options.InputPassword, "input document").Resolve(
            _parse, Inputs, ReadEnvironment, standardInputAvailable));
        _otherPassword = new(() => Declared(_options.OtherPassword, "second input document").Resolve(
            _parse, Inputs, ReadEnvironment));
    }

    /// <summary>
    /// Restates a password error about one of the two documents a command reads with that
    /// document's own password option, which the loader that raised it cannot know; returns
    /// any other error unchanged.
    /// </summary>
    internal CliException ForPairedInput(CliException error)
    {
        if (_options.Other is null || !CliErrors.IsPasswordError(error)
            || error.Details?["path"]?.GetValue<string>() is not { } path)
        {
            return error;
        }

        (Argument<string>? argument, PasswordOptions? password) =
            string.Equals(path, _input, StringComparison.Ordinal) ? (_options.Input, _options.InputPassword)
            : string.Equals(path, _other, StringComparison.Ordinal) ? (_options.Other, _options.OtherPassword)
            : (null, null);
        return argument is null || password is null
            ? error
            : CliErrors.ForInput(error, argument.Name, password.EnvironmentOption);
    }

    /// <summary>
    /// Restates <c>FORMAT_UNSUPPORTED</c> about the format that a named output's extension asks
    /// for, from a command that writes its output in that format rather than one chosen by
    /// <c>--to</c>, such as an edit: the error names the option or argument and what the command
    /// writes, and the hint the product's command that writes the format, found through the
    /// <c>--to</c> formats of the commands beside it. An extension that names no format of the
    /// product or that the input document shares, a format the command writes, and every other
    /// error, are returned unchanged.
    /// </summary>
    internal CliException ForOutputFormat(CliException error)
    {
        if (error.Code != ErrorCodes.FormatUnsupported || _options.Target is not null
            || error.Details?["requested"]?.GetValue<string>() is not { } requested
            || error.Details["supported"] is not JsonArray supportedIds
            || NamedOutput() is not var (parameter, output)
            || Path.GetExtension(output) is not { Length: > 1 } extension
            // An input of the same extension could be the file the error is about.
            || (_options.Input is { } input
                && string.Equals(Path.GetExtension(_parse.GetValue(input)), extension, StringComparison.OrdinalIgnoreCase))
            || _parse.CommandResult.Parent is not CommandResult parent)
        {
            return error;
        }

        (string Command, TargetFormat Target)[] siblings =
        [
            .. parent.Command.Subcommands
                .Where(command => !command.Hidden && command != _parse.CommandResult.Command)
                .SelectMany(static command => command.Options
                    .Select(StandardOptions.TargetOf)
                    .OfType<TargetFormat>()
                    .Select(target => (command.Name, target))),
        ];
        FormatDescriptor[] formats = [.. siblings.SelectMany(static sibling => sibling.Target.Formats).Distinct()];
        FormatDescriptor[] named = [.. formats.Where(format => format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))];
        bool namesRequested = string.Equals(extension[1..], requested, StringComparison.OrdinalIgnoreCase)
            || named.Any(format => string.Equals(format.Id, requested, StringComparison.OrdinalIgnoreCase));
        string[] supported = [.. supportedIds.Select(static id => id!.GetValue<string>())];
        // A command that writes the requested format, or the one the extension names, raised the
        // error about another file, such as one an operation reads.
        bool writesIt = supported.Any(id => string.Equals(id, requested, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, extension[1..], StringComparison.OrdinalIgnoreCase)
            || named.Any(format => string.Equals(format.Id, id, StringComparison.OrdinalIgnoreCase)));
        if (named.Length == 0 || !namesRequested || writesIt)
        {
            return error;
        }

        string[] path = CommandPath();
        string? producer = siblings
            .Select(sibling => sibling.Target.Formats.WithExtension(sibling.Target.Use, extension) is [var format, ..]
                ? string.Join(' ', [DistributionInfo.CommandName, .. path[..^1], sibling.Command, "<that file>", "--to", format.Id])
                : null)
            .FirstOrDefault(static line => line is not null);
        return CliErrors.OutputFormatUnsupported(
            parameter, output, string.Join(' ', path), requested, supported,
            [.. supported.Select(ExtensionOf).Distinct(StringComparer.OrdinalIgnoreCase)], producer);

        string ExtensionOf(string id) =>
            formats.FirstOrDefault(format => string.Equals(format.Id, id, StringComparison.OrdinalIgnoreCase)) is { } format
                ? format.OutputExtension ?? format.Extensions.FirstOrDefault() ?? "." + id
                : "." + id;
    }

    /// <summary>The parameter naming the output file and the value the caller gave it, or null when none was named.</summary>
    private (string Parameter, string Output)? NamedOutput() =>
        _options.CreatedFile is { } created && _parse.GetValue(created) is { Length: > 0 } file ? (created.Name, file)
        : _options.OutputFile is { } outputFile && _parse.GetValue(outputFile.Option) is { Length: > 0 } named ? (StandardOptionNames.Out, named)
        : null;

    /// <summary>The names of the invoked command and the commands it is under, below the root.</summary>
    private string[] CommandPath()
    {
        var path = new List<string>();
        for (CommandResult? command = _parse.CommandResult; command?.Parent is not null; command = command.Parent as CommandResult)
        {
            path.Add(command.Command.Name);
        }

        path.Reverse();
        return [.. path];
    }

    /// <summary>The resolved input document.</summary>
    /// <exception cref="CliException">The file does not exist.</exception>
    public string Input => _input ??= ResolveDocument(Declared(_options.Input, "input document"));

    /// <summary>The resolved second input document.</summary>
    /// <exception cref="CliException">The file does not exist.</exception>
    public string Other => _other ??= ResolveDocument(Declared(_options.Other, "second input document"));

    /// <summary>
    /// The password for <see cref="Input"/>, or for the command's own input files under
    /// <see cref="CommandTraits.PasswordSubject"/>; null when none was given.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a conflicting, empty or unavailable source.</exception>
    public string? InputPassword => _inputPassword.Value;

    /// <summary>
    /// Starts the command that continues this invocation for a result's <c>window.next</c>: the
    /// same command path and input, and the input password's environment variable when it came
    /// from one (the variable's name is not a secret). The product appends its own options. A
    /// password given literally or on stdin is not repeated, so the caller supplies it again.
    /// </summary>
    public ContinuationCommand Continuation()
    {
        ContinuationCommand next = new ContinuationCommand(CommandPath()).Argument(Input);
        if (_options.InputPassword is { } password && password.EnvironmentName(_parse) is { } variable)
        {
            next.Option(password.EnvironmentOption, variable);
        }

        return next;
    }

    /// <summary>The password for <see cref="Other"/>, or null when none was given.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a conflicting, empty or missing source.</exception>
    public string? OtherPassword => _otherPassword.Value;

    /// <summary>
    /// The resolved file named by one of the command's own <see cref="InputKind.File"/> options,
    /// or null when it was omitted.
    /// </summary>
    /// <exception cref="CliException">The file does not exist.</exception>
    public string? InputFile(Option<string?> option) => InputFileValues(option).SingleOrDefault();

    /// <summary>The resolved file named by one of the command's own required <see cref="InputKind.File"/> options.</summary>
    /// <exception cref="CliException">The file does not exist.</exception>
    public string RequiredInputFile(Option<string> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return option.Required
            ? InputFileValues(option).Single()
            : throw new InvalidOperationException($"'{option.Name}' is optional; read it with {nameof(InputFile)}.");
    }

    /// <summary>The resolved files named by one of the command's own <see cref="InputKind.File"/> options.</summary>
    /// <exception cref="CliException">A file does not exist.</exception>
    public string[] InputFiles(Option<string[]> option) => InputFileValues(option);

    /// <summary>The resolved files named by one of the command's own <see cref="InputKind.File"/> arguments.</summary>
    /// <exception cref="CliException">A file does not exist.</exception>
    public string[] InputFiles(Argument<string[]> argument) => InputFileValues(argument);

    /// <summary>Whether an existing output may be replaced.</summary>
    public bool Overwrite => _parse.GetValue(Declared(_options.Overwrite, "output"));

    /// <summary>
    /// The output file named by <c>--out</c>, or the sibling of <see cref="Input"/> with
    /// <paramref name="targetExtension"/> when it was omitted.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the output names an input.</exception>
    public string OutputPath(string targetExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExtension);
        return RequestedOutputPath() ?? Derived(OutputFileOption.DerivePath(Input, targetExtension), inPlaceAvailable: false);
    }

    /// <summary>The output file named by a required <c>--out</c>.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when <c>--out</c> names an input.</exception>
    public string OutputPath()
    {
        OutputFileOption output = Declared(_options.OutputFile, "output file");
        return output.Required
            ? output.ResolveRequired(_parse, Paths, DeclaredInputs())
            : throw new InvalidOperationException("The command's --out is optional; derive the output with OutputPath(targetExtension).");
    }

    /// <summary>The output file named by <c>--out</c>, or null when it was omitted.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when <c>--out</c> names an input.</exception>
    public string? RequestedOutputPath() =>
        Declared(_options.OutputFile, "output file").Resolve(_parse, Paths, DeclaredInputs());

    /// <summary>
    /// The target format id that <c>--to</c> names by its id or an alias in any case. It must
    /// not contradict a format of the product that the <c>--out</c> extension names, so one
    /// format's bytes are never written under another format's extension; an alias extension
    /// of the same format such as .jpeg, and an extension that names no format of the product,
    /// are accepted. When a render command omits <c>--to</c>, the render format the
    /// <c>--out</c> extension names is used, so <c>--out page.svg</c> writes SVG, and otherwise
    /// the default; an <c>--out</c> extension that names no render format, such as a convert
    /// format's, is refused rather than given image bytes.
    /// </summary>
    /// <exception cref="CliException"><c>USAGE_ERROR</c> when a render <c>--out</c> extension names no render format, or <c>--to</c> and the extension name different formats.</exception>
    public string TargetFormat()
    {
        Option<string> to = Declared(_options.To, "target format");
        TargetFormat target = _options.Target!;
        string requested = _parse.GetRequiredValue(to);
        FormatDescriptor format = target.Formats.Named(target.Use, requested)!;
        string? extension = _options.OutputFile?.RequestedExtension(_parse);
        FormatDescriptor[] named = extension is null ? []
            : [.. target.Formats.Where(candidate => candidate.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))];
        if (target.Use == FormatUse.Render && extension is not null
            && !named.Any(static candidate => candidate.Uses.HasFlag(FormatUse.Render)))
        {
            string[] extensions = target.Formats
                .Where(static candidate => candidate.Uses.HasFlag(FormatUse.Render))
                .SelectMany(static candidate => candidate.Extensions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            throw CliErrors.Usage(
            [
                $"{StandardOptionNames.Out} '{_parse.GetValue(_options.OutputFile!.Option)}' has the {extension} extension, "
                    + $"which names no render format; use {string.Join(", ", extensions)}",
            ]);
        }

        if (target.Use == FormatUse.Render && _parse.GetResult(to) is not { Implicit: false })
        {
            return named.FirstOrDefault(static candidate => candidate.Uses.HasFlag(FormatUse.Render))?.Id ?? format.Id;
        }

        if (named.Length > 0 && !named.Contains(format))
        {
            throw CliErrors.Usage(
            [
                $"{to.Name} {requested} contradicts {StandardOptionNames.Out} '{_parse.GetValue(_options.OutputFile!.Option)}', "
                    + $"whose {extension} extension names {string.Join(" or ", named.Select(static match => match.Id))}; "
                    + $"give the output the {target.Formats.ExtensionFor(format.Id)} extension or change {to.Name}",
            ]);
        }

        return format.Id;
    }

    /// <summary>The file a creating command writes, named by its <c>file</c> argument.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the file is one of the inputs.</exception>
    public string CreatedPath
    {
        get
        {
            Argument<string> file = Declared(_options.CreatedFile, "created file");
            return OutputFileOption.ResolveExplicit(
                Paths, _parse.GetRequiredValue(file), file.Name, DeclaredInputs());
        }
    }

    /// <summary>
    /// The resolved <c>--out-dir</c>, which may not exist yet; a directory can never be an
    /// input file, since a file at the path is refused.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when it is missing or a file occupies the path.</exception>
    public string OutputDirectory =>
        Declared(_options.OutputDirectory, "output directory").ResolveRequired(_parse, Paths);

    /// <summary>The resolved <c>--out-dir</c>, or null when it was omitted.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when a file occupies the path.</exception>
    public string? RequestedOutputDirectory =>
        Declared(_options.OutputDirectory, "output directory").Resolve(_parse, Paths);

    /// <summary>
    /// The password for the output, or null when none was given. A password for a format that
    /// cannot carry one is refused, naming the option the caller passed, before the secret is read.
    /// </summary>
    /// <param name="format">The output format id.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for an unprotectable format or a bad source.</exception>
    public string? EncryptPassword(string format)
    {
        PasswordOptions encrypt = Declared(_options.Encrypt, "output password");
        encrypt.EnsureProtectable(_parse, format, _options.ProtectableFormats);
        return encrypt.Resolve(_parse, Inputs, ReadEnvironment);
    }

    /// <summary>The font directories named by <c>--font-dir</c>; ambient when none was given.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a directory that is not a local, existing one.</exception>
    public FontSearchProfile FontDirectories =>
        Declared(_options.Fonts, "font directories").Read(_parse, Paths);

    /// <summary>Reads a named environment variable through this invocation's input source.</summary>
    public Func<string, string?> ReadEnvironment { get; }

    internal PathResolver Paths { get; }

    internal InputSource Inputs { get; }

    private protected bool UsesFonts => _options.Fonts is not null;

    private protected void ResolveDocuments()
    {
        if (_options.Input is not null)
        {
            _ = Input;
        }

        if (_options.Other is not null)
        {
            _ = Other;
        }
    }

    /// <summary>
    /// The publication target of a mutation: <c>--in-place</c> replaces <see cref="Input"/>,
    /// with a backup only when <c>--backup</c> is given; otherwise the output is <c>--out</c>
    /// or the input path with '.out' inserted before its extension.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a contradictory combination or an output that names an input.</exception>
    internal MutationTarget MutationTarget()
    {
        Option<bool> inPlaceOption = Declared(_options.InPlace, "mutation output");
        string? requested = _parse.GetValue(_options.OutputFile!.Option);
        bool inPlace = _parse.GetValue(inPlaceOption);
        bool backup = _parse.GetValue(_options.Backup!);
        if (requested is not null && inPlace)
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.InPlace,
                $"cannot be combined with {StandardOptionNames.Out}",
                $"Choose {StandardOptionNames.Out} or {StandardOptionNames.InPlace}.");
        }

        if (backup && !inPlace)
        {
            throw CliErrors.OptionInvalid(
                StandardOptionNames.Backup,
                $"a backup is only meaningful with {StandardOptionNames.InPlace}",
                $"Pass {StandardOptionNames.InPlace} or omit {StandardOptionNames.Backup}.");
        }

        if (inPlace)
        {
            return new MutationTarget(Input, Overwrite: true, InPlace: true, backup ? BackupPath(Input) : null);
        }

        string output = requested is null
            ? Derived(OutputFileOption.DerivePath(Input, Path.GetExtension(Input)), inPlaceAvailable: true)
            : OutputFileOption.ResolveExplicit(
                Paths, requested, StandardOptionNames.Out, inPlaceAvailable: true, DeclaredInputs());
        return new MutationTarget(output, Overwrite, InPlace: false, BackupPath: null);
    }

    // A derived output is refused like a named one: the caller moves it with --out.
    private string Derived(string output, bool inPlaceAvailable)
    {
        OutputFileOption.EnsureNotInput(output, StandardOptionNames.Out, inPlaceAvailable, DeclaredInputs());
        return output;
    }

    private static string BackupPath(string inputPath) =>
        Path.Combine(
            Path.GetDirectoryName(inputPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(inputPath)}.backup{Path.GetExtension(inputPath)}");

    private string ResolveDocument(Argument<string> argument) =>
        Paths.ResolveInput(_parse.GetRequiredValue(argument));

    private string[] InputFileValues(Symbol parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        Command command = _parse.CommandResult.Command;
        bool declared = parameter switch
        {
            Argument argument => command.Arguments.Contains(argument),
            Option option => command.Options.Contains(option),
            _ => false,
        };
        if (!declared || parameter.GetParameterMetadata().InputKind != InputKind.File)
        {
            throw new InvalidOperationException($"'{parameter.Name}' is not an input file parameter of the command.");
        }

        return [.. Values(parameter).OfType<string>().Select(Paths.ResolveInput)];
    }

    // Every file named by a parameter the command declares as InputKind.File, and every JSON
    // source read from a file, whoever owns the parameter.
    private string[] DeclaredInputs()
    {
        Command command = _parse.CommandResult.Command;
        return
        [
            .. command.Arguments.Cast<Symbol>().Concat(command.Options)
                .SelectMany(symbol => symbol.GetParameterMetadata().InputKind switch
                {
                    InputKind.File => Values(symbol),
                    InputKind.JsonSource => Values(symbol).Where(value =>
                        !string.IsNullOrWhiteSpace(value)
                        && JsonInputSource.Classify(value, symbol.Name) == JsonSourceKind.File),
                    _ => [],
                })
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Paths.ResolveOutput(value!)),
        ];
    }

    private IEnumerable<string?> Values(Symbol symbol) => symbol switch
    {
        Argument<string> argument => new string?[] { _parse.GetValue(argument) },
        Argument<string[]> argument => _parse.GetValue(argument) ?? [],
        Option<string> option => new string?[] { _parse.GetValue(option) },
        Option<string[]> option => _parse.GetValue(option) ?? [],
        _ => throw new InvalidOperationException($"File parameter '{symbol.Name}' has an unsupported type."),
    };

    private static T Declared<T>(T? value, string trait)
        where T : class =>
        value ?? throw new InvalidOperationException($"The command declares no {trait}.");
}

/// <summary>
/// The common values of one product command invocation and the product engine. Disposing the
/// invocation ends the font scope that <see cref="OpenEngine"/> entered.
/// </summary>
/// <typeparam name="TPort">The product's typed port.</typeparam>
public sealed class StandardInvocation<TPort> : StandardInvocation, IDisposable
    where TPort : class
{
    private readonly ProductCommandContext<TPort> _context;
    private IDisposable? _fontScope;

    internal StandardInvocation(
        StandardOptions options,
        ParseResult parse,
        ProductCommandContext<TPort> context,
        bool standardInputAvailable)
        : base(options, parse, context.Paths, context.Inputs, context.ReadEnvironment, standardInputAvailable)
    {
        _context = context;
    }

    /// <summary>
    /// Opens the product engine: resolves the input documents and, for a command that uses
    /// fonts, applies <c>--font-dir</c> to the engine until the invocation ends, so the engine
    /// never opens a document before its fonts are in place. Run every check that needs no
    /// input before calling it.
    /// </summary>
    /// <exception cref="CliException">An input document does not exist, or a font directory is invalid.</exception>
    public TPort OpenEngine()
    {
        ResolveDocuments();
        if (UsesFonts && _fontScope is null)
        {
            _fontScope = _context.Binding.UseFonts(FontDirectories);
        }

        return _context.Port;
    }

    /// <summary>Ends the font scope entered by <see cref="OpenEngine"/>.</summary>
    public void Dispose()
    {
        _fontScope?.Dispose();
        _fontScope = null;
    }
}
