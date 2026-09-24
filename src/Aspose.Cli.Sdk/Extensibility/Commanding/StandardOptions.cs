using System.CommandLine;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// What a product command does, in the terms that decide its common parameters. The product
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
    /// parameters, such as the list of documents a merge reads; <see cref="Input"/> brings
    /// its own password instead.
    /// </summary>
    public string? PasswordSubject { get; init; }

    /// <summary>Where the command publishes its result.</summary>
    public OutputTarget? Output { get; init; }

    /// <summary>The password the command can put on its output (<c>--encrypt</c>).</summary>
    public EncryptedOutput? Encrypt { get; init; }

    /// <summary>Whether the result depends on the fonts available to the engine (<c>--font-dir</c>).</summary>
    public bool UsesFonts { get; init; }
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

    private static string Help(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return description;
    }
}

internal enum OutputKind { File, Directory, FileOrDirectory, CreatedFile }

/// <summary>The password a command can put on its output.</summary>
/// <param name="Subject">The output as the password help names it, such as "the output report".</param>
/// <param name="ProtectableFormats">The output format ids that can carry a password.</param>
public sealed record EncryptedOutput(
    string Subject,
    IReadOnlyList<string> ProtectableFormats);

/// <summary>
/// The one mapping from <see cref="CommandTraits"/> to the common option owners. It fixes their
/// order: the document arguments first, then, after the product's own parameters, the output,
/// the input passwords, the output password and the font directories. A new common option is
/// added here, and every command that declares the trait gets it.
/// </summary>
internal sealed class StandardOptions
{
    private const string CreatedFileArgument = "file";

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
                OutputFile = new OutputFileOptions(file, output.Required);
            }

            Overwrite = OutputFile is null ? OutputOptions.Overwrite() : null;
        }

        if (traits.Encrypt is { } encrypt)
        {
            ArgumentNullException.ThrowIfNull(encrypt.ProtectableFormats);
            Encrypt = new PasswordOptions(StandardOptionNames.Encrypt, encrypt.Subject, allowStdin: false);
            ProtectableFormats = encrypt.ProtectableFormats;
        }

        Fonts = traits.UsesFonts ? new FontDirectoryOptions() : null;
    }

    internal Argument<string>? Input { get; }

    internal Argument<string>? Other { get; }

    internal Argument<string>? CreatedFile { get; }

    internal OutputFileOptions? OutputFile { get; }

    internal OutputDirectoryOption? OutputDirectory { get; }

    /// <summary>The bare <c>--overwrite</c> of outputs without an <see cref="OutputFileOptions"/>, which owns its own.</summary>
    internal Option<bool>? Overwrite { get; }

    internal PasswordOptions? InputPassword { get; }

    internal PasswordOptions? OtherPassword { get; }

    internal PasswordOptions? Encrypt { get; }

    internal IReadOnlyList<string> ProtectableFormats { get; } = [];

    internal FontDirectoryOptions? Fonts { get; }

    /// <summary>Adds the document arguments, which precede every option.</summary>
    public void AddArguments(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Input is not null)
        {
            command.Arguments.Add(Input);
        }

        if (Other is not null)
        {
            command.Arguments.Add(Other);
        }

        if (CreatedFile is not null)
        {
            command.Arguments.Add(CreatedFile);
        }
    }

    /// <summary>Adds the common options, which follow the product's own options.</summary>
    public void AddOptions(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        OutputDirectory?.AddTo(command);
        OutputFile?.AddTo(command);
        if (Overwrite is not null)
        {
            command.Options.Add(Overwrite);
        }

        InputPassword?.AddTo(command);
        OtherPassword?.AddTo(command);
        Encrypt?.AddTo(command);
        Fonts?.AddTo(command);
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

    // Two documents cannot share standard input, so neither password reads it.
    private static PasswordOptions PairedPassword(InputDocument document) =>
        new($"--{document.Argument}-password", document.PasswordSubject, allowStdin: false);
}

/// <summary>
/// The common values of one command invocation. Each value is resolved when the handler first
/// reads it, so the handler decides the order: checks that need no input first, then the
/// documents, secrets and outputs. The rules every command shares are enforced here: an output
/// never names a file that any of the command's file parameters names, a password is refused for a format that cannot carry one before its
/// secret is read, and standard input already carrying data is never read for a password.
/// Disposing the invocation ends the font scope that <see cref="Port"/> entered.
/// </summary>
/// <typeparam name="TPort">The product's typed port.</typeparam>
public sealed class StandardInvocation<TPort> : IDisposable
    where TPort : class
{
    private readonly StandardOptions _options;
    private readonly ParseResult _parse;
    private readonly ProductCommandContext<TPort> _context;
    private readonly Lazy<string?> _inputPassword;
    private readonly Lazy<string?> _otherPassword;
    private string? _input;
    private string? _other;
    private IDisposable? _fontScope;

    internal StandardInvocation(
        StandardOptions options,
        ParseResult parse,
        ProductCommandContext<TPort> context,
        bool standardInputAvailable)
    {
        _options = options;
        _parse = parse;
        _context = context;
        _inputPassword = new(() => Declared(_options.InputPassword, "input document").Resolve(
            _parse, _context.Inputs, _context.ReadEnvironment, standardInputAvailable));
        _otherPassword = new(() => Declared(_options.OtherPassword, "second input document").Resolve(
            _parse, _context.Inputs, _context.ReadEnvironment));
    }

    /// <summary>Resolves the product's own path options against the invocation directory.</summary>
    public PathResolver Paths => _context.Paths;

    /// <summary>
    /// The product engine. The first access resolves the input documents and, for a command
    /// that uses fonts, applies <c>--font-dir</c> to the engine before it opens anything.
    /// </summary>
    public TPort Port
    {
        get
        {
            ResolveDocuments();
            if (_options.Fonts is { } fonts && _fontScope is null)
            {
                _fontScope = fonts.Use(_parse, _context);
            }

            return _context.Port;
        }
    }

    /// <summary>The resolved input document.</summary>
    /// <exception cref="Errors.CliException">The file does not exist.</exception>
    public string Input => _input ??= ResolveDocument(Declared(_options.Input, "input document"));

    /// <summary>The resolved second input document.</summary>
    /// <exception cref="Errors.CliException">The file does not exist.</exception>
    public string Other => _other ??= ResolveDocument(Declared(_options.Other, "second input document"));

    /// <summary>
    /// The password for <see cref="Input"/>, or for the command's own input files under
    /// <see cref="CommandTraits.PasswordSubject"/>; null when none was given.
    /// </summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> for a conflicting, empty or unavailable source.</exception>
    public string? InputPassword => _inputPassword.Value;

    /// <summary>The password for <see cref="Other"/>, or null when none was given.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> for a conflicting, empty or missing source.</exception>
    public string? OtherPassword => _otherPassword.Value;

    /// <summary>Whether an existing output may be replaced.</summary>
    public bool Overwrite =>
        _options.OutputFile?.Overwrite(_parse)
        ?? _parse.GetValue(Declared(_options.Overwrite, "output"));

    /// <summary>
    /// The output file named by <c>--out</c>, or the sibling of <see cref="Input"/> with
    /// <paramref name="targetExtension"/> when it was omitted.
    /// </summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when <c>--out</c> names an input.</exception>
    public string OutputPath(string targetExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExtension);
        return RequestedOutputPath() ?? OutputFileOptions.DerivePath(Input, targetExtension);
    }

    /// <summary>The output file named by a required <c>--out</c>.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when <c>--out</c> names an input.</exception>
    public string OutputPath()
    {
        OutputFileOptions output = Declared(_options.OutputFile, "output file");
        return output.Required
            ? output.ResolveRequired(_parse, _context.Paths, DeclaredInputs())
            : throw new InvalidOperationException("The command's --out is optional; derive the output with OutputPath(targetExtension).");
    }

    /// <summary>The output file named by <c>--out</c>, or null when it was omitted.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when <c>--out</c> names an input.</exception>
    public string? RequestedOutputPath() =>
        Declared(_options.OutputFile, "output file").Resolve(_parse, _context.Paths, DeclaredInputs());

    /// <summary>The extension of the file named by <c>--out</c>, such as <c>.svg</c>, or null.</summary>
    public string? RequestedOutputExtension =>
        Declared(_options.OutputFile, "output file").RequestedExtension(_parse);

    /// <summary>The file a creating command writes, named by its <c>file</c> argument.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when the file is one of the inputs.</exception>
    public string CreatedPath
    {
        get
        {
            Argument<string> file = Declared(_options.CreatedFile, "created file");
            return OutputFileOptions.ResolveExplicit(
                _context.Paths, _parse.GetRequiredValue(file), file.Name, DeclaredInputs());
        }
    }

    /// <summary>
    /// The resolved <c>--out-dir</c>, which may not exist yet; a directory can never be an
    /// input file, since a file at the path is refused.
    /// </summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when it is missing or a file occupies the path.</exception>
    public string OutputDirectory =>
        Declared(_options.OutputDirectory, "output directory").ResolveRequired(_parse, _context.Paths);

    /// <summary>The resolved <c>--out-dir</c>, or null when it was omitted.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when a file occupies the path.</exception>
    public string? RequestedOutputDirectory =>
        Declared(_options.OutputDirectory, "output directory").Resolve(_parse, _context.Paths);

    /// <summary>
    /// The password for the output, or null when none was given. A password for a format that
    /// cannot carry one is refused, naming the option the caller passed, before the secret is read.
    /// </summary>
    /// <param name="format">The output format id.</param>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> for an unprotectable format or a bad source.</exception>
    public string? EncryptPassword(string format)
    {
        PasswordOptions encrypt = Declared(_options.Encrypt, "output password");
        encrypt.EnsureProtectable(_parse, format, _options.ProtectableFormats);
        return encrypt.Resolve(_parse, _context.Inputs, _context.ReadEnvironment);
    }

    /// <summary>Reads a named environment variable through this invocation's input source.</summary>
    public Func<string, string?> ReadEnvironment => _context.ReadEnvironment;

    internal InputSource Inputs => _context.Inputs;

    /// <summary>Ends the font scope entered by <see cref="Port"/>.</summary>
    public void Dispose()
    {
        _fontScope?.Dispose();
        _fontScope = null;
    }

    private string ResolveDocument(Argument<string> argument) =>
        _context.Paths.ResolveInput(_parse.GetRequiredValue(argument));

    private void ResolveDocuments()
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

    // Every file named by a parameter the command declares as InputKind.File, whoever owns it.
    private string[] DeclaredInputs()
    {
        Command command = _parse.CommandResult.Command;
        return
        [
            .. command.Arguments.Cast<Symbol>().Concat(command.Options)
                .Where(static symbol => symbol.GetParameterMetadata().InputKind == InputKind.File)
                .SelectMany(Values)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(value => _context.Paths.ResolveOutput(value!)),
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
