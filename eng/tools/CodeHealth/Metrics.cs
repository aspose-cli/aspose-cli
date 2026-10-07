namespace Aspose.Cli.CodeHealth;

/// <summary>The metrics of one unit (see <see cref="SourceMetrics"/>).</summary>
/// <param name="Key"><c>path::Type.Member(parameterTypes)</c>, stable across line moves.</param>
/// <param name="Path">The repository-relative path of the file that declares the unit.</param>
/// <param name="HasBody">Whether the unit has a body; a signature-only unit carries just its parameters.</param>
/// <param name="Cognitive">The cognitive complexity of the body.</param>
/// <param name="Cyclomatic">The cyclomatic complexity of the body.</param>
/// <param name="Lines">The body lines that hold code.</param>
/// <param name="Parameters">The parameter count, or 0 when the unit has no parameter list.</param>
internal sealed record MemberMetrics(string Key, string Path, bool HasBody, int Cognitive, int Cyclomatic, int Lines, int Parameters);

/// <summary>The metrics of one source file.</summary>
/// <param name="Path">The repository-relative path with <c>/</c>.</param>
/// <param name="Lines">The lines that hold anything but whitespace.</param>
/// <param name="Cognitive">The summed cognitive complexity of the file's units.</param>
/// <param name="Members">The units with a body.</param>
/// <param name="Types">The type and delegate declarations.</param>
internal sealed record FileMetrics(string Path, int Lines, int Cognitive, int Members, int Types);

/// <summary>Tokens two locations share as exact clones (see <see cref="CloneDetector"/>).</summary>
/// <param name="Key">The two locations in ordinal order joined by <c> &lt;-&gt; </c>.</param>
/// <param name="Tokens">The total tokens of all clones between the two locations.</param>
internal sealed record Clone(string Key, int Tokens);

/// <summary>Repository-wide sums of a <see cref="Snapshot"/>.</summary>
internal sealed record Totals(int Files, int Types, int Members, int Lines, int Cognitive, int Cyclomatic, int CloneTokens)
{
    /// <summary>The totals' names and values, in report order.</summary>
    public IEnumerable<(string Name, int Value)> Entries() =>
    [
        ("files", Files),
        ("types", Types),
        ("members", Members),
        ("lines", Lines),
        ("cognitive", Cognitive),
        ("cyclomatic", Cyclomatic),
        ("cloneTokens", CloneTokens),
    ];
}

/// <summary>Every measurement of a set of source files, in ordinal key order.</summary>
internal sealed record Snapshot(IReadOnlyList<FileMetrics> Files, IReadOnlyList<MemberMetrics> Members, IReadOnlyList<Clone> Clones)
{
    /// <summary>Measures <paramref name="files"/> (path and text pairs), leaving out generated ones.</summary>
    public static Snapshot Measure(IEnumerable<(string Path, string Text)> files)
    {
        List<ParsedSource> sources = files.AsParallel().AsOrdered()
            .Select(file => SourceMetrics.Parse(file.Path, file.Text))
            .Where(source => !SourceMetrics.IsGenerated(source))
            .OrderBy(source => source.Path, StringComparer.Ordinal)
            .ToList();
        List<MemberMetrics> members = [];
        List<FileMetrics> fileMetrics = [];
        foreach (ParsedSource source in sources)
        {
            IReadOnlyList<MemberMetrics> units = SourceMetrics.MeasureMembers(source);
            members.AddRange(units);
            fileMetrics.Add(SourceMetrics.MeasureFile(source, units));
        }
        members.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        return new Snapshot(fileMetrics, members, CloneDetector.Detect(sources));
    }

    /// <summary>The repository-wide sums.</summary>
    public Totals Totals() => new(
        Files.Count,
        Files.Sum(file => file.Types),
        Files.Sum(file => file.Members),
        Files.Sum(file => file.Lines),
        Files.Sum(file => file.Cognitive),
        Members.Sum(member => member.Cyclomatic),
        Clones.Sum(clone => clone.Tokens));
}
