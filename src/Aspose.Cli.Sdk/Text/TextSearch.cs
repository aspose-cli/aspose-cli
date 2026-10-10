using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Text;

/// <summary>
/// One validated text query shared by every product search: a literal compared ordinally or
/// a culture-invariant regular expression with a hard timeout. Only non-empty matches count.
/// </summary>
public sealed class TextSearch
{
    private const string PatternOption = "--pattern";

    private TextSearch(string pattern, bool caseSensitive, Regex? expression)
    {
        Pattern = pattern;
        CaseSensitive = caseSensitive;
        Expression = expression;
    }

    /// <summary>The literal text or regular expression as the caller supplied it.</summary>
    public string Pattern { get; }

    /// <summary>Whether matching distinguishes letter case.</summary>
    public bool CaseSensitive { get; }

    /// <summary>The bounded regular expression, or null for a literal search.</summary>
    public Regex? Expression { get; }

    /// <summary>
    /// Validates a query: the pattern must be non-empty, and a regular expression must compile
    /// and must not match an empty string.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> naming <c>--pattern</c>.</exception>
    public static TextSearch Create(string pattern, bool regex, bool caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0)
        {
            throw CliErrors.OptionInvalid(
                PatternOption,
                "the pattern is empty",
                "Pass the text or regular expression to find.");
        }

        if (!regex)
        {
            return new TextSearch(pattern, caseSensitive, null);
        }

        Regex expression;
        try
        {
            expression = SafeRegex.Create(pattern, caseSensitive);
        }
        catch (ArgumentException exception)
        {
            throw CliErrors.OptionInvalid(
                PatternOption,
                $"invalid regular expression: {exception.Message}",
                "Fix the expression, or drop --regex for a literal search.");
        }

        if (expression.IsMatch(string.Empty))
        {
            throw CliErrors.OptionInvalid(
                PatternOption,
                "the regular expression must not match an empty string",
                "Use an expression that matches at least one character.");
        }

        return new TextSearch(pattern, caseSensitive, expression);
    }

    /// <summary>Returns whether the text contains at least one non-empty match.</summary>
    /// <exception cref="CliException"><c>OPERATION_TIMEOUT</c> when the expression exceeds its budget.</exception>
    public bool IsMatch(string text) => Matches(text).Any();

    /// <summary>Finds every non-empty occurrence in order.</summary>
    /// <exception cref="CliException"><c>OPERATION_TIMEOUT</c> when the expression exceeds its budget.</exception>
    public IReadOnlyList<(int Start, int Length)> Find(string text) => Matches(text).ToArray();

    /// <summary>
    /// Returns <paramref name="text"/> unchanged when it has at most <paramref name="length"/>
    /// characters, or its first <paramref name="length"/> characters followed by an ellipsis.
    /// </summary>
    public static string Truncate(string text, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return text.Length <= length ? text : text[..length] + "\u2026";
    }

    /// <summary>Builds a bounded context excerpt around one match.</summary>
    public static string Preview(
        string text,
        int start,
        int length,
        int radius,
        string ellipsis = "…")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        ArgumentNullException.ThrowIfNull(ellipsis);

        if (start > text.Length || length > text.Length - start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "The match must be inside the source text.");
        }

        int from = Math.Max(0, start - radius);
        int to = Math.Min(text.Length, start + length + radius);
        return (from > 0 ? ellipsis : string.Empty)
            + text[from..to]
            + (to < text.Length ? ellipsis : string.Empty);
    }

    private IEnumerable<(int Start, int Length)> Matches(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Expression is null ? LiteralMatches(text) : RegexMatches(text);
    }

    private IEnumerable<(int Start, int Length)> LiteralMatches(string text)
    {
        StringComparison comparison = CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int offset = 0;
        while (offset <= text.Length - Pattern.Length)
        {
            int found = text.IndexOf(Pattern, offset, comparison);
            if (found < 0)
            {
                yield break;
            }

            yield return (found, Pattern.Length);
            offset = found + Pattern.Length;
        }
    }

    private IEnumerable<(int Start, int Length)> RegexMatches(string text)
    {
        Match match = Evaluate(() => Expression!.Match(text));
        while (match.Success)
        {
            if (match.Length > 0)
            {
                yield return (match.Index, match.Length);
            }

            Match current = match;
            match = Evaluate(current.NextMatch);
        }
    }

    private static Match Evaluate(Func<Match> next)
    {
        try
        {
            return next();
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw CliErrors.RegexTimeout(exception);
        }
    }
}
