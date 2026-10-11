using System.CommandLine;
using Aspose.Cli.Host.Output;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The actual parsed command, resolved globals and declared execution ownership.</summary>
internal sealed class ParsedInvocation
{
    internal ParsedInvocation(ParseResult parse, GlobalOptions globals, GlobalValues? inherited = null)
    {
        ParseResult = parse;
        GlobalValues = parse.Errors.Count == 0 ? globals.Resolve(parse, inherited) : null;
        var path = new List<Command>();
        for (CommandResult? current = parse.CommandResult; current?.Parent is not null;
            current = current.Parent as CommandResult)
        {
            path.Add(current.Command);
        }
        path.Reverse();
        CommandPath = path.AsReadOnly();
        ProductId = path.Select(static command => command.Policy().ProductId).FirstOrDefault(static id => id is not null);
        Execution = path.Select(static command => command.Policy().Execution)
            .FirstOrDefault(static value => value != CommandExecutionOwnership.Worker);
        McpAllowed = ProductId is not null || parse.CommandResult.Command.Policy().McpAllowed;
    }

    public ParseResult ParseResult { get; }
    public GlobalValues? GlobalValues { get; }
    public IReadOnlyList<Command> CommandPath { get; }
    public Command Command => ParseResult.CommandResult.Command;
    public string? ProductId { get; }
    public CommandExecutionOwnership Execution { get; }
    public bool McpAllowed { get; }

    /// <summary>
    /// Whether the command only groups other commands and was given none of them, such as
    /// <c>aspose-cli cells</c>: it does nothing on its own, so its help is the answer.
    /// </summary>
    internal bool LacksCommand =>
        ParseResult.Errors.Count > 0
        && ParseResult.Errors.All(static error => error.SymbolResult is CommandResult)
        && Command.Action is null
        && VisibleCommands().Length > 0
        && ParseResult.UnmatchedTokens.Count == 0
        && !OptionLikeArguments().Any();

    internal void EnsureValid()
    {
        if (LacksCommand)
        {
            throw CliErrors.CommandRequired(
                string.Join(' ', [DistributionInfo.CommandName, .. CommandPath.Select(static command => command.Name)]),
                [.. VisibleCommands().Select(Below)]);
        }

        if ((UnknownCommand() ?? HelpOrVersionWord()) is { } unknown)
        {
            throw CliErrors.Usage([unknown.Problem], unknown.Mistake);
        }

        (ParseError Error, string Problem, Mistake Mistake)? value = UnknownValue();
        string[] problems =
        [
            .. ParseResult.Errors.Select(error => value is { } refused && refused.Error == error ? refused.Problem : error.Message),
            .. OptionLikeArguments().Select(static token => $"Unrecognized command or argument '{token}'."),
        ];
        if (problems.Length > 0)
        {
            throw CliErrors.Usage(problems, UnknownOption() ?? value?.Mistake);
        }
    }

    /// <summary>The names of the commands the command groups, as help lists them.</summary>
    private string[] VisibleCommands() =>
        [.. Command.Subcommands.Where(static command => !command.Hidden).Select(static command => command.Name)];

    /// <summary>A command of this one by its path below the root, such as <c>cells inspect</c>.</summary>
    private string Below(string name) => string.Join(' ', [.. CommandPath.Select(static command => command.Name), name]);

    /// <summary>
    /// The tokens starting with <c>--</c> that the command's arguments took, such as a mistyped
    /// option after a list of files: they are unknown options, never file names, unless the
    /// caller ended the options with a <c>--</c> token.
    /// </summary>
    private IEnumerable<string> OptionLikeArguments() =>
        ParseResult.Tokens.Any(static token => token.Type == TokenType.DoubleDash)
            ? []
            : ParseResult.CommandResult.Children.OfType<ArgumentResult>()
                .SelectMany(static argument => argument.Tokens)
                .Select(static token => token.Value)
                .Where(static value => value.StartsWith("--", StringComparison.Ordinal));

    /// <summary>
    /// A command that only groups other commands, given a first token that names none of them:
    /// the token is a mistyped command, so the tokens after it are not reported on their own and
    /// the closest commands are suggested by their full path, as unknown options are. Help and
    /// the version are options, not commands, so <c>help</c> and <c>version</c> suggest
    /// <c>--help</c> and <c>--version</c>.
    /// </summary>
    private (string Problem, Mistake Mistake)? UnknownCommand()
    {
        string[] commands = VisibleCommands();
        if (commands.Length == 0
            || ParseResult.UnmatchedTokens.FirstOrDefault() is not { } token || token.StartsWith('-'))
        {
            return null;
        }

        string parent = string.Join(' ', [DistributionInfo.CommandName, .. CommandPath.Select(static command => command.Name)]);
        string? meant = token switch
        {
            "help" => Below("--help"),
            "version" when CommandPath.Count == 0 => "--version",
            _ => null,
        };
        return (
            $"'{token}' is not a command of '{parent}'.",
            Mistake.Of(token, commands.Select(Below), meant, keyOf: static name => name[(name.LastIndexOf(' ') + 1)..]));
    }

    /// <summary>
    /// A <c>help</c> or <c>version</c> the parser left over beside the command it found, typed
    /// before or after the command's name: help and the version are options, so the command's <c>--help</c>
    /// or <c>--version</c> is suggested, and the problems the stray word caused, such as a missing
    /// argument, are not reported on their own.
    /// </summary>
    private (string Problem, Mistake Mistake)? HelpOrVersionWord()
    {
        if (ParseResult.UnmatchedTokens.FirstOrDefault(static token => token is "help" or "version") is not { } token)
        {
            return null;
        }

        string parent = string.Join(' ', [DistributionInfo.CommandName, .. CommandPath.Select(static command => command.Name)]);
        string meant = token == "help" ? Below("--help") : "--version";
        return ($"'{token}' is not a command of '{parent}'.", Mistake.Of(token, [meant], meant));
    }

    /// <summary>
    /// The first unknown option among the options the command accepts: its own, the recursive
    /// ones of its parents and the root's, compared without their dashes; null when there is none.
    /// </summary>
    private Mistake? UnknownOption()
    {
        if (ParseResult.UnmatchedTokens.Concat(OptionLikeArguments())
            .FirstOrDefault(static token => token.StartsWith('-')) is not { } unknown)
        {
            return null;
        }

        IEnumerable<string> options = Command.Options
            .Concat(CommandPath.SelectMany(static command => command.Options.Where(static option => option.Recursive)))
            .Concat(ParseResult.RootCommandResult.Command.Options.Where(static option => option.Recursive))
            .Where(static option => !option.Hidden)
            .Select(static option => option.Name)
            .Distinct(StringComparer.Ordinal);
        return Mistake.Of(unknown, options, keyOf: static name => name.TrimStart('-'));
    }

    /// <summary>
    /// The first value the parser refused that its option or argument declares a list of values
    /// for, such as a mistyped <c>--detail</c>: the error, the one-line problem that replaces the
    /// parser's message, which lists the values on lines of their own, and the mistake; null when
    /// no refused value has such a list.
    /// </summary>
    private (ParseError Error, string Problem, Mistake Mistake)? UnknownValue()
    {
        foreach (ParseError error in ParseResult.Errors)
        {
            (Option? option, string name, IReadOnlyList<string> allowed, IReadOnlyList<Token> tokens) = error.SymbolResult switch
            {
                OptionResult result => (result.Option, result.Option.Name, OptionCompletions.Read(result.Option), result.Tokens),
                ArgumentResult { Parent: OptionResult result } argument => (result.Option, result.Option.Name, OptionCompletions.Read(result.Option), argument.Tokens),
                ArgumentResult argument => (null, $"<{argument.Argument.Name}>", OptionCompletions.Read(argument.Argument), argument.Tokens),
                _ => (null, string.Empty, [], []),
            };
            if (allowed.Count > 0
                && tokens.Select(static token => token.Value).LastOrDefault(value => !allowed.Contains(value, StringComparer.Ordinal)) is { } value)
            {
                return (
                    error,
                    $"'{value}' is not a value of {name}.",
                    Mistake.Of(value, allowed, meant: option?.Name == GlobalOptionNames.OutputFormat ? TargetFormat(value) : null));
            }
        }

        return null;
    }

    /// <summary>
    /// A file format given to <c>--output-format</c>, which selects how results are printed:
    /// <c>--to</c> with that format when the command writes it; null otherwise.
    /// </summary>
    private string? TargetFormat(string value) =>
        Command.Options.FirstOrDefault(static option => option.Name == "--to") is { } to
        && OptionCompletions.Read(to).Contains(value, StringComparer.Ordinal)
            ? $"--to {value}"
            : null;
}

/// <summary>One command tree for CLI execution, supervision and MCP authorization.</summary>
internal sealed class InvocationParser(RootCommand root, GlobalOptions globals)
{
    // Tokens are always literal. Response-file expansion would read a file relative to
    // whichever process parses, so a supervisor, worker or MCP child could each resolve
    // a different command from the same arguments, and '@'-prefixed file names would
    // be unusable.
    private static readonly ParserConfiguration Configuration = new() { ResponseFileTokenReplacer = null };

    internal ParsedInvocation Parse(string[] args, GlobalValues? inherited = null) =>
        new(root.Parse(args, Configuration), globals, inherited);

    internal (OutputMode OutputFormat, bool Quiet) ResolveErrorOutput(IReadOnlyList<string> args) =>
        globals.ResolveForErrorReporting(root.Parse(args, Configuration));

    /// <summary>Writes the help of the command <paramref name="args"/> name to <paramref name="output"/>, as <c>--help</c> prints it.</summary>
    internal void WriteHelp(IReadOnlyList<string> args, TextWriter output) =>
        root.Parse([.. args, "--help"], Configuration).Invoke(new InvocationConfiguration
        {
            Output = output,
            EnableDefaultExceptionHandler = false,
        });

    /// <summary>The host commands MCP <c>execute</c> may run, as command paths in tree order.</summary>
    internal IReadOnlyList<string> McpHostCommands()
    {
        var paths = new List<string>();
        Collect(root, prefix: null);
        return paths;

        void Collect(Command command, string? prefix)
        {
            foreach (Command child in command.Subcommands)
            {
                if (child.Policy().ProductId is not null) { continue; }
                string path = prefix is null ? child.Name : prefix + " " + child.Name;
                if (child.Policy().McpAllowed) { paths.Add(path); }
                Collect(child, path);
            }
        }
    }
}
