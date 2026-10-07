namespace Aspose.Cli.CodeHealth;

/// <summary>A file that changes often and is complex: where defects and review effort concentrate.</summary>
/// <param name="Path">The repository-relative path.</param>
/// <param name="Commits">The commits that changed the file in the analyzed range.</param>
/// <param name="Fixes">Those of them whose subject is a <c>fix</c> commit.</param>
/// <param name="Cognitive">The file's summed cognitive complexity.</param>
/// <param name="Score">Commits times cognitive complexity, the ranking key.</param>
internal sealed record Hotspot(string Path, int Commits, int Fixes, int Cognitive, int Score);

/// <summary>The measurements of one source tree, each list ranked and cut to the requested length.</summary>
internal sealed record MeasureReport(
    string Source,
    string Revision,
    string History,
    int MinimumCloneTokens,
    Totals Totals,
    IReadOnlyList<MemberMetrics> MostComplexMembers,
    IReadOnlyList<MemberMetrics> LongestMembers,
    IReadOnlyList<MemberMetrics> MostParameters,
    IReadOnlyList<FileMetrics> LongestFiles,
    IReadOnlyList<Clone> Clones,
    IReadOnlyList<Hotspot> Hotspots)
{
    /// <summary>Ranks <paramref name="snapshot"/>, keeping the first <paramref name="top"/> of each list (0 keeps all).</summary>
    public static MeasureReport Create(
        string source, string revision, string history, Snapshot snapshot, IReadOnlyDictionary<string, FileHistory> changes, int top)
    {
        IEnumerable<MemberMetrics> bodies = snapshot.Members.Where(member => member.HasBody);
        return new MeasureReport(
            source,
            revision,
            history,
            CloneDetector.MinimumTokens,
            snapshot.Totals(),
            Take(bodies.OrderByDescending(member => member.Cognitive).ThenByDescending(member => member.Lines).ThenBy(member => member.Key, StringComparer.Ordinal), top),
            Take(bodies.OrderByDescending(member => member.Lines).ThenByDescending(member => member.Cognitive).ThenBy(member => member.Key, StringComparer.Ordinal), top),
            Take(snapshot.Members.Where(member => member.Parameters > 0).OrderByDescending(member => member.Parameters).ThenBy(member => member.Key, StringComparer.Ordinal), top),
            Take(snapshot.Files.OrderByDescending(file => file.Lines).ThenBy(file => file.Path, StringComparer.Ordinal), top),
            Take(snapshot.Clones.OrderByDescending(clone => clone.Tokens).ThenBy(clone => clone.Key, StringComparer.Ordinal), top),
            Take(RankHotspots(snapshot, changes), top));
    }

    /// <summary>The current files that changed in the range, by commits times complexity.</summary>
    private static IEnumerable<Hotspot> RankHotspots(Snapshot snapshot, IReadOnlyDictionary<string, FileHistory> changes) =>
        snapshot.Files
            .Where(file => changes.ContainsKey(file.Path))
            .Select(file =>
            {
                FileHistory history = changes[file.Path];
                return new Hotspot(file.Path, history.Commits, history.Fixes, file.Cognitive, history.Commits * file.Cognitive);
            })
            .OrderByDescending(hotspot => hotspot.Score)
            .ThenByDescending(hotspot => hotspot.Fixes)
            .ThenBy(hotspot => hotspot.Path, StringComparer.Ordinal);

    /// <summary>The first <paramref name="top"/> items, or all of them when it is 0.</summary>
    public static IReadOnlyList<T> Take<T>(IEnumerable<T> items, int top) => (top == 0 ? items : items.Take(top)).ToList();
}

/// <summary>How the current source differs from a base revision, each kind of change cut to the requested length.</summary>
internal sealed record CompareReport(string Source, string Base, string Head, Totals BaseTotals, Totals HeadTotals,
    ChangeCounts MemberCounts, ChangeCounts FileCounts, IReadOnlyList<MemberChange> Members, IReadOnlyList<FileChange> Files, IReadOnlyList<Clone> NewClones)
{
    /// <summary>Cuts <paramref name="comparison"/> to the first <paramref name="top"/> changes of each kind (0 keeps all).</summary>
    public static CompareReport Create(string source, string baseRevision, string head, Comparison comparison, int top) => new(
        source,
        baseRevision,
        head,
        comparison.Base,
        comparison.Head,
        ChangeCounts.Of(comparison.Members.Select(change => change.Kind)),
        ChangeCounts.Of(comparison.Files.Select(change => change.Kind)),
        comparison.Members.GroupBy(change => change.Kind).SelectMany(group => MeasureReport.Take(group, top)).ToList(),
        comparison.Files.GroupBy(change => change.Kind).SelectMany(group => MeasureReport.Take(group, top)).ToList(),
        MeasureReport.Take(comparison.NewClones, top));
}

/// <summary>How many members or files changed in each way, before any cut.</summary>
internal sealed record ChangeCounts(int New, int Worse, int Better, int Removed)
{
    /// <summary>Counts <paramref name="kinds"/>.</summary>
    public static ChangeCounts Of(IEnumerable<ChangeKind> kinds)
    {
        List<ChangeKind> all = kinds.ToList();
        return new(
            all.Count(kind => kind == ChangeKind.New),
            all.Count(kind => kind == ChangeKind.Worse),
            all.Count(kind => kind == ChangeKind.Better),
            all.Count(kind => kind == ChangeKind.Removed));
    }

    /// <summary>The count of <paramref name="kind"/>.</summary>
    public int this[ChangeKind kind] => kind switch
    {
        ChangeKind.New => New,
        ChangeKind.Worse => Worse,
        ChangeKind.Better => Better,
        _ => Removed,
    };
}
