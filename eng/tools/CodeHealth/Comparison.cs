namespace Aspose.Cli.CodeHealth;

/// <summary>How a member or file differs between the base and the current source.</summary>
internal enum ChangeKind
{
    /// <summary>Only the current source has it.</summary>
    New,

    /// <summary>Its cognitive complexity, or at equal complexity its length, grew.</summary>
    Worse,

    /// <summary>Its cognitive complexity, or at equal complexity its length, fell.</summary>
    Better,

    /// <summary>Only the base has it.</summary>
    Removed,
}

/// <summary>One changed member; a value is 0 on the side that lacks the member.</summary>
internal sealed record MemberChange(
    string Key, ChangeKind Kind, int BaseCognitive, int HeadCognitive, int BaseLines, int HeadLines, int BaseParameters, int HeadParameters);

/// <summary>One changed file; a value is 0 on the side that lacks the file.</summary>
internal sealed record FileChange(string Path, ChangeKind Kind, int BaseCognitive, int HeadCognitive, int BaseLines, int HeadLines);

/// <summary>The member and file differences between two snapshots, each list in report order.</summary>
internal sealed record Comparison(Totals Base, Totals Head, IReadOnlyList<MemberChange> Members, IReadOnlyList<FileChange> Files, IReadOnlyList<Clone> NewClones)
{
    /// <summary>Compares <paramref name="head"/> with <paramref name="baseline"/>; unchanged entries are left out.</summary>
    public static Comparison Create(Snapshot baseline, Snapshot head)
    {
        Dictionary<string, MemberMetrics> baseMembers = baseline.Members.ToDictionary(member => member.Key, StringComparer.Ordinal);
        Dictionary<string, MemberMetrics> headMembers = head.Members.ToDictionary(member => member.Key, StringComparer.Ordinal);
        List<MemberChange> members = [];
        foreach (string key in baseMembers.Keys.Union(headMembers.Keys, StringComparer.Ordinal))
        {
            MemberMetrics? before = baseMembers.GetValueOrDefault(key);
            MemberMetrics? after = headMembers.GetValueOrDefault(key);
            if (Classify(before, after, member => member.Cognitive, member => member.Lines, member => member.Parameters) is ChangeKind kind)
            {
                members.Add(new MemberChange(key, kind,
                    before?.Cognitive ?? 0, after?.Cognitive ?? 0, before?.Lines ?? 0, after?.Lines ?? 0,
                    before?.Parameters ?? 0, after?.Parameters ?? 0));
            }
        }

        Dictionary<string, FileMetrics> baseFiles = baseline.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        Dictionary<string, FileMetrics> headFiles = head.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        List<FileChange> files = [];
        foreach (string path in baseFiles.Keys.Union(headFiles.Keys, StringComparer.Ordinal))
        {
            FileMetrics? before = baseFiles.GetValueOrDefault(path);
            FileMetrics? after = headFiles.GetValueOrDefault(path);
            if (Classify(before, after, file => file.Cognitive, file => file.Lines, _ => 0) is ChangeKind kind)
            {
                files.Add(new FileChange(path, kind, before?.Cognitive ?? 0, after?.Cognitive ?? 0, before?.Lines ?? 0, after?.Lines ?? 0));
            }
        }

        HashSet<string> baseClones = baseline.Clones.Select(clone => clone.Key).ToHashSet(StringComparer.Ordinal);
        return new Comparison(
            baseline.Totals(),
            head.Totals(),
            members
                .OrderBy(change => change.Kind)
                .ThenByDescending(change => Weight(change.Kind, change.BaseCognitive, change.HeadCognitive))
                .ThenByDescending(change => Weight(change.Kind, change.BaseLines, change.HeadLines))
                .ThenBy(change => change.Key, StringComparer.Ordinal)
                .ToList(),
            files
                .OrderBy(change => change.Kind)
                .ThenByDescending(change => Weight(change.Kind, change.BaseCognitive, change.HeadCognitive))
                .ThenByDescending(change => Weight(change.Kind, change.BaseLines, change.HeadLines))
                .ThenBy(change => change.Path, StringComparer.Ordinal)
                .ToList(),
            head.Clones.Where(clone => !baseClones.Contains(clone.Key))
                .OrderByDescending(clone => clone.Tokens)
                .ThenBy(clone => clone.Key, StringComparer.Ordinal)
                .ToList());
    }

    /// <summary>The kind of difference, comparing each measure in turn, or null when they are equal.</summary>
    private static ChangeKind? Classify<T>(T? before, T? after, params Func<T, int>[] measures)
        where T : class
    {
        if (before is null)
        {
            return ChangeKind.New;
        }
        if (after is null)
        {
            return ChangeKind.Removed;
        }
        foreach (Func<T, int> measure in measures)
        {
            int delta = measure(after) - measure(before);
            if (delta != 0)
            {
                return delta > 0 ? ChangeKind.Worse : ChangeKind.Better;
            }
        }
        return null;
    }

    /// <summary>How much a change weighs within its kind: the new or removed value, or the size of the delta.</summary>
    private static int Weight(ChangeKind kind, int before, int after) => kind switch
    {
        ChangeKind.New => after,
        ChangeKind.Removed => before,
        _ => Math.Abs(after - before),
    };
}
