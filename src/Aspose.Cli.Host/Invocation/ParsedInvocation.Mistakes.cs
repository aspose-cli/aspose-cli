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
            : new([.. refused.Select(static value => value.Problem)], refused[0].Mistake, refused[0].Parameter, ValuesOnly: true);
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
}
