namespace Aspose.Cli.Sdk.Text;

/// <summary>
/// Ranks the existing names closest to a name that was not found, so a caller can correct a
/// typo or a casing slip in one step. Ranking is deterministic: a match that ignores case and
/// surrounding white space comes first, then names that contain the request or are a whole
/// word of it (for field names, those ending the other first), then names within a small edit
/// distance; ties keep the candidates' order.
/// </summary>
public static class NameSuggestions
{
    /// <summary>The most suggestions returned for one request.</summary>
    public const int MaximumSuggestions = 3;

    // Suggestions are computed over at most this many candidates, which bounds the work for
    // documents with very many names; the error still reports the full count.
    private const int MaximumCandidates = 10_000;

    // The shorter of two names must have this many characters to count as contained in the other.
    private const int MinimumOverlap = 3;

    /// <summary>Returns up to <see cref="MaximumSuggestions"/> candidates close to <paramref name="requested"/>.</summary>
    /// <param name="requested">The name that was not found.</param>
    /// <param name="candidates">The existing names, in the order ties keep.</param>
    /// <param name="fieldNames">
    /// Whether the names are compound field names such as <c>fontSize</c>, whose last word says
    /// what the field is: a contained name that ends the other then ranks before one that does not.
    /// </param>
    public static IReadOnlyList<string> Closest(string requested, IEnumerable<string> candidates, bool fieldNames = false)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(candidates);

        string trimmed = requested.Trim();
        string wanted = Normalize(requested);
        if (wanted.Length == 0)
        {
            return [];
        }

        int allowedDistance = Math.Max(1, wanted.Length / 3);
        var ranked = new List<(int Rank, int Order, string Name)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int order = 0;
        foreach (string candidate in candidates.Take(MaximumCandidates))
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
            {
                continue;
            }

            string name = Normalize(candidate);
            int? rank = name == wanted ? 0
                : Overlaps(trimmed, candidate.Trim()) ? (fieldNames && !EndsWithEither(wanted, name) ? 2 : 1)
                : Distance(wanted, name, allowedDistance) is int distance ? 2 + distance
                : null;
            if (rank is int value)
            {
                ranked.Add((value, order, candidate));
            }

            order++;
        }

        return ranked
            .OrderBy(static item => item.Rank)
            .ThenBy(static item => item.Order)
            .Take(MaximumSuggestions)
            .Select(static item => item.Name)
            .ToArray();
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    // The name contains the request, so a request such as "Sales" finds "Sales 2026", or it is a
    // whole word of the request: "fontSize" holds the field "size", but "subtitle" is another
    // word than "title".
    private static bool Overlaps(string requested, string name)
    {
        if (Math.Min(requested.Length, name.Length) < MinimumOverlap)
        {
            return false;
        }

        if (name.Contains(requested, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int start = requested.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            start >= 0;
            start = requested.IndexOf(name, start + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (IsWordBoundary(requested, start) && IsWordBoundary(requested, start + name.Length))
            {
                return true;
            }
        }

        return false;
    }

    // A word starts or ends at either end of the text, next to a character that is not a cased
    // letter, and where a lower-case letter meets an upper-case one, as in "fontSize". Scripts
    // without case, such as Chinese, mark no word edges, so any character may start a word.
    private static bool IsWordBoundary(string text, int index) =>
        index == 0 || index == text.Length
        || !IsCased(text[index - 1]) || !IsCased(text[index])
        || (char.IsLower(text[index - 1]) && char.IsUpper(text[index]));

    private static bool IsCased(char value) => char.IsLower(value) || char.IsUpper(value);

    // The last word of a compound field name says what it is: "fontSize" is a size, not a font.
    private static bool EndsWithEither(string left, string right) =>
        left.EndsWith(right, StringComparison.Ordinal) || right.EndsWith(left, StringComparison.Ordinal);

    /// <summary>
    /// Optimal string alignment distance between two strings, or null when it exceeds
    /// <paramref name="limit"/>; a transposition of adjacent characters counts as one edit.
    /// </summary>
    private static int? Distance(string left, string right, int limit)
    {
        if (Math.Abs(left.Length - right.Length) > limit)
        {
            return null;
        }

        var above = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        var twoAbove = new int[right.Length + 1];
        for (int column = 0; column <= right.Length; column++)
        {
            current[column] = column;
        }

        for (int line = 1; line <= left.Length; line++)
        {
            (above, current, twoAbove) = (current, twoAbove, above);
            current[0] = line;
            int best = current[0];
            for (int column = 1; column <= right.Length; column++)
            {
                int cost = left[line - 1] == right[column - 1] ? 0 : 1;
                int value = Math.Min(
                    Math.Min(above[column] + 1, current[column - 1] + 1),
                    above[column - 1] + cost);
                if (line > 1 && column > 1
                    && left[line - 1] == right[column - 2]
                    && left[line - 2] == right[column - 1])
                {
                    value = Math.Min(value, twoAbove[column - 2] + 1);
                }

                current[column] = value;
                best = Math.Min(best, value);
            }

            if (best > limit)
            {
                return null;
            }
        }

        return current[right.Length] <= limit ? current[right.Length] : null;
    }
}
