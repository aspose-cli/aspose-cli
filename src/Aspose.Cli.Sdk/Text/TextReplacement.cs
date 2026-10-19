using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Text;

/// <summary>
/// The find-and-replace every product's replace_text shares: a literal compared ordinally (ignoring
/// letter case unless asked) or a culture-invariant regular expression with a hard timeout, whole
/// words, substitutions and a replacement budget. Products only walk their own text units.
/// </summary>
public sealed class TextReplacement
{
    private const string RegexHint =
        "Escape the characters . $ ^ { [ ( | ) * + ? \\ with a backslash, or set regex to false for literal text.";

    private readonly string _find;
    private readonly string _replace;
    private readonly bool _caseSensitive;
    private readonly bool _wholeWord;
    private readonly Regex? _expression;

    private TextReplacement(string find, string replace, bool caseSensitive, bool wholeWord, Regex? expression)
    {
        _find = find;
        _replace = replace;
        _caseSensitive = caseSensitive;
        _wholeWord = wholeWord;
        _expression = expression;
    }

    /// <summary>
    /// Why an operation's find cannot be used, with a recovery hint, or null when it can: find
    /// must be non-empty, and a regular expression must compile and must not match an empty
    /// string, as a search's pattern must not.
    /// </summary>
    public static (string Reason, string Hint)? Problem(string find, bool regex, bool caseSensitive)
    {
        ArgumentNullException.ThrowIfNull(find);
        if (find.Length == 0)
        {
            return ("find is empty", "Set find to the text or regular expression to replace.");
        }

        if (!regex)
        {
            return null;
        }

        Regex expression;
        try
        {
            expression = SafeRegex.Create(find, caseSensitive);
        }
        catch (ArgumentException exception)
        {
            return ($"find is not a valid .NET regular expression: {exception.Message}", RegexHint);
        }

        return expression.IsMatch(string.Empty)
            ? ("find matches an empty string", "Use an expression that matches at least one character, such as x+ rather than x*.")
            : null;
    }

    /// <summary>Creates a replacement whose find has no <see cref="Problem"/>.</summary>
    /// <param name="find">The literal text, or the regular expression when <paramref name="regex"/> is true.</param>
    /// <param name="replace">The replacement; with a regular expression it honors substitutions such as $1, and is inserted verbatim otherwise.</param>
    /// <param name="regex">Whether <paramref name="find"/> is a regular expression.</param>
    /// <param name="caseSensitive">Whether matching distinguishes letter case.</param>
    /// <param name="wholeWord">Whether a match counts only when no letter, decimal digit or underscore adjoins it on either side.</param>
    /// <exception cref="ArgumentException"><paramref name="find"/> has a <see cref="Problem"/>.</exception>
    public static TextReplacement Create(string find, string replace, bool regex, bool caseSensitive, bool wholeWord)
    {
        ArgumentNullException.ThrowIfNull(replace);
        if (Problem(find, regex, caseSensitive) is { } problem)
        {
            throw new ArgumentException(problem.Reason, nameof(find));
        }

        return new TextReplacement(find, replace, caseSensitive, wholeWord, regex ? SafeRegex.Create(find, caseSensitive) : null);
    }

    /// <summary>
    /// The first <paramref name="limit"/> matches in <paramref name="text"/>, in order and without
    /// overlap, each with the text that replaces it. A whole-word replacement keeps the matches
    /// that are whole words: it neither retries a regular expression's other alternatives nor
    /// grows a match into one.
    /// </summary>
    /// <exception cref="CliException"><c>OPERATION_TIMEOUT</c> when the expression exceeds its budget.</exception>
    public IReadOnlyList<(int Start, int Length, string Replacement)> Matches(string text, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        var matches = new List<(int Start, int Length, string Replacement)>();
        if (limit == 0)
        {
            return matches;
        }

        if (_expression is not null)
        {
            for (Match match = Evaluate(() => _expression.Match(text)); match.Success; match = Next(match))
            {
                if (match.Length > 0 && (!_wholeWord || IsWholeWord(text, match.Index, match.Length)))
                {
                    matches.Add((match.Index, match.Length, match.Result(_replace)));
                    if (matches.Count == limit)
                    {
                        break;
                    }
                }
            }

            return matches;
        }

        StringComparison comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (int found = text.IndexOf(_find, comparison); found >= 0 && matches.Count < limit;)
        {
            if (_wholeWord && !IsWholeWord(text, found, _find.Length))
            {
                found = text.IndexOf(_find, found + 1, comparison);
                continue;
            }

            matches.Add((found, _find.Length, _replace));
            found = text.IndexOf(_find, found + _find.Length, comparison);
        }

        return matches;
    }

    /// <summary>
    /// Replaces the first <paramref name="limit"/> <see cref="Matches"/> in <paramref name="text"/>
    /// and returns the result with how many it replaced.
    /// </summary>
    /// <exception cref="CliException"><c>OPERATION_TIMEOUT</c> when the expression exceeds its budget.</exception>
    public (string Text, int Replaced) Replace(string text, int limit)
    {
        IReadOnlyList<(int Start, int Length, string Replacement)> matches = Matches(text, limit);
        if (matches.Count == 0)
        {
            return (text, 0);
        }

        var result = new StringBuilder(text.Length);
        int copied = 0;
        foreach ((int start, int length, string replacement) in matches)
        {
            result.Append(text, copied, start - copied).Append(replacement);
            copied = start + length;
        }

        return (result.Append(text, copied, text.Length - copied).ToString(), matches.Count);
    }

    /// <summary>Whether no letter, decimal digit or underscore adjoins the text at <paramref name="start"/> on either side.</summary>
    private static bool IsWholeWord(string text, int start, int length) =>
        (start == 0 || !IsWordCharacter(text[start - 1]))
        && (start + length >= text.Length || !IsWordCharacter(text[start + length]));

    // The class [\p{L}\p{Nd}_].
    private static bool IsWordCharacter(char character) =>
        character == '_' || char.IsLetter(character)
        || char.GetUnicodeCategory(character) == UnicodeCategory.DecimalDigitNumber;

    private static Match Next(Match match) => Evaluate(match.NextMatch);

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
