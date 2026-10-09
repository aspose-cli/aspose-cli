using Aspose.Cli.Product.Words.Engine.Editing;
using Xunit;

namespace Aspose.Cli.Product.Words.Tests;

/// <summary>
/// An outcome lists at most <see cref="BoundedOperationRunner.MaximumTargets"/> targets, the
/// headers and footers an operation changed counted with its blocks; past the cap it lists the
/// Words degenerate form, which itself fits the cap however many headers and footers changed, so
/// a large edit never ends in an internal error.
/// </summary>
public sealed class WordsTargetCapTests
{
    private static readonly FormatTextOp BlockRange = new() { Target = new WordsTarget { Blocks = "1-60" }, Bold = true };

    private static string[] Blocks(int count) => [.. Enumerable.Range(1, count).Select(static block => $"block/{block}")];

    /// <summary>A primary header and footer for each of <paramref name="sections"/> sections.</summary>
    private static string[] HeadersAndFooters(int sections) =>
    [
        .. Enumerable.Range(1, sections).SelectMany(static section => new[]
        {
            $"section/{section}/header/primary",
            $"section/{section}/footer/primary",
        }),
    ];

    [Fact]
    public void HeadersAndFooters_CountTowardTheCap()
    {
        string[] changed = [.. Blocks(60), .. HeadersAndFooters(30)];

        IReadOnlyList<string> targets = Run(BlockRange, changed);

        Assert.InRange(targets.Count, 1, BoundedOperationRunner.MaximumTargets);
        Assert.StartsWith("blocks/1-60", targets[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DegenerateTargets_FitTheCapWhenManyHeadersAndFootersChanged()
    {
        string[] changed = [.. Blocks(60), .. HeadersAndFooters(75)];

        IReadOnlyList<string> degenerate = WordsAnchorResolver.DegenerateTargets(BlockRange, changed);

        Assert.InRange(degenerate.Count, 1, BoundedOperationRunner.MaximumTargets);
    }

    [Fact]
    public void ABlockRangeEditWithManyHeadersAndFooters_ListsTheDegenerateFormInsteadOfFailing()
    {
        string[] changed = [.. Blocks(60), .. HeadersAndFooters(75)];

        IReadOnlyList<string> targets = Run(BlockRange, changed);

        Assert.InRange(targets.Count, 1, BoundedOperationRunner.MaximumTargets);
        Assert.True(targets is ["document"] || targets[0] == "blocks/1-60",
            $"The degenerate form is the whole document or the block range first: {string.Join(", ", targets)}");
    }

    /// <summary>The targets the Words outcome of one operation lists when it changed <paramref name="changed"/>.</summary>
    private static IReadOnlyList<string> Run(WordsOp op, string[] changed)
    {
        WordsOpsBatch batch = WordsOp.Catalog.Prepare(new WordsOpsBatch { Ops = [op] });
        IReadOnlyList<BoundedOperationOutcome> outcomes = BoundedOperationRunner.Run(
            WordsOp.Catalog,
            batch.Ops,
            bestEffort: false,
            deadline: null,
            (_, _) => new AppliedOperation(changed.Length, changed),
            (_, _) => changed,
            WordsAnchorResolver.DegenerateTargets);
        return Assert.Single(outcomes).Targets;
    }
}
