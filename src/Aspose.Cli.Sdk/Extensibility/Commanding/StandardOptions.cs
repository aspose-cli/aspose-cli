using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Contracts;
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
/// The format a command writes, chosen by <c>--to</c> by a format id or alias in any case.
/// </summary>
public sealed class TargetFormat
{
    private TargetFormat(FormatUse? use, string description, IReadOnlyList<FormatDescriptor> formats, string? defaultFormat, bool required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(formats);
        Description = description;
        Offered = use is { } selected ? [.. formats.IdsFor(selected).Select(id => formats.Named(selected, id)!)] : formats;
        if (Offered.Count == 0)
        {
            throw new ArgumentException($"The product declares no {use.ToString()!.ToLowerInvariant()} format.", nameof(formats));
        }

        if (defaultFormat is not null && Named(defaultFormat) is null)
        {
            throw new ArgumentException($"The default format '{defaultFormat}' is not offered.", nameof(defaultFormat));
        }

        Default = defaultFormat;
        Required = required;
    }

    internal string Description { get; }

    /// <summary>The formats <c>--to</c> offers, in their declared order.</summary>
    internal IReadOnlyList<FormatDescriptor> Offered { get; }

    internal string? Default { get; }

    internal bool Required { get; }

    /// <summary>A required <c>--to</c> among the product's convert formats.</summary>
    /// <param name="description">Help for <c>--to</c>.</param>
    /// <param name="formats">The product's format declarations; those with the convert use are offered.</param>
    public static TargetFormat Convert(string description, IReadOnlyList<FormatDescriptor> formats) =>
        new(FormatUse.Convert, description, formats, defaultFormat: null, required: true);

    /// <summary>
    /// An optional <c>--to</c> among the product's render formats; when it is omitted, the
    /// <c>--out</c> extension names the format, and otherwise <paramref name="defaultFormat"/>.
    /// Its help is the standard wording, such as <c>Image format: png, jpeg or svg. Default: png.</c>
    /// </summary>
    /// <param name="formats">The product's format declarations; those with the render use are offered.</param>
    /// <param name="defaultFormat">The render format id used when nothing else names one.</param>
    public static TargetFormat Render(IReadOnlyList<FormatDescriptor> formats, string defaultFormat = "png")
    {
        ArgumentNullException.ThrowIfNull(formats);
        IReadOnlyList<string> ids = [.. formats.IdsFor(FormatUse.Render)];
        string listed = ids.Count < 2 ? string.Concat(ids) : $"{string.Join(", ", ids.Take(ids.Count - 1))} or {ids[^1]}";
        return new(FormatUse.Render, $"Image format: {listed}. Default: {defaultFormat}.", formats, defaultFormat, required: false);
    }

    /// <summary>
    /// An optional <c>--to</c> among <paramref name="formats"/>, such as the formats a command
    /// exports data in; when it is omitted, the <c>--out</c> extension names the format.
    /// </summary>
    public static TargetFormat Among(string description, IReadOnlyList<FormatDescriptor> formats) =>
        new(use: null, description, formats, defaultFormat: null, required: false);

    /// <summary>The offered format whose id or alias is <paramref name="name"/>, ignoring case.</summary>
    internal FormatDescriptor? Named(string name) =>
        Offered.FirstOrDefault(format => string.Equals(format.Id, name, StringComparison.OrdinalIgnoreCase)
            || format.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
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

/// <summary>
/// Where a command publishes its result and the formats it can write there. The command
/// template resolves the output once per invocation (<see cref="StandardInvocation.Output"/>):
/// the format <c>--to</c> names when the command has one, else the format among those it writes
/// that the named output's extension declares, else the edited input's format or the
/// <c>--to</c> default; the output's extension must belong to that format.
/// </summary>
public sealed class OutputTarget
{
    private OutputTarget(
        OutputKind kind,
        string? fileDescription,
        string? directoryDescription,
        bool required,
        IReadOnlyList<FormatDescriptor>? writes = null,
        string? derivedMarker = null)
    {
        if (writes is { Count: 0 })
        {
            throw new ArgumentException("An output writes at least one format.", nameof(writes));
        }

        Kind = kind;
        FileDescription = fileDescription;
        DirectoryDescription = directoryDescription;
        Required = required;
        Writes = writes;
        DerivedMarker = derivedMarker;
    }

    internal OutputKind Kind { get; }

    internal string? FileDescription { get; }

    internal string? DirectoryDescription { get; }

    internal bool Required { get; }

    /// <summary>The formats the command writes, or null when its <c>--to</c> chooses among them.</summary>
    internal IReadOnlyList<FormatDescriptor>? Writes { get; }

    /// <summary>What a derived output inserts before the extension, such as <c>.signed</c>.</summary>
    internal string? DerivedMarker { get; }

    /// <summary>
    /// One file named by <c>--out</c>, beside <c>--overwrite</c>, in the format the command's
    /// <c>--to</c> chooses. Unless it is required, an omitted <c>--out</c> is derived from the input.
    /// </summary>
    public static OutputTarget File(string description, bool required = false) =>
        new(OutputKind.File, Help(description), null, required);

    /// <summary>
    /// One file named by <c>--out</c>, beside <c>--overwrite</c>, in the format among
    /// <paramref name="writes"/> its extension declares. Unless it is required, an omitted
    /// <c>--out</c> is the input path with <paramref name="derivedMarker"/> and the extension of
    /// the first format inserted.
    /// </summary>
    public static OutputTarget File(string description, IReadOnlyList<FormatDescriptor> writes, bool required = false, string? derivedMarker = null) =>
        new(OutputKind.File, Help(description), null, required, Declared(writes), derivedMarker);

    /// <summary>A set of files in the directory named by the required <c>--out-dir</c>, beside <c>--overwrite</c>.</summary>
    public static OutputTarget Directory(string description) =>
        new(OutputKind.Directory, null, Help(description), required: true);

    /// <summary>
    /// Either a set of files in the directory named by <c>--out-dir</c> or one file named by
    /// <c>--out</c> beside <c>--overwrite</c>, in the format the command's <c>--to</c> chooses;
    /// both are optional and the command picks the mode.
    /// </summary>
    public static OutputTarget FileOrDirectory(string fileDescription, string directoryDescription) =>
        new(OutputKind.FileOrDirectory, Help(fileDescription), Help(directoryDescription), required: false);

    /// <summary>
    /// A new file named by the required <c>file</c> argument, beside <c>--overwrite</c>, in the
    /// format among <paramref name="writes"/> its extension declares.
    /// </summary>
    public static OutputTarget CreatedFile(string description, IReadOnlyList<FormatDescriptor> writes) =>
        new(OutputKind.CreatedFile, Help(description), null, required: true, Declared(writes));

    /// <summary>
    /// The edited input document, published to <c>--out</c> (by default beside the input) or
    /// atomically in place with <c>--in-place</c> and an optional <c>--backup</c>, in the format
    /// among <paramref name="writes"/> the output's extension declares.
    /// </summary>
    internal static OutputTarget Mutation(IReadOnlyList<FormatDescriptor> writes) => new(
        OutputKind.Mutation,
        "Output path. Default: the input path with '.out' inserted before the extension.",
        null,
        required: false,
        Declared(writes));

    private static string Help(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return description;
    }

    private static IReadOnlyList<FormatDescriptor> Declared(IReadOnlyList<FormatDescriptor> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return writes;
    }
}

internal enum OutputKind { File, Directory, FileOrDirectory, CreatedFile, Mutation }

/// <summary>The password a command can put on its output.</summary>
/// <remarks>
/// The resolved output's format decides whether it can carry one
/// (<see cref="FormatDescriptor.Protectable"/>).
/// </remarks>
/// <param name="Subject">The output as the password help names it, such as "the output report".</param>
public sealed record EncryptedOutput(string Subject);

/// <summary>
/// The one mapping from <see cref="CommandTraits"/> to the common arguments and options. It
/// fixes their order: the document arguments first, then the target format, the command's own
/// parameters, the output, the input passwords, the output password and the font directories.
/// A new common option is added here, and every command that declares the trait gets it.
/// Product commands are built on it through <see cref="CommandDefinition{TRequest, TResult}"/>, which
/// runs them through the host pipeline; only a host command that runs through its own pipeline builds
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
            Encrypt = new PasswordOptions(StandardOptionNames.Encrypt, encrypt.Subject, allowStdin: false);
        }

        OutputTarget = traits.Output;
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
        else if (traits.Output is { Kind: not OutputKind.Directory, Writes: null })
        {
            throw new ArgumentException("An output file without --to declares the formats it is written in.", nameof(traits));
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

    internal FontDirectoryOptions? Fonts { get; }

    internal Option<string>? To { get; }

    internal TargetFormat? Target { get; }

    internal OutputTarget? OutputTarget { get; }

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
        var option = new Option<string>(StandardOptionNames.To)
        {
            Description = target.Description,
            Required = target.Required,
        }.WithInput(InputKind.None);
        if (target.Default is { } fallback)
        {
            option.DefaultValueFactory = _ => fallback;
        }

        option.CompletionSources.Add([.. target.Offered.Select(static format => format.Id)]);
        string names = string.Join(", ", target.Offered.SelectMany(static format => format.Aliases.Prepend(format.Id)));
        option.Validators.Add(result =>
        {
            if (result.Tokens.Count > 0 && target.Named(result.Tokens[^1].Value) is null)
            {
                result.AddError($"Argument '{result.Tokens[^1].Value}' not recognized. Must be one of: {names}.");
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
public partial class StandardInvocation
{
    private readonly StandardOptions _options;
    private readonly ParseResult _parse;
    private readonly Lazy<Secret?> _inputPassword;
    private readonly Lazy<Secret?> _otherPassword;
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
    public Secret? InputPassword => _inputPassword.Value;

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
    public Secret? OtherPassword => _otherPassword.Value;

    /// <summary>
    /// The password one of the command's own <see cref="PasswordOptions.RequiredEnvironment"/>
    /// parameters names, read from its environment variable.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the variable is missing or empty.</exception>
    public Secret Password(PasswordOptions password)
    {
        ArgumentNullException.ThrowIfNull(password);
        Command command = _parse.CommandResult.Command;
        if (!password.Options.All(command.Options.Contains))
        {
            throw new InvalidOperationException("The password options are not parameters of the command.");
        }

        return password.Resolve(_parse, Inputs, ReadEnvironment)
            ?? throw new InvalidOperationException("A required password option resolved no password.");
    }

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

    /// <summary>The output file named by <c>--out</c>, or null when it was omitted.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when <c>--out</c> names an input.</exception>
    public string? RequestedOutputPath() =>
        Declared(_options.OutputFile, "output file").Resolve(_parse, Paths, DeclaredInputs());

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

    /// <summary>The resolved <c>--out-dir</c>, or null when it was omitted.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when a file occupies the path.</exception>
    public string? RequestedOutputDirectory =>
        Declared(_options.OutputDirectory, "output directory").Resolve(_parse, Paths);

    /// <summary>The font directories named by <c>--font-dir</c>; ambient when none was given.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a directory that is not a local, existing one.</exception>
    public FontSearchProfile FontDirectories =>
        Declared(_options.Fonts, "font directories").Read(_parse, Paths);

    /// <summary>Reads a named environment variable through this invocation's input source.</summary>
    internal Func<string, string?> ReadEnvironment { get; }

    internal PathResolver Paths { get; }

    internal InputSource Inputs { get; }

    /// <summary>The product's content format detector, which a menu edit command supplies; null otherwise.</summary>
    internal Func<string, string?>? DetectFormat { get; init; }

    internal bool UsesFonts => _options.Fonts is not null;

    /// <summary>Resolves the input documents the command declares, so a password error names its input.</summary>
    internal void ResolveDocuments()
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
    /// The output of a mutation in <paramref name="format"/>: <c>--in-place</c> replaces
    /// <see cref="Input"/>, with a backup only when <c>--backup</c> is given; otherwise the output
    /// is <c>--out</c> or the input path with '.out' inserted before its extension.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a contradictory combination or an output that names an input.</exception>
    private ResolvedOutput Mutated(FormatDescriptor format, IReadOnlyList<FormatDescriptor> alternatives)
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
            return new ResolvedOutput(format, Input, overwrite: true, inPlace: true, backup ? BackupPath(Input) : null, alternatives);
        }

        string output = requested is null
            ? Derived(OutputFileOption.DerivePath(Input, Path.GetExtension(Input)), inPlaceAvailable: true)
            : OutputFileOption.ResolveExplicit(
                Paths, requested, StandardOptionNames.Out, inPlaceAvailable: true, DeclaredInputs());
        return new ResolvedOutput(format, output, Overwrite, inPlace: false, backupPath: null, alternatives);
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
