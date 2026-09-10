using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Text;

/// <summary>Shared, timeout-safe primitives for product text search.</summary>
public static class TextSearch
{
    /// <summary>
    /// Creates a bounded regular expression and rejects expressions that match
    /// an empty string, or returns <see langword="null"/> for literal search.
    /// </summary>
    public static Regex? CreateRegex(
        bool enabled,
        string pattern,
        bool caseSensitive,
        string option = "--pattern",
        string hint = "Fix the regular expression syntax.")
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (!enabled)
        {
            return null;
        }

        try
        {
            Regex regex = SafeRegex.Create(pattern, caseSensitive);
            if (regex.IsMatch(string.Empty))
            {
                throw new ArgumentException(
                    "The regular expression must not match an empty string.");
            }

            return regex;
        }
        catch (ArgumentException exception)
        {
            throw CliErrors.OptionInvalid(
                option,
                exception.Message,
                hint);
        }
    }

    /// <summary>Finds every non-empty regex or literal occurrence in order.</summary>
    public static IReadOnlyList<(int Start, int Length)> Find(
        string text,
        string pattern,
        bool caseSensitive,
        Regex? regex,
        string timeoutMessage,
        string timeoutHint)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeoutMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeoutHint);

        if (regex is not null)
        {
            try
            {
                return regex.Matches(text)
                    .Cast<Match>()
                    .Where(static match => match.Length > 0)
                    .Select(static match => (match.Index, match.Length))
                    .ToArray();
            }
            catch (RegexMatchTimeoutException exception)
            {
                throw new CliException(
                    ErrorCodes.OperationTimeout,
                    timeoutMessage,
                    hint: timeoutHint,
                    innerException: exception);
            }
        }

        if (pattern.Length == 0)
        {
            return [];
        }

        StringComparison comparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var matches = new List<(int Start, int Length)>();
        int offset = 0;
        while (offset <= text.Length - pattern.Length)
        {
            int found = text.IndexOf(pattern, offset, comparison);
            if (found < 0)
            {
                break;
            }

            matches.Add((found, pattern.Length));
            offset = found + pattern.Length;
        }

        return matches;
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
}
