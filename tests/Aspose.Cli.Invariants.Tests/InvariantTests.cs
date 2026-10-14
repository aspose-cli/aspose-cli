using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Invariants.Tests;

// Generic invariants that every command satisfies, generated from the live CLI catalog by
// InvariantCases and checked against known-violations.json. Each class is one shard, so the
// shards run side by side; its Holds theory is the unmarked sample and HoldsExhaustively the
// rest of the matrix. The slow cases a known violation names run unmarked in *ListedInvariantTests
// classes of their own, so every run checks every entry of its license mode beside the samples.

/// <summary>The platform commands: the root, review, fonts, license, skills and the other host commands.</summary>
public sealed class PlatformInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), InvariantCases.Platform, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(InvariantCases.Exhaustive), InvariantCases.Platform, MemberType = typeof(InvariantCases))]
    public void HoldsExhaustively(string id) => KnownViolations.Verify(id);
}

public sealed class CellsInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "cells", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(InvariantCases.Exhaustive), "cells", MemberType = typeof(InvariantCases))]
    public void HoldsExhaustively(string id) => KnownViolations.Verify(id);
}

public sealed class PdfInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "pdf", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(InvariantCases.Exhaustive), "pdf", MemberType = typeof(InvariantCases))]
    public void HoldsExhaustively(string id) => KnownViolations.Verify(id);
}

public sealed class SlidesInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "slides", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(InvariantCases.Exhaustive), "slides", MemberType = typeof(InvariantCases))]
    public void HoldsExhaustively(string id) => KnownViolations.Verify(id);
}

public sealed class WordsInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "words", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);

    [Theory]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(InvariantCases.Exhaustive), "words", MemberType = typeof(InvariantCases))]
    public void HoldsExhaustively(string id) => KnownViolations.Verify(id);
}

/// <summary>The contract rules that the catalog, help and schemas answer without running a command.</summary>
public sealed class ContractInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), InvariantCases.Contract, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);

    [Theory(SkipTestWithoutData = true)]
    [Category(TestCategory.Slow)]
    [MemberData(nameof(InvariantCases.Exhaustive), InvariantCases.Contract, MemberType = typeof(InvariantCases))]
    public void HoldsExhaustively(string id) => KnownViolations.Verify(id);
}

/// <summary>Each product's own document converted to every declared format and rendered to every image format.</summary>
public sealed class CellsFormatInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "cells-formats", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class PdfFormatInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "pdf-formats", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class SlidesFormatInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "slides-formats", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class WordsFormatInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Sample), "words-formats", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

/// <summary>A seed of every other format each product reads and writes, converted back to its own format and to PDF.</summary>
[Category(TestCategory.Slow)]
public sealed class FormatMatrixInvariantTests
{
    [Theory]
    [MemberData(nameof(InvariantCases.Exhaustive), InvariantCases.FormatMatrix, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class PlatformListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), InvariantCases.Platform, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class CellsListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), "cells", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class PdfListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), "pdf", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class SlidesListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), "slides", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class WordsListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), "words", MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class ContractListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), InvariantCases.Contract, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

public sealed class FormatMatrixListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), InvariantCases.FormatMatrix, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

/// <summary>
/// The known-violations list names only generated cases and known invariants, once each, with the
/// contract rule and a root cause, grouped by cause.
/// </summary>
public sealed partial class KnownViolationsTests
{
    [Fact]
    public void EveryEntryNamesAGeneratedCaseAndAnInvariant()
    {
        string[] modes = ["licensed", "evaluation"];
        var seen = new HashSet<(string, string)>();
        foreach (KnownViolation entry in KnownViolations.Entries)
        {
            Assert.True(InvariantCases.All.ContainsKey(entry.Case), $"{KnownViolations.FileName} names a case that is no longer generated: {entry.Case}");
            Assert.True(InvariantCases.Invariants.Contains(entry.Invariant), $"{entry.Case} names an unknown invariant '{entry.Invariant}'.");
            Assert.True(entry.Rule == InvariantCases.RuleOf(entry.Invariant),
                $"{entry.Case} must name the contract rule its invariant enforces, {InvariantCases.RuleOf(entry.Invariant)}.");
            Assert.True(entry.Cause is not null && CausePattern().IsMatch(entry.Cause),
                $"{entry.Case} must name its root cause in kebab case, such as convert-foreign-extension.");
            Assert.True(entry.Modes is null || (entry.Modes.Count == 1 && modes.Contains(entry.Modes[0])),
                $"{entry.Case} must omit modes (both) or name one of: {string.Join(", ", modes)}.");
            Assert.True(seen.Add((entry.Case, entry.Invariant)), $"{entry.Case} lists {entry.Invariant} twice.");
            Assert.False(string.IsNullOrWhiteSpace(entry.Actual), $"{entry.Case} does not say what happens.");
        }
    }

    [Fact]
    public void EntriesAreGroupedByCause()
    {
        IReadOnlyList<KnownViolation> listed = KnownViolations.Entries;

        Assert.Equal(
            listed.OrderBy(static entry => entry.Cause, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Case, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Invariant, StringComparer.Ordinal),
            listed);
    }

    [Fact]
    public void EveryNumberedContractRuleHasCases()
    {
        string[] numbered = [.. Enumerable.Range(1, 38).Select(static number => $"C-R{number:00}")];
        Assert.Equal(numbered, ContractRules.All.Select(static rule => rule.Id));
        HashSet<string> checkedRules = [.. InvariantCases.All.Values.Select(static item => InvariantCases.RuleOf(item.Invariant))];
        string[] missing = [.. numbered.Where(rule => !checkedRules.Contains(rule))];
        Assert.True(missing.Length == 0, $"No invariant case checks {string.Join(", ", missing)}.");
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CausePattern();

    /// <summary>
    /// Every generated case runs: the cases that the shard classes' theories select add up to all
    /// cases, so a shard without its class (a new product's, say) fails here instead of running
    /// nothing; every sample theory selects cases; and every product the fixtures name has cases
    /// in its shard and its format shard.
    /// </summary>
    [Fact]
    public void EveryCaseRunsInAShardClass()
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var empty = new List<string>();
        foreach ((Type type, MemberDataAttribute data) in typeof(KnownViolationsTests).Assembly.GetTypes()
            .SelectMany(static type => type.GetMethods().SelectMany(method => method.GetCustomAttributes(typeof(MemberDataAttribute), false)
                .Cast<MemberDataAttribute>().Select(data => (type, data))))
            .Where(static pair => pair.data.MemberType == typeof(InvariantCases)))
        {
            var rows = (IEnumerable<ITheoryDataRow>)typeof(InvariantCases).GetMethod(data.MemberName)!.Invoke(null, data.Arguments)!;
            string[] ids = [.. rows.Select(static row => (string)row.GetData()[0]!)];
            selected.UnionWith(ids);
            if (ids.Length == 0 && data.MemberName == nameof(InvariantCases.Sample))
            {
                empty.Add($"{type.Name}: {data.MemberName}({string.Join(", ", data.Arguments)})");
            }
        }
        InvariantCase[] unrun = [.. InvariantCases.All.Values.Where(item => !selected.Contains(item.Id)).OrderBy(static item => item.Id, StringComparer.Ordinal)];
        Assert.True(unrun.Length == 0,
            $"{unrun.Length} generated cases run in no shard class; add a class for the shards "
            + $"{string.Join(", ", unrun.Select(static item => item.Shard).Distinct())} (first: {string.Join(", ", unrun.Take(3).Select(static item => item.Id))})");
        Assert.True(empty.Count == 0, "Sample theories that select no case: " + string.Join("; ", empty));

        string[] expected = [InvariantCases.Platform, InvariantCases.Contract, .. ScenarioFixtures.Products, .. ScenarioFixtures.Products.Select(static product => product + "-formats")];
        string[] missing = [.. expected.Where(shard => !InvariantCases.All.Values.Any(item => item.Shard == shard && !item.Slow))];
        Assert.True(missing.Length == 0, $"The shards {string.Join(", ", missing)} have no unmarked cases.");
        Assert.NotEmpty(InvariantCases.Exhaustive(InvariantCases.FormatMatrix));
    }
}
