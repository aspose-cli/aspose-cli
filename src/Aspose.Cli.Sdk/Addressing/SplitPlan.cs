using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Addressing;

/// <summary>How a split command divides a document, the values of <c>--by</c>.</summary>
public enum SplitBy
{
    /// <summary><c>pages</c>: one part per page, per <c>--every</c> pages or per <c>--groups</c> group.</summary>
    Pages,

    /// <summary><c>bookmarks</c>: one part per top-level bookmark.</summary>
    Bookmarks,

    /// <summary><c>sections</c>: one part per section.</summary>
    Sections,

    /// <summary><c>heading1</c>: one part per Heading 1, after a leading part for any blocks before the first.</summary>
    Heading1,
}

/// <summary>One validated split request: the division and, for pages, which pages go into which part.</summary>
/// <param name="By">How the document is divided.</param>
/// <param name="Every">Pages per part with <see cref="SplitBy.Pages"/>, or null for one page per part.</param>
/// <param name="Groups">The explicit page groups, one part each, or null.</param>
/// <param name="Pages">The pages to split, or null for every page.</param>
public sealed record SplitPlan(SplitBy By, int? Every, IReadOnlyList<PageRange>? Groups, PageRange? Pages)
{
    /// <summary>
    /// The pages of each part of a split by pages over <paramref name="pageCount"/> pages: each
    /// explicit group, or the selected pages, every page by default, in runs of
    /// <see cref="Every"/> pages, one by default.
    /// </summary>
    /// <exception cref="InvalidOperationException">The plan does not split by pages.</exception>
    /// <exception cref="CliException"><c>PAGE_NOT_FOUND</c> for a page past the count.</exception>
    public IReadOnlyList<IReadOnlyList<int>> PageGroups(int pageCount)
    {
        if (By != SplitBy.Pages)
        {
            throw new InvalidOperationException($"A split by {Spell(By)} has no page groups.");
        }

        if (Groups is { } groups)
        {
            return [.. groups.Select(group => group.Resolve(pageCount))];
        }

        return [.. (Pages ?? PageRange.All).Resolve(pageCount).Chunk(Every ?? 1).Select(static run => (IReadOnlyList<int>)run)];
    }

    /// <summary>
    /// The numbers that name the parts of <paramref name="groups"/>, the result of
    /// <see cref="PageGroups"/>: a part's page when every part is one page by default or with
    /// <c>--every 1</c>, and otherwise its 1-based position.
    /// </summary>
    public IReadOnlyList<int> PartNumbers(IReadOnlyList<IReadOnlyList<int>> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return By == SplitBy.Pages && Groups is null && (Every ?? 1) == 1
            ? [.. groups.Select(static group => group[0])]
            : [.. Enumerable.Range(1, groups.Count)];
    }

    /// <summary>The <c>--by</c> value of a division.</summary>
    internal static string Spell(SplitBy division) => division switch
    {
        SplitBy.Pages => "pages",
        SplitBy.Bookmarks => "bookmarks",
        SplitBy.Sections => "sections",
        SplitBy.Heading1 => "heading1",
        _ => throw new ArgumentOutOfRangeException(nameof(division)),
    };
}
