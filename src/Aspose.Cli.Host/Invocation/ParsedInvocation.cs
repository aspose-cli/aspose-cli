using System.CommandLine;
using Aspose.Cli.Host.Output;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The actual parsed command, resolved globals and declared execution ownership.</summary>
internal sealed class ParsedInvocation
{
    private readonly GlobalOptions _globals;

    internal ParsedInvocation(ParseResult parse, GlobalOptions globals, GlobalValues? inherited = null)
    {
        ParseResult = parse;
        _globals = globals;
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
    /// <c>aspose-cli cells</c> or <c>aspose-cli cells --bogus</c>: it does nothing on its own, so
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
            string[] unknown = [.. ParseResult.UnmatchedTokens.Where(static token => token.StartsWith('-'))];
            throw CliErrors.CommandRequired(
                Path(),
                [.. VisibleCommands().Select(Below)],
                unknown,
                unknown.Length > 0 ? UnknownOptionMistake(unknown[0]).Suggestions : null);
        }

        // Before help: a mistaken name beside --help would otherwise show the help as if it were right.
        if (MistakenName() is { } mistaken)
        {
            throw CliErrors.Usage([mistaken.Problem, .. mistaken.Others], mistaken.Mistake, mistaken.Corrected);
        }

        if (VersionWithOthers() is { } version)
        {
            throw CliErrors.Usage([$"'{version}' is not an option of --version, which takes only --output-format."],
                UnknownOptionMistake(version), parameter: version);
        }

        if (Problems() is { } refused)
        {
            throw refused.ValuesOnly
                ? CliErrors.OptionValuesRefused(refused.Problems, refused.Parameter!, refused.Mistake)
                : CliErrors.Usage(refused.Problems, refused.Mistake, refused.Corrected, refused.Parameter);
        }
    }

    /// <summary>
    /// What makes the command line fail, other than a mistaken name: the problems, the first
    /// unknown command, option or value among it, the option or argument the first problem is
    /// about, and whether every problem is a value the command refused, so the command line
    /// itself parses; null when nothing fails.
    /// </summary>
    private Refusal? Problems()
    {
        if (LacksCommand)
        {
            return new([$"'{Path()}' needs a command."], null, null, ValuesOnly: false);
        }

        if ((UnknownCommand() ?? HelpOrVersionWord()) is { } unknown)
        {
            return new([unknown.Problem], unknown.Mistake, null, ValuesOnly: false);
        }

        if (Placeholder() is { } placeholder)
        {
            return placeholder;
        }

        var values = new Dictionary<ParseError, RefusedValue>();
        foreach (ParseError error in ParseResult.Errors)
        {
            if (Refused(error) is { } value)
            {
                values[error] = value;
            }
        }

        string[] optionLike = [.. OptionLikeArguments()];
        string[] problems =
        [
            .. ParseResult.Errors.Select(error => values.TryGetValue(error, out RefusedValue? refused) ? refused.Problem : error.Message),
            .. optionLike.Select(static token => $"Unrecognized command or argument '{token}'."),
        ];
        if (problems.Length == 0)
        {
            return null;
        }

        RefusedValue? first = ParseResult.Errors.Select(error => values.GetValueOrDefault(error)).FirstOrDefault(static value => value is not null);
        if (first is not null && values.Count == ParseResult.Errors.Count && optionLike.Length == 0)
        {
            return new(problems, first.Mistake, first.Parameter, ValuesOnly: true);
        }

        string? unknownOption = ParseResult.UnmatchedTokens.Concat(optionLike).FirstOrDefault(static token => token.StartsWith('-'));
        Option? missing = MissingOptions().FirstOrDefault();
        Mistake? mistake = unknownOption is not null ? UnknownOptionMistake(unknownOption)
            : StrayArgumentMistake() ?? first?.Mistake
            ?? (problems.Length == 1 && missing is not null && OptionCompletions.Read(missing) is { Count: > 0 } allowed
                ? Mistake.Of(string.Empty, allowed)
                : null);
        string? parameter = unknownOption ?? missing?.Name ?? MissingArguments().FirstOrDefault()?.Name ?? first?.Parameter;
        return new(problems, mistake, parameter, ValuesOnly: false);
    }

    /// <summary>
    /// The name of an argument written in place of its value beside the value itself, such as
    /// <c>words read file a.docx</c>, as a usage line reads: the name is taken as the value and
    /// the value is left over. The command line without the name is parsed again: when it
    /// parses it is the correction, ready to run; otherwise its own problems are reported beside
    /// the name. Null when no argument holds its own name while a token is left over.
    /// </summary>
    private Refusal? Placeholder()
    {
        if (ParseResult.UnmatchedTokens.Count == 0)
        {
            return null;
        }

        foreach (ArgumentResult result in ParseResult.CommandResult.Children.OfType<ArgumentResult>())
        {
            string name = result.Argument.Name;
            if (result.Tokens.FirstOrDefault(token => string.Equals(token.Value, name, StringComparison.OrdinalIgnoreCase)
                || token.Value == $"<{name}>") is not { } placeholder)
            {
                continue;
            }

            string[] values = [.. ParseResult.Tokens.Select(static item => item.Value)];
            int index = Array.IndexOf(values, placeholder.Value);
            string[] corrected = [.. values[..index], .. values[(index + 1)..]];
            Refusal? rest = new ParsedInvocation(ParseResult.RootCommandResult.Command.Parse(corrected, InvocationParser.Configuration), _globals).Problems();
            return new(
                [$"'{placeholder.Value}' stands for the <{name}> argument, whose value follows it.", .. rest?.Problems ?? []],
                rest?.Mistake,
                name,
                ValuesOnly: false,
                Corrected: rest is null ? corrected : null);
        }

        return null;
    }

    /// <summary>
    /// <paramref name="problems"/> with the suggestions of <paramref name="mistake"/> named beside
    /// the first problem that quotes it, such as <c>... '--patern' (did you mean '--pattern'?).</c>
    /// </summary>
    private static string[] WithAside(string[] problems, Mistake? mistake)
    {
        if (mistake is not { Suggestions.Count: > 0 })
        {
            return problems;
        }

        string quoted = $"'{mistake.Requested}'";
        int index = Array.FindIndex(problems, problem => problem.Contains(quoted, StringComparison.Ordinal));
        if (index < 0)
        {
            return problems;
        }

        string[] marked = [.. problems];
        marked[index] = marked[index].TrimEnd().TrimEnd('.') + mistake.Aside() + ".";
        return marked;
    }

    /// <summary>What fails a command line; see <see cref="Problems"/>. <c>Corrected</c> is the command line that fixes it, when one is known.</summary>
    private sealed record Refusal(string[] Problems, Mistake? Mistake, string? Parameter, bool ValuesOnly, IReadOnlyList<string>? Corrected = null);

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

    /// <summary>
    /// A command that only groups other commands, given a first token that names none of them:
    /// the token is a mistyped command, so the tokens after it are not reported on their own and
    /// the closest commands are suggested by their full path, as unknown options are. Help and
    /// the version are options, not commands, so <c>help</c> and <c>version</c> suggest
    /// <c>--help</c> and <c>--version</c>; a name commands declare they are mistaken for, such as
    /// a command that no longer exists, suggests those commands first.
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
        string[] meant = token switch
        {
            "help" => [Below("--help")],
            "version" when CommandPath.Count == 0 => ["--version"],
            _ => [.. Command.Subcommands
                .Where(command => !command.Hidden && command.MistakenNames().Contains(token, StringComparer.Ordinal))
                .Select(static command => command.Name)
                .Select(Below)],
        };
        return (
            $"'{token}' is not a command of '{parent}'.",
            Mistake.Of(token, commands.Select(Below), meant, keyOf: static name => name[(name.LastIndexOf(' ') + 1)..]));
    }

    /// <summary>
    /// A name the command or one of its siblings declares it is mistaken for, left over beside
    /// the command, such as the name of a removed command group written before the command it
    /// held: it names no command, so it is refused even with <c>--help</c>. The
    /// commands that declare it are suggested first, the command itself before its siblings. The
    /// command line without the name is parsed again: when it parses and the command declares the
    /// name, it is the correction, ready to run; otherwise its own problems are reported beside
    /// the name, so one error states everything to fix.
    /// </summary>
    private (string Problem, Mistake Mistake, IReadOnlyList<string>? Corrected, string[] Others)? MistakenName()
    {
        if (CommandPath.Count == 0)
        {
            return null;
        }

        Command[] siblings =
        [
            .. (CommandPath.Count > 1 ? CommandPath[^2] : ParseResult.RootCommandResult.Command).Subcommands
                .Where(static command => !command.Hidden),
        ];
        if (ParseResult.UnmatchedTokens.FirstOrDefault(token => siblings.Any(command => Declares(command, token))) is not { } token)
        {
            return null;
        }

        string[] parentPath = [.. CommandPath.Take(CommandPath.Count - 1).Select(static command => command.Name)];
        string Sibling(Command command) => string.Join(' ', [.. parentPath, command.Name]);
        string[] meant = [.. siblings.Where(command => Declares(command, token)).OrderBy(command => command == Command ? 0 : 1).Select(Sibling)];
        string[] values = [.. ParseResult.Tokens.Select(static item => item.Value)];
        int index = Array.IndexOf(values, token);
        string[] corrected = [.. values[..index], .. values[(index + 1)..]];
        var rest = new ParsedInvocation(ParseResult.RootCommandResult.Command.Parse(corrected, InvocationParser.Configuration), _globals);
        // The rest's own mistake, such as a mistyped option, is named beside its problem, so one
        // error corrects both.
        Refusal? others = rest.Problems();
        string[] problems = others is null ? [] : WithAside(others.Problems, others.Mistake);
        IReadOnlyList<string>? correction = !Declares(Command, token) ? null : others is null ? corrected : others.Corrected;

        return (
            $"'{token}' is not a command of '{string.Join(' ', [DistributionInfo.CommandName, .. parentPath])}'.",
            Mistake.Of(token, siblings.Select(Sibling), meant, keyOf: static name => name[(name.LastIndexOf(' ') + 1)..]),
            correction,
            problems);

        static bool Declares(Command command, string token) => command.MistakenNames().Contains(token, StringComparer.Ordinal);
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

        string meant = token == "help" ? Below("--help") : "--version";
        return ($"'{token}' is not a command of '{Path()}'; {token} is the {meant.Split(' ')[^1]} option.", Mistake.Of(token, [], meant));
    }

    /// <summary>
    /// <paramref name="unknown"/>, an option the command does not accept, among the options it
    /// does: its own, the recursive ones of its parents and the root's, compared without their
    /// dashes, with the options that declare it a common mistake for them first. A single letter,
    /// such as <c>-o</c>, most likely abbreviates the options that start with it, and <c>-?</c>
    /// asks for help.
    /// </summary>
    private Mistake UnknownOptionMistake(string unknown)
    {
        Option[] options = AcceptedOptions();
        string[] names = [.. options.Select(static option => option.Name)];
        string[] meant =
        [
            .. options.Where(option => option.MistakenNames().Contains(unknown, StringComparer.Ordinal)).Select(static option => option.Name),
            .. unknown switch
            {
                "-?" or "/?" => ["--help"],
                ['-', var letter] when char.IsAsciiLetter(letter) =>
                    names.Where(name => name.StartsWith("--" + letter, StringComparison.Ordinal)),
                _ => [],
            },
        ];
        return Mistake.Of(unknown, names, meant, keyOf: static name => name.TrimStart('-'));
    }

    /// <summary>
    /// A stray argument beside a missing required option that a bare argument may stand for,
    /// such as <c>search deck.pptx speaker</c> for <c>--pattern speaker</c>: it suggests that
    /// option; null otherwise.
    /// </summary>
    private Mistake? StrayArgumentMistake()
    {
        Option[] options = AcceptedOptions();
        return ParseResult.UnmatchedTokens.FirstOrDefault() is { } stray
            && options.FirstOrDefault(option => option.IsMistakenForArgument() && ParseResult.GetResult(option) is not OptionResult { IdentifierToken: not null }) is { } missing
            ? Mistake.Of(stray, [.. options.Select(static option => option.Name)], missing.Name, keyOf: static name => name.TrimStart('-'))
            : null;
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
    /// The first token beside <c>--version</c> that the parser leaves over, such as
    /// <c>--version --bogus</c>: the version is a terminating option, so the parser runs it
    /// without reporting the token; null when there is none.
    /// </summary>
    private string? VersionWithOthers() =>
        CommandPath.Count == 0
        && ParseResult.RootCommandResult.Children.OfType<OptionResult>().Any(static result => result.Option is VersionOption)
            ? ParseResult.UnmatchedTokens.FirstOrDefault()
            : null;

    /// <summary>
    /// <paramref name="error"/> when it refuses a value given to an option or argument, rather
    /// than a missing value or more values than it takes: a value outside its list, such as a
    /// mistyped <c>--detail</c>, whose problem replaces the parser's message, which lists the
    /// values on lines of their own; a number that does not parse; or a value a validator
    /// refused. Null for any other error.
    /// </summary>
    private RefusedValue? Refused(ParseError error)
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

        string parameter = option?.Name ?? argument!.Name;
        string name = option?.Name ?? $"<{argument!.Name}>";
        IReadOnlyList<string> allowed = option is not null ? OptionCompletions.Read(option) : OptionCompletions.Read(argument!);
        if (allowed.Count > 0
            && tokens.Select(static token => token.Value).LastOrDefault(value => !allowed.Contains(value, StringComparer.Ordinal)) is { } value)
        {
            return new($"'{value}' is not a value of {name}.", parameter, Mistake.Of(value, allowed, MeantValue(option, value, allowed)));
        }

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
    /// The value a refused one most likely stands for: <c>--to</c> with a file format given to
    /// <c>--output-format</c>, the value the option declares the refused one stands for, such
    /// as <c>md</c> for <c>--to markdown</c>, or, for <c>all</c> given to an option that takes
    /// several values, every value at once; null otherwise.
    /// </summary>
    private string? MeantValue(Option? option, string value, IReadOnlyList<string> allowed) => option switch
    {
        null => null,
        { Name: GlobalOptionNames.OutputFormat } => TargetFormat(value),
        _ when option.MeantValue(value) is { } meant => meant,
        { Arity.MaximumNumberOfValues: > 1 } when string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            => $"{option.Name} {string.Join(' ', allowed)}",
        _ => null,
    };

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
    internal static readonly ParserConfiguration Configuration = new() { ResponseFileTokenReplacer = null };

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
