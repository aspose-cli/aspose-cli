using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Builds the ready-to-run follow-up command a bounded read advertises in its <c>next</c>
/// field. The command names this executable, quotes every value that a POSIX shell,
/// PowerShell or cmd would otherwise split or expand, and always asks for JSON, so an
/// agent can run it verbatim instead of computing the next window itself.
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
    /// correction is that line; null when it gives a secret on the command line or on stdin, reads
    /// a document from stdin, which a printed command cannot repeat, or names no command.
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
        var replay = new CommandLineReplay(repeated, [], passed: null);
        return replay.Append(parse.RootCommandResult) && replay.LeftOut.Count == 0 && replay.Piped is null ? repeated : null;
    }
}

/// <summary>
/// Appends to a command line the arguments and options of a parsed one, as the caller gave them:
/// never <c>--output-format</c>, which every printed command asks for itself.
/// </summary>
/// <param name="command">The command line appended to.</param>
/// <param name="dropped">The parameters the caller replaces or leaves out.</param>
/// <param name="passed">The name of an option the command line already gives, or null.</param>
internal sealed class CommandLineReplay(ContinuationCommand command, IReadOnlyCollection<Symbol> dropped, string? passed)
{
    /// <summary>The option of a document the invocation read from stdin, or null.</summary>
    public string? Piped { get; private set; }

    /// <summary>The options of the secrets the invocation read from stdin.</summary>
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
                && option.Option.Name is not GlobalOptionNames.OutputFormat && option.Option.Name != passed)
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
