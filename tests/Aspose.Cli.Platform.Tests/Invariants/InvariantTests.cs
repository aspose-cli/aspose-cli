using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Invariants;

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

public sealed class FormatMatrixListedInvariantTests
{
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(InvariantCases.Listed), InvariantCases.FormatMatrix, MemberType = typeof(InvariantCases))]
    public void Holds(string id) => KnownViolations.Verify(id);
}

/// <summary>
/// The known-violations list names only generated cases and known invariants, once each, with a
/// root cause, grouped by cause.
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

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CausePattern();

    [Fact]
    public void EveryShardHasCases()
    {
        string[] shards = [InvariantCases.Platform, .. ScenarioFixtures.Products, .. ScenarioFixtures.Products.Select(static product => product + "-formats")];
        foreach (string shard in shards)
        {
            Assert.NotEmpty(InvariantCases.Sample(shard));
        }
        Assert.NotEmpty(InvariantCases.Exhaustive(InvariantCases.FormatMatrix));
    }
}
