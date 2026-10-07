using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspose.Cli.CodeHealth;

/// <summary>
/// Finds exact token clones across source files.
/// <para>
/// Each file is reduced to its token texts in order, without trivia (whitespace, comments,
/// inactive preprocessor regions) and without <c>using</c> directives, which files share by
/// convention. Identifiers and literals are compared exactly as written: renamed copies are not
/// reported, which keeps the result free of the structural look-alikes (records, guard clauses,
/// switch tables) that normalized matching flags in this code base.
/// </para>
/// <para>
/// A clone is a maximal run of at least <see cref="MinimumTokens"/> identical
/// tokens at two positions, within one file or across two, that cannot be extended to the left or
/// right; two runs in one file never overlap. A run of k identical copies reports every pair.
/// Each clone is located by the unit (see <see cref="SourceMetrics"/>) holding its first token,
/// or else the innermost type, or else the file. The clone key is the two locations in
/// ordinal order joined by <c> &lt;-&gt; </c>, and its value the total tokens of all clones
/// between those two locations.
/// </para>
/// </summary>
internal static class CloneDetector
{
    /// <summary>The shortest run of identical tokens that counts as a clone.</summary>
    public const int MinimumTokens = 70;

    private const ulong HashBase = 1_000_003;

    /// <summary>The clones of <paramref name="sources"/>, in ordinal key order.</summary>
    public static IReadOnlyList<Clone> Detect(IReadOnlyList<ParsedSource> sources, int minimumTokens = MinimumTokens)
    {
        List<int> ids = [];
        List<int> files = [];
        List<SyntaxToken> tokens = [];
        Dictionary<string, int> interned = new(StringComparer.Ordinal);
        int[] fileStart = new int[sources.Count];
        int[] fileEnd = new int[sources.Count];
        for (int file = 0; file < sources.Count; file++)
        {
            fileStart[file] = ids.Count;
            foreach (SyntaxToken token in sources[file].Root.DescendantTokens(static node => node is not UsingDirectiveSyntax))
            {
                if (token.Span.IsEmpty)
                {
                    continue;
                }
                if (!interned.TryGetValue(token.Text, out int id))
                {
                    interned[token.Text] = id = interned.Count + 1;
                }
                ids.Add(id);
                files.Add(file);
                tokens.Add(token);
            }
            fileEnd[file] = ids.Count;
        }

        Dictionary<string, int> totals = new(StringComparer.Ordinal);
        foreach (List<int> group in WindowGroups(ids, files, fileEnd, minimumTokens))
        {
            for (int i = 0; i < group.Count; i++)
            {
                for (int j = i + 1; j < group.Count; j++)
                {
                    int first = group[i];
                    int second = group[j];
                    if (!SameRun(ids, first, second, minimumTokens) || ExtendsLeft(ids, files, fileStart, first, second))
                    {
                        continue;
                    }
                    int length = Extend(ids, files, fileEnd, first, second);
                    if (length < minimumTokens)
                    {
                        continue;
                    }
                    string[] locations =
                    [
                        Locate(sources[files[first]], tokens[first]),
                        Locate(sources[files[second]], tokens[second]),
                    ];
                    Array.Sort(locations, StringComparer.Ordinal);
                    string key = locations[0] + " <-> " + locations[1];
                    totals[key] = totals.GetValueOrDefault(key) + length;
                }
            }
        }
        return totals
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new Clone(entry.Key, entry.Value))
            .ToList();
    }

    /// <summary>Start positions grouped by the hash of the window of tokens they begin, in order.</summary>
    private static IEnumerable<List<int>> WindowGroups(List<int> ids, List<int> files, int[] fileEnd, int window)
    {
        ulong power = 1;
        for (int i = 0; i < window; i++)
        {
            power = unchecked(power * HashBase);
        }
        ulong[] prefix = new ulong[ids.Count + 1];
        for (int i = 0; i < ids.Count; i++)
        {
            prefix[i + 1] = unchecked(prefix[i] * HashBase + (ulong)ids[i]);
        }
        Dictionary<ulong, List<int>> groups = [];
        for (int start = 0; start < ids.Count; start++)
        {
            if (start + window > fileEnd[files[start]])
            {
                continue;
            }
            ulong hash = unchecked(prefix[start + window] - prefix[start] * power);
            if (!groups.TryGetValue(hash, out List<int>? group))
            {
                groups[hash] = group = [];
            }
            group.Add(start);
        }
        return groups.Values.Where(group => group.Count > 1);
    }

    private static bool SameRun(List<int> ids, int first, int second, int length)
    {
        for (int offset = 0; offset < length; offset++)
        {
            if (ids[first + offset] != ids[second + offset])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Whether the clone at the pair also matches one token earlier, so it starts there instead.</summary>
    private static bool ExtendsLeft(List<int> ids, List<int> files, int[] fileStart, int first, int second) =>
        first > fileStart[files[first]] && second > fileStart[files[second]] && ids[first - 1] == ids[second - 1];

    /// <summary>The matching length from the pair, kept inside both files and short of overlapping.</summary>
    private static int Extend(List<int> ids, List<int> files, int[] fileEnd, int first, int second)
    {
        int limit = Math.Min(fileEnd[files[first]] - first, fileEnd[files[second]] - second);
        if (files[first] == files[second])
        {
            limit = Math.Min(limit, second - first);
        }
        int length = 0;
        while (length < limit && ids[first + length] == ids[second + length])
        {
            length++;
        }
        return length;
    }

    private static string Locate(ParsedSource source, SyntaxToken token)
    {
        Dictionary<SyntaxNode, string> units = source.Units
            .Where(unit => unit.Body is not null)
            .ToDictionary(unit => unit.Declaration, unit => unit.Key);
        foreach (SyntaxNode node in token.Parent!.AncestorsAndSelf())
        {
            if (units.TryGetValue(node, out string? key))
            {
                return key;
            }
            if (node is BaseTypeDeclarationSyntax)
            {
                string types = string.Join(".", node.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(SourceMetrics.TypeName));
                return source.Path + "::" + types;
            }
        }
        return source.Path;
    }
}
