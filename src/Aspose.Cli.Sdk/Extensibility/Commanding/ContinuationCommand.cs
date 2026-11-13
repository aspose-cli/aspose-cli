using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Builds the ready-to-run follow-up command a bounded read advertises in its <c>next</c>
/// field. The command names this executable, quotes every value that a POSIX shell,
/// PowerShell or cmd would otherwise split or expand, and always asks for JSON, so an
/// agent can run it verbatim instead of computing the next window itself. It names what it
/// writes only through <see cref="Out"/> and <see cref="OutDir"/>, which give
/// <c>--overwrite</c> by the rule of <see cref="ContinuationOutput"/>.
/// </summary>
public sealed class ContinuationCommand
{
    private readonly List<string> _tokens = [DistributionInfo.CommandName];

    /// <summary>Starts a command from its path, such as <c>product query parts</c>.</summary>
    public ContinuationCommand(params string[] commandPath)
    {
        ArgumentNullException.ThrowIfNull(commandPath);
        if (commandPath.Length == 0)
        {
            throw new ArgumentException("A continuation needs a command path.", nameof(commandPath));
        }

        foreach (string segment in commandPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(segment);
            _tokens.Add(segment);
        }
    }

    /// <summary>Appends a positional argument.</summary>
    public ContinuationCommand Argument(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _tokens.Add(CommandLineText.Quote(value));
        return this;
    }

    /// <summary>
    /// Appends a placeholder the caller replaces, such as <c>&lt;that file&gt;</c> for
    /// <paramref name="name"/> <c>that file</c>, as written: lower-case words a hint's reader, and
    /// its check, fill in.
    /// </summary>
    public ContinuationCommand Placeholder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!name.All(static character => char.IsAsciiLetterLower(character) || character is ' ' or '-'))
        {
            throw new ArgumentException("A placeholder is lower-case words.", nameof(name));
        }

        _tokens.Add($"<{name}>");
        return this;
    }

    /// <summary>Appends an option with a text value.</summary>
    public ContinuationCommand Option(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _tokens.Add(name);
        _tokens.Add(CommandLineText.Quote(value));
        return this;
    }

    /// <summary>Appends an option with an integer value.</summary>
    public ContinuationCommand Option(string name, long value) =>
        Option(name, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Appends an option whose value is a JSON document, such as an operation document, in
    /// single quotes, which a POSIX shell and PowerShell both pass as written, as they do not the
    /// escaped double quotes of any other value; a document that holds a single quote or a
    /// backtick is quoted as any other value is.
    /// </summary>
    public ContinuationCommand Document(string name, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(json);
        if (json.Contains('\'', StringComparison.Ordinal) || json.Contains('`', StringComparison.Ordinal))
        {
            return Option(name, json);
        }

        _tokens.Add(name);
        _tokens.Add($"'{json}'");
        return this;
    }

    /// <summary>
    /// The placeholder a command names for the environment variable of a password the invocation
    /// read some other way, or set none for, which the caller replaces with a variable that holds it.
    /// </summary>
    internal const string PasswordPlaceholder = "<password-env>";

    /// <summary>
    /// Appends <paramref name="option"/>, which names the environment variable of a password, with
    /// <paramref name="variable"/>, or, for a password the invocation did not read from a variable,
    /// with <see cref="PasswordPlaceholder"/> in double quotes, which a shell passes as written
    /// rather than reading its angle brackets as a redirection: a command never repeats a password
    /// itself, nor reads one from stdin, which a document the command pipes there may need.
    /// </summary>
    internal ContinuationCommand PasswordVariable(string option, string? variable) =>
        Option(option, variable ?? PasswordPlaceholder);

    /// <summary>Appends <c>--out</c> naming the file the command writes, with <c>--overwrite</c> when it replaces what is there (<see cref="ContinuationOutput"/>).</summary>
    public ContinuationCommand Out(ContinuationOutput output) => Writing(StandardOptionNames.Out, output);

    /// <summary>Appends <c>--out-dir</c> naming the directory the command writes in, with <c>--overwrite</c> when it replaces what is there (<see cref="ContinuationOutput"/>).</summary>
    public ContinuationCommand OutDir(ContinuationOutput output) => Writing(StandardOptionNames.OutDir, output);

    /// <summary>
    /// Appends <c>--overwrite</c> when the command replaces <paramref name="output"/>, which its
    /// command line names already, as a repeated command line keeps the output its invocation named.
    /// </summary>
    internal ContinuationCommand Overwriting(ContinuationOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return Flag(StandardOptionNames.Overwrite, output.Replaces);
    }

    private ContinuationCommand Writing(string option, ContinuationOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return Option(option, output.Path).Overwriting(output);
    }

    /// <summary>Appends a switch when <paramref name="enabled"/> is true.</summary>
    public ContinuationCommand Flag(string name, bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (enabled)
        {
            _tokens.Add(name);
        }

        return this;
    }

    /// <summary>
    /// Appends each option given on <paramref name="parse"/>'s command line, every value as given,
    /// that <paramref name="command"/>, run instead to write <paramref name="format"/>, takes by the
    /// same name, but <paramref name="except"/>, the input's password and those it leaves out (<see cref="LeftOut"/>).
    /// </summary>
    public ContinuationCommand Carrying(ParseResult parse, Command command, string format, params string[] except)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(command);
        foreach (OptionResult given in parse.CommandResult.Children.OfType<OptionResult>().Where(given => !given.Implicit
            && !except.Contains(given.Option.Name) && command.Options.Any(option => option.Name == given.Option.Name)
            && given.Option.GetParameterMetadata() is { ValueSource: ParameterValueSource.CommandLine, Secret: false }))
        {
            if (!command.Options.First(option => option.Name == given.Option.Name).GetParameterMetadata().AppliesTo(format))
            {
                LeftOut += $" It leaves out {given.Option.Name}: a '{format}' output holds the whole document.";
                continue;
            }

            _ = given.Tokens.Count == 0 ? Flag(given.Option.Name) : given.Tokens.Aggregate(this, (line, token) => line.Option(given.Option.Name, token.Value));
        }

        return this;
    }

    /// <summary>A sentence for each option <see cref="Carrying"/> left out, and why, to follow the command in a hint; empty for none.</summary>
    public string LeftOut { get; private set; } = string.Empty;

    /// <summary>Returns the command line, ending with <c>--output-format json</c>.</summary>
    public override string ToString() => string.Join(' ', _tokens.Append(GlobalOptionNames.OutputFormat).Append("json"));

    /// <summary>
    /// The command line as a hint's prose quotes it: in single quotes, or in backticks when it
    /// holds single quotes of its own, as a <see cref="Document"/> value does.
    /// </summary>
    public string Quoted()
    {
        string line = ToString();
        return line.Contains('\'', StringComparison.Ordinal) ? $"`{line}`" : $"'{line}'";
    }

    /// <summary>Command lines a hint runs one after the other, each <see cref="Quoted"/>, as in <c>'a', then 'b'</c>.</summary>
    public static string Steps(IEnumerable<ContinuationCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        return string.Join(", then ", commands.Select(static command => command.Quoted()));
    }

    /// <summary>
    /// The command line <paramref name="parse"/> holds, as the caller gave it, for a mistake whose
    /// correction is that line, with each password it read from stdin passed on as every command
    /// passes one it did not read from a variable (<see cref="PasswordVariable"/>), by the option of
    /// its environment variable; null when it gives a secret on the command line, reads a document
    /// from stdin, which a printed command cannot repeat, or names no command.
    /// </summary>
    /// <param name="parse">A command line that parses.</param>
    public static ContinuationCommand? Repeating(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        var path = new List<string>();
        for (CommandResult? current = parse.CommandResult; current?.Parent is not null; current = current.Parent as CommandResult)
        {
            path.Insert(0, current.Command.Name);
        }

        if (path.Count == 0)
        {
            return null;
        }

        var repeated = new ContinuationCommand([.. path]);
        var replay = new CommandLineReplay(repeated, []);
        if (!replay.Append(parse.RootCommandResult) || replay.Piped is not null)
        {
            return null;
        }

        // A line that names the password's variable as well keeps the one it names.
        foreach (string variable in replay.LeftOut.Select(static option => option[..^StandardOptionNames.StandardInputSuffix.Length] + StandardOptionNames.EnvironmentSuffix)
            .Where(variable => !parse.CommandResult.Children.OfType<OptionResult>().Any(given => given.Option.Name == variable)))
        {
            repeated.PasswordVariable(variable, null);
        }

        return repeated;
    }
}

/// <summary>
/// A file or directory a command a hint names writes, and the one rule for whether the command
/// replaces what is there: only what the caller agreed to replace, which is the output the
/// invocation wrote, or the output the caller named for it with <c>--overwrite</c>. A file or
/// directory the hint names itself, such as an encrypted copy beside the output, is written only
/// where nothing is: a file of the caller's there is refused with <c>OUTPUT_EXISTS</c> rather than
/// replaced, and so is the command run a second time.
/// </summary>
public sealed class ContinuationOutput
{
    private ContinuationOutput(string path, bool replaces)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Path = path;
        Replaces = replaces;
    }

    /// <summary>The file or directory, as the command names it.</summary>
    public string Path { get; }

    /// <summary>Whether the command replaces what is there.</summary>
    internal bool Replaces { get; }

    /// <summary>The output the invocation wrote, which the command replaces.</summary>
    public static ContinuationOutput Written(string path) => new(path, replaces: true);

    /// <summary>The output the caller named for the invocation, which it did not write: replaced when the caller allowed it.</summary>
    /// <param name="path">The output.</param>
    /// <param name="overwrite">Whether the caller gave <c>--overwrite</c>.</param>
    public static ContinuationOutput Requested(string path, bool overwrite) => new(path, overwrite);

    /// <summary>A file or directory the hint names itself, which the command writes only where nothing is.</summary>
    public static ContinuationOutput New(string path) => new(path, replaces: false);
}

/// <summary>
/// Appends to a command line the arguments and options of a parsed one, as the caller gave them:
/// never <c>--output-format</c>, which every printed command asks for itself.
/// </summary>
/// <param name="command">The command line appended to.</param>
/// <param name="dropped">The parameters the caller replaces or leaves out.</param>
internal sealed class CommandLineReplay(ContinuationCommand command, IReadOnlyCollection<Symbol> dropped)
{
    /// <summary>The option of a document the invocation read from stdin, or null.</summary>
    public string? Piped { get; private set; }

    /// <summary>The options of the secrets the invocation read from stdin, each <c>…-stdin</c> beside the <c>…-env</c> of its password (<see cref="PasswordOptions"/>).</summary>
    public List<string> LeftOut { get; } = [];

    /// <summary>Appends what <paramref name="result"/> and its subcommands hold; false when an option holds a secret given literally.</summary>
    public bool Append(CommandResult result)
    {
        foreach (SymbolResult child in result.Children)
        {
            if (child is CommandResult subcommand)
            {
                if (!Append(subcommand))
                {
                    return false;
                }
            }
            else if (child is OptionResult { Implicit: false } option && !dropped.Contains(option.Option)
                && option.Option.Name is not GlobalOptionNames.OutputFormat)
            {
                ParameterMetadata metadata = option.Option.GetParameterMetadata();
                if (metadata.LiteralSecret)
                {
                    return false;
                }

                if (metadata.ValueSource == ParameterValueSource.StandardInput)
                {
                    LeftOut.Add(option.Option.Name);
                    continue;
                }

                command.Flag(option.Option.Name, option.Tokens.Count == 0);
                foreach (Token token in option.Tokens)
                {
                    Piped ??= metadata.InputKind == InputKind.JsonSource && token.Value == "-" ? option.Option.Name : null;
                    // An inline operation document is passed in single quotes, as a document is.
                    _ = metadata.InputKind == InputKind.JsonSource && token.Value.TrimStart() is ['{' or '[', ..]
                        ? command.Document(option.Option.Name, token.Value)
                        : command.Option(option.Option.Name, token.Value);
                }
            }
            else if (child is ArgumentResult argument && !dropped.Contains(argument.Argument))
            {
                foreach (Token token in argument.Tokens)
                {
                    command.Argument(token.Value);
                }
            }
        }

        return true;
    }
}
