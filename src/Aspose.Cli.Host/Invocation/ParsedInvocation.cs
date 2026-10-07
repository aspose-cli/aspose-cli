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

    internal void EnsureValid()
    {
        if (UnknownCommand() is { } unknown)
        {
            throw CliErrors.Usage([unknown.Problem], unknown.Mistake);
        }

        string[] problems =
        [
            .. ParseResult.Errors.Select(static error => error.Message),
            .. OptionLikeArguments().Select(static token => $"Unrecognized command or argument '{token}'."),
        ];
        if (problems.Length > 0)
        {
            throw CliErrors.Usage(problems, UnknownOption() ?? UnknownValue());
        }
    }

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
    /// the closest commands are suggested by their full path, as unknown options are.
    /// </summary>
    private (string Problem, Mistake Mistake)? UnknownCommand()
    {
        string[] commands = [.. Command.Subcommands.Where(static command => !command.Hidden).Select(static command => command.Name)];
        if (commands.Length == 0
            || ParseResult.UnmatchedTokens.FirstOrDefault() is not { } token || token.StartsWith('-'))
        {
            return null;
        }

        string[] path = [.. CommandPath.Select(static command => command.Name)];
        string parent = string.Join(' ', [DistributionInfo.CommandName, .. path]);
        return (
            $"'{token}' is not a command of '{parent}'; its commands: {string.Join(", ", commands)}.",
            Mistake.Of(token, commands.Select(name => string.Join(' ', [.. path, name])), keyOf: static name => name[(name.LastIndexOf(' ') + 1)..]));
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
    /// for, such as a mistyped <c>--detail</c>; null when no refused value has such a list.
    /// </summary>
    private Mistake? UnknownValue()
    {
        foreach (ParseError error in ParseResult.Errors)
        {
            (IReadOnlyList<string> allowed, IReadOnlyList<Token> tokens) = error.SymbolResult switch
            {
                OptionResult option => (OptionCompletions.Read(option.Option), option.Tokens),
                ArgumentResult { Parent: OptionResult option } argument => (OptionCompletions.Read(option.Option), argument.Tokens),
                ArgumentResult argument => (OptionCompletions.Read(argument.Argument), argument.Tokens),
                _ => ([], []),
            };
            if (allowed.Count > 0
                && tokens.Select(static token => token.Value).LastOrDefault(value => !allowed.Contains(value, StringComparer.Ordinal)) is { } value)
            {
                return Mistake.Of(value, allowed);
            }
        }

        return null;
    }
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

    internal (OutputMode Output, bool Quiet) ResolveErrorOutput(IReadOnlyList<string> args) =>
        globals.ResolveForErrorReporting(root.Parse(args, Configuration));

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
