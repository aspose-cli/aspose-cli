using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>The mistakes of a command line that only a second look at its tokens recognises (see <see cref="Problems"/>).</summary>
internal sealed partial class ParsedInvocation
{
    /// <summary>
    /// A value of a global option refused beside <c>--help</c>. Help stops the parser before it
    /// checks values, so the value would fail only when the option is read; it is refused as the
    /// same command line without <c>--help</c> refuses it. Null when no help was asked for or
    /// every global value is accepted.
    /// </summary>
    private Refusal? RefusedGlobalBesideHelp()
    {
        Command root = ParseResult.RootCommandResult.Command;
        if (root.Options.OfType<HelpOption>().FirstOrDefault() is not { } help || ParseResult.GetResult(help) is null)
        {
            return null;
        }

        string[] names = [help.Name, .. help.Aliases];
        ParsedInvocation line = Reparsed([.. ParseResult.Tokens.Select(static token => token.Value).Where(value => !names.Contains(value, StringComparer.Ordinal))]);
        RefusedValue[] refused = [.. line.ParseResult.Errors
            .Select(line.Refused)
            .OfType<RefusedValue>()
            .Where(value => root.Options.Any(option => option.Name == value.Parameter))];
        return refused.Length == 0
            ? null
            : new([.. refused.Select(static value => value.Problem)], refused[0].Mistake, refused[0].Parameter, ValuesOnly: true) { Hint = refused[0].Hint };
    }

    /// <summary>
    /// An option written without the value it needs, at the end of the command line or before
    /// another option; the values it can take, when it has a list of them. Null when none lacks
    /// its value (<see cref="LacksValue"/>).
    /// </summary>
    private Refusal? MissingValue()
    {
        foreach (OptionResult result in OptionResults())
        {
            if (result.Option.Arity.MinimumNumberOfValues > 0 && LacksValue(result))
            {
                IReadOnlyList<string> allowed = OptionCompletions.Read(result.Option);
                return new(
                    [$"{result.Option.Name} needs a value."],
                    allowed.Count > 0 ? Mistake.Of(string.Empty, allowed) : null,
                    result.Option.Name,
                    ValuesOnly: false);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether an option that needs a value was given none: no token at all, or the name of
    /// another option of the command, which the parser hands it as its value whatever it is, so
    /// that a value may start with a dash. That name stands for a missing value when the option
    /// has a list of values it is not one of, which the parser does not check for a name it knows,
    /// or when the command line fails because of it, that is, when it fails less without this
    /// option; otherwise it is the value.
    /// </summary>
    private bool LacksValue(OptionResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return true;
        }

        string[] names = [.. AcceptedOptions().SelectMany(static option => option.Aliases.Prepend(option.Name))];
        if (result.Tokens.FirstOrDefault(token => names.Contains(token.Value, StringComparer.Ordinal)) is not { } taken)
        {
            return false;
        }

        if (OptionCompletions.Read(result.Option) is { Count: > 0 } allowed)
        {
            return !allowed.Contains(taken.Value, StringComparer.Ordinal);
        }

        if (ParseResult.Errors.Count == 0)
        {
            return false;
        }

        Token[] tokens = [.. ParseResult.Tokens];
        int value = Array.FindIndex(tokens, token => ReferenceEquals(token, taken));
        string[] values = [.. tokens.Select(static token => token.Value)];
        ParsedInvocation without = Reparsed([.. values[..(value - 1)], .. values[value..]]);
        return without.ParseResult.Errors.Count - (result.Option.Required ? 1 : 0) < ParseResult.Errors.Count;
    }

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
    /// is one value too many for that option. Null for a token that follows no option's value,
    /// and beside a missing option a bare argument may stand for
    /// (<see cref="StrayArgumentMistake"/>), which is what such a token is then, unless the option
    /// holds a secret, of which the token may be part.
    /// </summary>
    private Option? OptionWithValueBefore(string leftOver)
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
                return holder.Option.GetParameterMetadata().Secret || StrayArgumentMistake() is null ? holder.Option : null;
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence for a value one too many for <paramref name="option"/>; the value of an option
    /// that holds a secret may be part of it, so it is not repeated.
    /// </summary>
    private static string ExtraValueMessage(Option option, string value)
    {
        string extra = option.GetParameterMetadata().Secret
            ? "the value after it, which is not repeated in case it is a secret,"
            : $"'{value}'";
        return option.Arity.MaximumNumberOfValues > 1
            ? $"{option.Name} takes one value each time it is given; {extra} is left over."
            : $"{option.Name} takes one value; {extra} is one too many.";
    }

    /// <summary>What to do about a value one too many for <paramref name="option"/>.</summary>
    private static string OneValueHint(Option option) =>
        option.Arity.MaximumNumberOfValues > 1
            ? $"Repeat {option.Name} for each value; quote a value that holds spaces."
            : $"Give only one value to {option.Name}; quote a value that holds spaces.";

    /// <summary>
    /// The command line that runs the command of the root <paramref name="token"/>, typed below a
    /// product, stands for, without the product's name: a command, or the command of a group, that
    /// reads a file of any product, as <c>review</c> and <c>preview open</c> do, or the command
    /// of that group the next word names. A file the product written is not its default owner
    /// keeps that product with <c>--product</c>. Null for any other token.
    /// </summary>
    private string? RootCommandLine(string token)
    {
        if (ProductId is null || CommandPath.Count != 1
            || ParseResult.RootCommandResult.Command.Subcommands.FirstOrDefault(command => !command.Hidden && command.Name == token) is not { } named
            || (ReadsAnyProduct(named) ? named : named.Subcommands.FirstOrDefault(ReadsAnyProduct)) is not { } reader)
        {
            return null;
        }

        string[] values = [.. ParseResult.Tokens.Select(static item => item.Value)];
        int group = Array.IndexOf(values, CommandPath[0].Name);
        string[] rest = values[(Array.IndexOf(values, token, group + 1) + 1)..];
        Command? written = rest.Length > 0 ? named.Subcommands.FirstOrDefault(command => !command.Hidden && command.Name == rest[0]) : null;
        Command target = written ?? reader;
        string[] path = target == named ? [token] : [token, target.Name];
        string[] line = [.. values[..group], .. path, .. written is null ? rest : rest[1..]];
        var template = new ContinuationCommand(path);
        if (!ReadsAnyProduct(target))
        {
            return Repeated(line, template);
        }

        template.Placeholder("file");
        ParsedInvocation parsed = Reparsed(line);
        if (parsed.ParseResult.GetResult(target.Arguments.First(IsFile)) is not { Tokens: [var file, ..] })
        {
            return template.ToString();
        }

        if (parsed.ParseResult.GetResult(target.Options.First(static option => option.Name == ProductOption)) is null
            && !OwnedByDefault(file.Value, ProductId))
        {
            template.Option(ProductOption, ProductId);
            line = [.. line, ProductOption, ProductId];
        }

        return Repeated(line, template);
    }

    /// <summary>The option of a command that reads a file of any product that chooses the product.</summary>
    private const string ProductOption = "--product";

    /// <summary>Whether a command reads a file of any product: it takes a file and lets the caller choose the product.</summary>
    private static bool ReadsAnyProduct(Command command) =>
        !command.Hidden
        && command.Arguments.Any(IsFile)
        && command.Options.Any(static option => option.Name == ProductOption);

    /// <summary>Whether an argument names a file the command reads.</summary>
    private static bool IsFile(Argument argument) => argument.GetParameterMetadata().InputKind == InputKind.File;

    /// <summary>Whether the file's extension routes it to <paramref name="product"/> when no <c>--product</c> is given; true when the catalog is unknown.</summary>
    private bool OwnedByDefault(string file, string product) =>
        _catalog is null
        || (System.IO.Path.GetExtension(file) is { Length: > 1 } extension
            && _catalog.TryGetDefaultOwner(extension, out ProductDefinition? owner)
            && owner!.Manifest.Id == product);

    /// <summary>
    /// An option the command refuses that stands for one of its arguments, such as the
    /// <c>--out</c> of a <c>create</c>: the command line with its value as that argument is the
    /// suggestion, and no option that merely looks like it. A token some option holds as its
    /// value is that value, and after a <c>--</c> token the argument may hold anything; a token
    /// the argument took, such as <c>-o=new.xlsx</c>, parses and is still the option. Null when
    /// no such option was given.
    /// </summary>
    private Refusal? OutputOptionOfAnArgument()
    {
        if (ParseResult.Tokens.Any(static token => token.Type == TokenType.DoubleDash))
        {
            return null;
        }

        Token[] tokens = [.. ParseResult.Tokens];
        for (int index = 0; index < tokens.Length; index++)
        {
            string name = OptionName(tokens[index].Value);
            if (!TakenByAnOption(tokens[index])
                && Command.RefusedOptions().FirstOrDefault(option => option.Argument is not null && option.Names.Contains(name, StringComparer.Ordinal)) is { } refused)
            {
                return new(
                    [$"'{name}' is not an option of '{Path()}'."],
                    Mistake.Of(name, [], [WithValueAsArgument(refused, tokens, index)], closest: false),
                    name,
                    ValuesOnly: false)
                {
                    Hint = refused.Hint,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// The command line with the value of the refused option at <paramref name="index"/> as the
    /// argument it stands for, or the command with a placeholder for that argument when the
    /// option has no value, the argument is given as well or the line would not parse.
    /// </summary>
    private string WithValueAsArgument(RefusedOption refused, Token[] tokens, int index)
    {
        var template = new ContinuationCommand([.. CommandPath.Select(static command => command.Name)]).Placeholder(refused.Argument!);
        string text = tokens[index].Value;
        string name = OptionName(text);
        bool joined = name.Length < text.Length;
        string? value = joined ? text[(name.Length + 1)..]
            : index + 1 < tokens.Length && !tokens[index + 1].Value.StartsWith('-') && !TakenByAnOption(tokens[index + 1]) ? tokens[index + 1].Value
            : null;
        int after = Array.FindLastIndex(tokens, static token => token.Type == TokenType.Command) + 1;
        Argument argument = Command.Arguments.First(candidate => candidate.Name == refused.Argument);
        bool given = ParseResult.GetResult(argument) is { Tokens: [var first, ..] } && !ReferenceEquals(first, tokens[index]);
        if (value is null || given || index < after)
        {
            return template.ToString();
        }

        string[] values = [.. tokens.Select(static token => token.Value)];
        return Repeated([.. values[..after], value, .. values[after..index], .. values[(index + (joined ? 1 : 2))..]], template);
    }

    /// <summary>
    /// The command line <paramref name="tokens"/> hold, as the builder writes a command to run, or
    /// <paramref name="fallback"/> when they do not parse or read a document from stdin, which a
    /// suggestion, a command line alone, cannot say to pipe again (<see cref="ContinuationCommand.Repeating"/>).
    /// </summary>
    private string Repeated(IReadOnlyList<string> tokens, ContinuationCommand fallback)
    {
        ParsedInvocation line = Reparsed(tokens);
        return (line.Problems() is null && ContinuationCommand.Repeating(line.ParseResult) is { Piped: null } again ? again.Command : fallback).ToString();
    }
}
