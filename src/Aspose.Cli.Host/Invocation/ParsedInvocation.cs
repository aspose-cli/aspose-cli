using System.CommandLine;
using System.CommandLine.Help;
using Aspose.Cli.Host.Output;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The actual parsed command, resolved globals and declared execution ownership.</summary>
internal sealed class ParsedInvocation
{
    internal ParsedInvocation(ParseResult parse, GlobalOptions globals)
    {
        ParseResult = parse;
        GlobalValues = parse.Errors.Count == 0 ? Globals(parse, globals) : null;
        var path = new List<Command>();
        for (CommandResult? current = parse.CommandResult; current?.Parent is not null;
            current = current.Parent as CommandResult)
        {
            path.Add(current.Command);
        }
        path.Reverse();
        CommandPath = path.AsReadOnly();
        ProductId = path.Select(static command => command.Policy().ProductId).FirstOrDefault(static id => id is not null);
    }

    /// <summary>
    /// The global values. Help stops the parser before it checks values, so beside --help a
    /// global value that does not parse leaves them unread, and help prints as the parser gives it.
    /// </summary>
    private static GlobalValues? Globals(ParseResult parse, GlobalOptions globals)
    {
        try
        {
            return globals.Resolve(parse);
        }
        catch (InvalidOperationException) when (AsksForHelp(parse))
        {
            return null;
        }
    }

    /// <summary>Whether the command line asks for help, which the parser gives without checking any value.</summary>
    private static bool AsksForHelp(ParseResult parse) =>
        parse.RootCommandResult.Command.Options.OfType<HelpOption>().Any(help => parse.GetResult(help) is not null);

    public ParseResult ParseResult { get; }
    public GlobalValues? GlobalValues { get; }
    public IReadOnlyList<Command> CommandPath { get; }
    public Command Command => ParseResult.CommandResult.Command;
    public string? ProductId { get; }

    /// <summary>
    /// What the invocation may reach through the <see cref="NetworkGate"/>: the web for a set
    /// network opt-in flag, and nothing otherwise.
    /// </summary>
    public NetworkAccess NetworkAccess =>
        CommandPath.SelectMany(static command => command.Options).OfType<Option<bool>>()
            .Any(option => option.IsNetworkOptIn() && ParseResult.GetResult(option) is not null && ParseResult.GetValue(option))
            ? NetworkAccess.Web
            : NetworkAccess.None;

    /// <summary>
    /// Whether the command only groups other commands and was given none of them, such as
    /// <c>aspose-cli cells</c>, alone or with an option it does not take: it does nothing on its own, so
    /// its help is the answer. Options it does not know are reported beside the missing command.
    /// </summary>
    internal bool LacksCommand =>
        ParseResult.Errors.Count > 0
        && ParseResult.Errors.All(static error => error.SymbolResult is CommandResult)
        && Command.Action is null
        && VisibleCommands().Length > 0
        && (ParseResult.UnmatchedTokens.FirstOrDefault() is not { } first || first.StartsWith('-'))
        && !OptionLikeArguments().Any();

    internal void EnsureValid()
    {
        if (LacksCommand)
        {
            string[] unknown = [.. ParseResult.UnmatchedTokens.Where(static token => token.StartsWith('-')).Select(OptionName)];
            throw CliErrors.CommandRequired(
                Path(),
                [.. VisibleCommands().Select(Below)],
                unknown);
        }

        if (VersionWithOthers() is { } version)
        {
            // The version takes only the output format, so that is the one option the error lists.
            string[] taken = [GlobalOptionNames.OutputFormat];
            throw CliErrors.Usage([$"'{version}' is not an option of --version, which takes only {taken[0]}."],
                Mistake.Of(version, taken), parameter: version);
        }

        if (Problems() is { } refused)
        {
            throw refused.ValuesOnly
                ? CliErrors.OptionValuesRefused(refused.Problems, refused.Parameter!, refused.Mistake)
                : CliErrors.Usage(refused.Problems, refused.Mistake, refused.Parameter);
        }
    }

    /// <summary>
    /// What makes the command line fail: the problems, the names that exist for the first
    /// unknown command, option or value among it, the option or argument the first problem is
    /// about, and whether every problem is a value the command refused, so the command line
    /// itself parses; null when nothing fails.
    /// </summary>
    private Refusal? Problems() =>
        MissingCommand()
        ?? UnknownCommand()
        ?? ParseProblems();

    /// <summary>A command that only groups others, given none of them (<see cref="LacksCommand"/>); null otherwise.</summary>
    private Refusal? MissingCommand() =>
        LacksCommand ? new([$"'{Path()}' needs a command."], null, null, ValuesOnly: false) : null;

    /// <summary>
    /// The parser's own errors and the options the arguments took: refused values alone
    /// (<see cref="RefusedValues"/>), or else an unknown option or a stray argument
    /// (<see cref="UnknownOptionOrArgument"/>); null when the command line parses.
    /// </summary>
    private Refusal? ParseProblems()
    {
        var values = new Dictionary<ParseError, RefusedValue>();
        foreach (ParseError error in ParseResult.Errors)
        {
            if (Refused(error) is { } value)
            {
                values[error] = value;
            }
        }

        // The parser does not check a list for a value it takes for a name it knows there, such
        // as --verbose written where the value of --content belongs; the same table of names picks
        // those values here, and no other value is touched. Beside --help nothing is checked,
        // as the parser checks nothing there.
        HashSet<string> names = KnownNames();
        RefusedValue[] outside = AsksForHelp(ParseResult) ? [] : [.. OptionResults()
            .Where(result => !values.Values.Any(value => value.Parameter == result.Option.Name))
            .Select(result => OutsideList(result.Option, null, [.. result.Tokens.Where(token => names.Contains(token.Value))]))
            .OfType<RefusedValue>()];
        string[] optionLike = [.. OptionLikeArguments()];
        string[] problems =
        [
            .. ParseResult.Errors.Select(error => values.TryGetValue(error, out RefusedValue? refused) ? refused.Problem : Unmatched(error) ?? error.Message),
            .. outside.Select(static value => value.Problem),
            .. optionLike.Select(Unrecognized),
        ];
        if (problems.Length == 0)
        {
            return null;
        }

        RefusedValue? first = ParseResult.Errors.Select(error => values.GetValueOrDefault(error)).FirstOrDefault(static value => value is not null)
            ?? outside.FirstOrDefault();
        return RefusedValues(problems, first, values.Count + outside.Length)
            ?? UnknownOptionOrArgument(problems, first, optionLike);
    }

    /// <summary>
    /// Every problem is a value the command refused (<see cref="Refused"/>,
    /// <see cref="OutsideList"/>): the command line itself parses, so the refusal is about the
    /// values alone, with the first one's mistake; null otherwise.
    /// </summary>
    private static Refusal? RefusedValues(string[] problems, RefusedValue? first, int refused) =>
        first is not null && refused == problems.Length
            ? new(problems, first.Mistake, first.Parameter, ValuesOnly: true)
            : null;

    /// <summary>
    /// Any other failing command line: the first unknown option lists the options the command
    /// accepts; without one, the first refused value lists its values, else a single missing
    /// option its values. A token left over after an option's value is about that option
    /// (<see cref="OptionWithValueBefore"/>).
    /// </summary>
    private Refusal UnknownOptionOrArgument(string[] problems, RefusedValue? first, string[] optionLike)
    {
        string? unknownOption = ParseResult.UnmatchedTokens.Concat(optionLike).Where(static token => token.StartsWith('-')).Select(OptionName).FirstOrDefault();
        Option? crowded = ParseResult.UnmatchedTokens.Select(OptionWithValueBefore).FirstOrDefault(static option => option is not null)?.Option;
        Option? missing = MissingOptions().FirstOrDefault();
        Mistake? mistake = unknownOption is not null ? Mistake.Of(unknownOption, [.. AcceptedOptions().Select(static option => option.Name)])
            : first?.Mistake
            ?? (problems.Length == 1 && missing is not null && OptionCompletions.Read(missing) is { Count: > 0 } allowed
                ? Mistake.Of(string.Empty, allowed)
                : null);
        string? parameter = unknownOption ?? crowded?.Name ?? missing?.Name ?? MissingArguments().FirstOrDefault()?.Name ?? first?.Parameter;
        return new(problems, mistake, parameter, ValuesOnly: false);
    }

    /// <summary>What fails a command line; see <see cref="Problems"/>.</summary>
    private sealed record Refusal(string[] Problems, Mistake? Mistake, string? Parameter, bool ValuesOnly);

    /// <summary>A value the command refused: the sentence that states it, its option or argument, and the mistake when it has a list of values.</summary>
    private sealed record RefusedValue(string Problem, string Parameter, Mistake? Mistake);

    /// <summary>The command as the caller typed it, such as <c>aspose-cli cells</c>.</summary>
    private string Path() => string.Join(' ', [DistributionInfo.CommandName, .. CommandPath.Select(static command => command.Name)]);

    /// <summary>The required options of the command the command line does not give, in declared order.</summary>
    private IEnumerable<Option> MissingOptions() =>
        Command.Options.Where(option => option.Required && ParseResult.GetResult(option) is not { Tokens.Count: > 0 });

    /// <summary>The arguments of the command that need a value and were given none, in declared order.</summary>
    private IEnumerable<Argument> MissingArguments() =>
        Command.Arguments.Where(argument => argument.Arity.MinimumNumberOfValues > 0
            && ParseResult.GetResult(argument) is not { Tokens.Count: > 0 });

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

    /// <summary>The parser's sentence for a token it could not place.</summary>
    private static string UnrecognizedMessage(string token) => $"Unrecognized command or argument '{token}'.";

    /// <summary>
    /// <paramref name="error"/> when the parser could not place a token, said as
    /// <see cref="Unrecognized"/> says it; null for any other error.
    /// </summary>
    private string? Unmatched(ParseError error) =>
        ParseResult.UnmatchedTokens.FirstOrDefault(token => error.Message == UnrecognizedMessage(token)) is { } token
            ? Unrecognized(token)
            : null;

    /// <summary>
    /// A token the command could not place. The value of an unknown option, joined to it by
    /// <c>=</c> or <c>:</c> or written after it, may be a secret meant for the option the caller
    /// mistyped, such as the password given to a misspelled encryption option, so only the option
    /// is named; a token after an option's value is that option's extra value
    /// (<see cref="OptionWithValueBefore"/>); any other token is named as written.
    /// </summary>
    private string Unrecognized(string token) =>
        OptionName(token) is var name && name.Length < token.Length
            ? $"Unrecognized command or argument '{name}' and the value given to it."
            : UnknownOptionBefore(token) is { } option
                ? $"Unrecognized command or argument after '{option}', which is not repeated in case it is a secret."
                : OptionWithValueBefore(token) is { } crowded
                    ? ExtraValueMessage(crowded.Option, crowded.Secret, token)
                    : UnrecognizedMessage(token);

    /// <summary>
    /// The name of an option token, without a value joined to it by <c>=</c> or <c>:</c>, as the
    /// parser takes them: the part before the first of them.
    /// </summary>
    private static string OptionName(string token) =>
        token.StartsWith('-') && token.IndexOfAny(['=', ':']) is > 1 and var end ? token[..end] : token;

    /// <summary>
    /// The unknown option that <paramref name="value"/>, not an option itself, follows on the
    /// command line, so it is that option's value; null when it follows none.
    /// </summary>
    private string? UnknownOptionBefore(string value)
    {
        if (value.StartsWith('-'))
        {
            return null;
        }

        string[] unknown = [.. ParseResult.UnmatchedTokens.Concat(OptionLikeArguments()).Where(static token => token.StartsWith('-'))];
        Token[] tokens = [.. ParseResult.Tokens];
        for (int index = 1; index < tokens.Length; index++)
        {
            if (tokens[index].Value == value && unknown.Contains(tokens[index - 1].Value, StringComparer.Ordinal))
            {
                return OptionName(tokens[index - 1].Value);
            }
        }

        return null;
    }

    /// <summary>
    /// A command that only groups other commands, given a first token that names none of them:
    /// the token is an unknown command, so the tokens after it are not reported on their own, and
    /// the commands of the group are listed by their full path.
    /// </summary>
    private Refusal? UnknownCommand()
    {
        string[] commands = VisibleCommands();
        if (commands.Length == 0
            || ParseResult.UnmatchedTokens.FirstOrDefault() is not { } token || token.StartsWith('-'))
        {
            return null;
        }

        string parent = string.Join(' ', [DistributionInfo.CommandName, .. CommandPath.Select(static command => command.Name)]);
        return new(
            [$"'{token}' is not a command of '{parent}'."],
            Mistake.Of(token, commands.Select(Below)),
            null,
            ValuesOnly: false);
    }

    /// <summary>The options the command accepts: its own, the recursive ones of its parents and the root's, visible ones once each.</summary>
    private Option[] AcceptedOptions() =>
    [
        .. Command.Options
            .Concat(CommandPath.SelectMany(static command => command.Options.Where(static option => option.Recursive)))
            .Concat(ParseResult.RootCommandResult.Command.Options.Where(static option => option.Recursive))
            .Where(static option => !option.Hidden)
            .DistinctBy(static option => option.Name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// The first token beside <c>--version</c> that the parser leaves over, such as an unknown
    /// option after it: the version is a terminating option, so the parser runs it
    /// without reporting the token; null when there is none.
    /// </summary>
    private string? VersionWithOthers() =>
        CommandPath.Count == 0
        && ParseResult.RootCommandResult.Children.OfType<OptionResult>().Any(static result => result.Option is VersionOption)
            ? ParseResult.UnmatchedTokens.FirstOrDefault() is { } token ? OptionName(token) : null
            : null;

    /// <summary>
    /// <paramref name="error"/> when it refuses a value given to an option or argument, rather
    /// than a missing value or more values than it takes: a value outside its list, such as a
    /// mistyped <c>--detail</c>, whose problem replaces the parser's message, which lists the
    /// values on lines of their own; a number that does not parse; or a value a validator
    /// refused. Null for any other error.
    /// </summary>
    private static RefusedValue? Refused(ParseError error)
    {
        (Option? option, Argument? argument, IReadOnlyList<Token> tokens) = error.SymbolResult switch
        {
            OptionResult result => (result.Option, null, result.Tokens),
            ArgumentResult { Parent: OptionResult result } argumentResult => (result.Option, null, argumentResult.Tokens),
            ArgumentResult argumentResult => (null, argumentResult.Argument, argumentResult.Tokens),
            _ => ((Option?)null, (Argument?)null, (IReadOnlyList<Token>)[]),
        };
        if (option is null && argument is null)
        {
            return null;
        }

        ArgumentArity arity = option?.Arity ?? argument!.Arity;
        if (tokens.Count == 0 || tokens.Count > arity.MaximumNumberOfValues)
        {
            return null;
        }

        if (OutsideList(option, argument, tokens) is { } outside)
        {
            return outside;
        }

        string parameter = option?.Name ?? argument!.Name;
        string name = option?.Name ?? $"<{argument!.Name}>";

        Type type = option?.ValueType ?? argument!.ValueType;
        type = Nullable.GetUnderlyingType(type) ?? type;
        string? number = Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32
                or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "a whole number",
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "a number",
            _ => null,
        };
        return number is not null && error.Message.StartsWith("Cannot parse argument", StringComparison.Ordinal)
            ? new($"'{tokens[^1].Value}' is not {number}, which {name} takes.", parameter, null)
            : new(error.Message, parameter, null);
    }

    /// <summary>
    /// The last of <paramref name="tokens"/> that is none of the values the option or argument
    /// lists, with its sentence and the values; null when every one is listed or it lists none.
    /// </summary>
    private static RefusedValue? OutsideList(Option? option, Argument? argument, IReadOnlyList<Token> tokens)
    {
        string name = option?.Name ?? $"<{argument!.Name}>";
        IReadOnlyList<string> allowed = option is not null ? OptionCompletions.Read(option) : OptionCompletions.Read(argument!);
        return allowed.Count > 0
            && tokens.Select(static token => token.Value).LastOrDefault(value => !allowed.Contains(value, StringComparer.Ordinal)) is { } value
            ? new($"'{value}' is not a value of {name}.", option?.Name ?? argument!.Name, Mistake.Of(value, allowed))
            : null;
    }

    /// <summary>
    /// The names the parser knows where the command's values are written: the command's options,
    /// hidden ones too, the recursive options of the commands above it and of the root, their
    /// aliases, and the command's own commands with theirs.
    /// </summary>
    private HashSet<string> KnownNames() =>
    [
        .. Command.Options
            .Concat(CommandPath.SelectMany(static command => command.Options.Where(static option => option.Recursive)))
            .Concat(ParseResult.RootCommandResult.Command.Options.Where(static option => option.Recursive))
            .SelectMany(static option => option.Aliases.Prepend(option.Name)),
        .. Command.Subcommands.SelectMany(static command => command.Aliases.Prepend(command.Name)),
    ];

    /// <summary>The options the command line gives, as opposed to their defaults.</summary>
    private IEnumerable<OptionResult> OptionResults() =>
        AcceptedOptions().Select(ParseResult.GetResult).OfType<OptionResult>().Where(static result => !result.Implicit);

    /// <summary>Whether an option holds <paramref name="token"/> as its value.</summary>
    private bool TakenByAnOption(Token token) =>
        OptionResults().Any(result => result.Tokens.Any(value => ReferenceEquals(value, token)));

    /// <summary>Whether an option or an argument holds <paramref name="token"/>.</summary>
    private bool Taken(Token token) =>
        TakenByAnOption(token)
        || ParseResult.CommandResult.Children.OfType<ArgumentResult>().Any(argument => argument.Tokens.Any(value => ReferenceEquals(value, token)));

    /// <summary>
    /// The option <paramref name="leftOver"/> follows with the value it already holds, as
    /// <c>B2</c> follows <c>--range A1</c>, directly or after other tokens left over like it: it
    /// is one value too many for that option. It may be a secret when that option holds one or
    /// the value it follows names a secret option, as <c>--password</c> given to <c>--content</c>
    /// does. Null for a token that follows no option's value.
    /// </summary>
    private (Option Option, bool Secret)? OptionWithValueBefore(string leftOver)
    {
        if (leftOver.StartsWith('-'))
        {
            return null;
        }

        Token[] tokens = [.. ParseResult.Tokens];
        for (int index = 1; index < tokens.Length; index++)
        {
            if (tokens[index].Value != leftOver || Taken(tokens[index]))
            {
                continue;
            }

            int before = index - 1;
            while (before > 0 && !Taken(tokens[before]) && !tokens[before].Value.StartsWith('-'))
            {
                before--;
            }

            Token last = tokens[before];
            if (OptionResults().FirstOrDefault(result => result.Tokens.Any(value => ReferenceEquals(value, last))) is { } holder)
            {
                return (holder.Option, holder.Option.GetParameterMetadata().Secret
                    || AcceptedOptions().Any(option => option.GetParameterMetadata().Secret && option.Aliases.Prepend(option.Name).Contains(last.Value, StringComparer.Ordinal)));
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence for a value one too many for <paramref name="option"/>; a value that may be a
    /// secret (<see cref="OptionWithValueBefore"/>) is not repeated.
    /// </summary>
    private static string ExtraValueMessage(Option option, bool secret, string value)
    {
        string extra = secret
            ? "the value after it, which is not repeated in case it is a secret,"
            : $"'{value}'";
        return option.Arity.MaximumNumberOfValues > 1
            ? $"{option.Name} takes one value each time it is given; {extra} is left over."
            : $"{option.Name} takes one value; {extra} is one too many.";
    }
}

/// <summary>One command tree for CLI execution.</summary>
/// <param name="root">The command tree.</param>
/// <param name="globals">The global options of the tree.</param>
internal sealed class InvocationParser(RootCommand root, GlobalOptions globals)
{
    // Tokens are always literal: response-file expansion would make '@'-prefixed file names
    // unusable.
    internal static readonly ParserConfiguration Configuration = new() { ResponseFileTokenReplacer = null };

    internal ParsedInvocation Parse(string[] args) =>
        new(root.Parse(args, Configuration), globals);

    internal OutputMode ResolveErrorOutput(IReadOnlyList<string> args) =>
        globals.ResolveForErrorReporting(root.Parse(args, Configuration));
}
