using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.CodeHealth;

/// <summary>Renders a report as Markdown for people or as JSON for tools; both are culture-invariant.</summary>
internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>The report as indented JSON with camel-case names.</summary>
    public static string Json(object report) => JsonSerializer.Serialize(report, report.GetType(), JsonOptions) + "\n";

    /// <summary>The measurements as Markdown.</summary>
    public static string Markdown(MeasureReport report)
    {
        StringBuilder text = new();
        text.Append(Invariant($"# Code health of `{report.Source}` ({report.Revision})\n\n"));
        text.Append("A diagnostic: the numbers point to where to look, not to what to change. ")
            .Append("Cognitive complexity charges each break in linear flow plus its nesting depth; ")
            .Append("cyclomatic complexity counts decision points.\n\n");
        Table(text, "Totals", ["Measure", "Value"], report.Totals.Entries().Select(entry => new[] { entry.Name, Number(entry.Value) }));
        MemberTable(text, "Most complex members (cognitive)", report.MostComplexMembers);
        MemberTable(text, "Longest members (body lines)", report.LongestMembers);
        MemberTable(text, "Most parameters", report.MostParameters);
        Table(text, "Longest files (non-blank lines)", ["Lines", "Cognitive", "Members", "Types", "File"],
            report.LongestFiles.Select(file => new[] { Number(file.Lines), Number(file.Cognitive), Number(file.Members), Number(file.Types), Code(file.Path) }));
        Table(text, Invariant($"Exact clones ({report.MinimumCloneTokens}+ identical tokens)"), ["Tokens", "Locations"],
            report.Clones.Select(clone => new[] { Number(clone.Tokens), Code(clone.Key) }));
        Table(text, Invariant($"Hotspots (commits x cognitive, history `{report.History}`)"), ["Score", "Commits", "Fixes", "Cognitive", "File"],
            report.Hotspots.Select(spot => new[] { Number(spot.Score), Number(spot.Commits), Number(spot.Fixes), Number(spot.Cognitive), Code(spot.Path) }));
        return text.ToString();
    }

    /// <summary>The comparison as Markdown.</summary>
    public static string Markdown(CompareReport report)
    {
        StringBuilder text = new();
        text.Append(Invariant($"# Code health of `{report.Source}`: {report.Head} against `{report.Base}`\n\n"));
        text.Append("Members are keyed by file, type and signature, so a moved or renamed member shows as removed and new: ")
            .Append("compare the totals to see whether complexity was removed or only moved.\n\n");
        Table(text, "Totals", ["Measure", "Base", "Head", "Delta"],
            report.BaseTotals.Entries().Zip(report.HeadTotals.Entries(), (before, after) =>
                new[] { before.Name, Number(before.Value), Number(after.Value), Delta(after.Value - before.Value) }));
        foreach (ChangeKind kind in Enum.GetValues<ChangeKind>())
        {
            MemberChange[] members = report.Members.Where(change => change.Kind == kind).ToArray();
            Table(text, Invariant($"{Name(kind)} members ({report.MemberCounts[kind]})"),
                ["Cognitive", "Delta", "Lines", "Delta", "Params", "Member"],
                members.Select(change => new[]
                {
                    Pair(kind, change.BaseCognitive, change.HeadCognitive), Delta(change.HeadCognitive - change.BaseCognitive),
                    Pair(kind, change.BaseLines, change.HeadLines), Delta(change.HeadLines - change.BaseLines),
                    Pair(kind, change.BaseParameters, change.HeadParameters), Code(change.Key),
                }));
        }
        foreach (ChangeKind kind in Enum.GetValues<ChangeKind>())
        {
            FileChange[] files = report.Files.Where(change => change.Kind == kind).ToArray();
            Table(text, Invariant($"{Name(kind)} files ({report.FileCounts[kind]})"), ["Cognitive", "Delta", "Lines", "Delta", "File"],
                files.Select(change => new[]
                {
                    Pair(kind, change.BaseCognitive, change.HeadCognitive), Delta(change.HeadCognitive - change.BaseCognitive),
                    Pair(kind, change.BaseLines, change.HeadLines), Delta(change.HeadLines - change.BaseLines), Code(change.Path),
                }));
        }
        Table(text, Invariant($"New exact clones ({report.NewClones.Count})"), ["Tokens", "Locations"],
            report.NewClones.Select(clone => new[] { Number(clone.Tokens), Code(clone.Key) }));
        return text.ToString();
    }

    private static void MemberTable(StringBuilder text, string title, IReadOnlyList<MemberMetrics> members) =>
        Table(text, title, ["Cognitive", "Cyclomatic", "Lines", "Params", "Member"],
            members.Select(member => member.HasBody
                ? new[] { Number(member.Cognitive), Number(member.Cyclomatic), Number(member.Lines), Number(member.Parameters), Code(member.Key) }
                : ["-", "-", "-", Number(member.Parameters), Code(member.Key)]));

    private static void Table(StringBuilder text, string title, string[] headers, IEnumerable<string[]> rows)
    {
        text.Append("## ").Append(title).Append("\n\n");
        string[][] all = rows.ToArray();
        if (all.Length == 0)
        {
            text.Append("None.\n\n");
            return;
        }
        text.Append("| ").AppendJoin(" | ", headers).Append(" |\n");
        text.Append('|').AppendJoin("|", headers.Select(header => header is "Member" or "File" or "Locations" or "Measure" ? " --- " : " ---: ")).Append("|\n");
        foreach (string[] row in all)
        {
            text.Append("| ").AppendJoin(" | ", row).Append(" |\n");
        }
        text.Append('\n');
    }

    private static string Name(ChangeKind kind) => kind switch
    {
        ChangeKind.New => "New",
        ChangeKind.Worse => "Worse",
        ChangeKind.Better => "Better",
        _ => "Removed",
    };

    /// <summary>The value a new or removed entry has, or <c>before -> after</c> for a changed one.</summary>
    private static string Pair(ChangeKind kind, int before, int after) => kind switch
    {
        ChangeKind.New => Number(after),
        ChangeKind.Removed => Number(before),
        _ => before == after ? Number(after) : Invariant($"{before} -> {after}"),
    };

    private static string Code(string value) => "`" + value.Replace("|", "\\|", StringComparison.Ordinal) + "`";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Delta(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString value) => FormattableString.Invariant(value);
}
