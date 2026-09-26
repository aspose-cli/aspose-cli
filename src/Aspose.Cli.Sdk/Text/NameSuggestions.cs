namespace Aspose.Cli.Sdk.Text;

/// <summary>
/// Ranks the existing names closest to a name that was not found, so a caller can correct a
/// typo or a casing slip in one step. Ranking is deterministic: a match that ignores case and
/// surrounding white space comes first, then names that contain the request or are contained
/// in it, then names within a small edit distance; ties keep the candidates' order.
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
    public static IReadOnlyList<string> Closest(string requested, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(candidates);

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
                : Overlaps(wanted, name) ? 1
                : Distance(wanted, name, allowedDistance) is int distance ? 1 + distance
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

    // One name contains the other, so a request such as "Sales" finds "Sales 2026".
    private static bool Overlaps(string left, string right) =>
        Math.Min(left.Length, right.Length) >= MinimumOverlap
        && (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal));

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
