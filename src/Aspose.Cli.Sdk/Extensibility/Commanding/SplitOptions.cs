using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one split grammar: <c>--by</c> with the divisions a product offers; with <c>--by pages</c>,
/// <c>--every N</c> pages per part, explicit <c>--groups</c> where the product takes them, and the
/// <c>--pages</c> selection.
/// </summary>
public sealed class SplitOptions
{
    /// <summary>The option that chooses the division, for an error about it.</summary>
    public const string ByOption = "--by";
    private const string EveryOption = "--every";
    private const string GroupsOption = "--groups";

    private readonly IReadOnlyList<SplitBy> _divisions;
    private readonly Option<string> _by;
    private readonly Option<int?>? _every;
    private readonly Option<string[]>? _groups;
    private readonly PartRangeOption? _pages;

    /// <summary>Creates the options for the divisions a product offers.</summary>
    /// <param name="divisions">The <c>--by</c> values, in the order help lists them.</param>
    /// <param name="groups">Whether the product takes explicit <c>--groups</c>; only with <see cref="SplitBy.Pages"/>.</param>
    public SplitOptions(IReadOnlyList<SplitBy> divisions, bool groups = false)
    {
        ArgumentNullException.ThrowIfNull(divisions);
        if (divisions.Count == 0 || divisions.Distinct().Count() != divisions.Count)
        {
            throw new ArgumentException("A split offers at least one division, each once.", nameof(divisions));
        }

        bool pages = divisions.Contains(SplitBy.Pages);
        if (groups && !pages)
        {
            throw new ArgumentException("Explicit groups need the pages division.", nameof(groups));
        }

        _divisions = divisions;
        _by = new Option<string>(ByOption)
        {
            Required = true,
            Description = "How to divide the document: " + string.Join("; ", divisions.OrderBy(SplitPlan.Spell, StringComparer.Ordinal).Select(division => "one part " + Describe(division, groups))) + ".",
        }.WithInput(InputKind.None);
        _by.AcceptOnlyFromAmong([.. divisions.Select(SplitPlan.Spell)]);
        var options = new List<Option> { _by };
        if (pages)
        {
            _every = new Option<int?>(EveryOption) { Description = "Pages per part with --by pages. Default: 1." };
            options.Add(_every);
            if (groups)
            {
                _groups = new Option<string[]>(GroupsOption)
                {
                    Description = "Page groups with --by pages, one part each, such as --groups 1-3 4-6.",
                    AllowMultipleArgumentsPerToken = true,
                }.WithInput(InputKind.None);
                options.Add(_groups);
            }

            _pages = new PartRangeOption("page", "every page", onlyWith: "--by pages");
            options.AddRange(_pages.Options);
        }

        Options = options;
        NumberedParts.Declare(_by, parse => Numbers(Read(parse)));
    }

    // The numbers of the parts a split by pages names without the document: one per explicit
    // group, or those of the pages a closed selection names.
    private static IReadOnlyList<int>? Numbers(SplitPlan plan) => plan switch
    {
        { By: not SplitBy.Pages } => null,
        { Groups: { } groups } => [.. Enumerable.Range(1, groups.Count)],
        { Pages.Last: { } last } => plan.PartNumbers(plan.PageGroups(last)),
        _ => null,
    };

    /// <summary>The options, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Returns the validated plan.</summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for a page option without <c>--by pages</c>, <c>--groups</c> with
    /// <c>--every</c> or <c>--pages</c>, an <c>--every</c> below 1, or a value that is not a selection.
    /// </exception>
    public SplitPlan Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        SplitBy by = _divisions.First(division => SplitPlan.Spell(division) == parse.GetRequiredValue(_by));
        int? every = _every is null ? null : parse.GetValue(_every);
        string[]? groupTexts = _groups is null ? null : parse.GetValue(_groups);
        string? pageText = _pages?.Read(parse);
        if (by != SplitBy.Pages)
        {
            string? stray = every is not null ? EveryOption
                : groupTexts is { Length: > 0 } ? GroupsOption
                : pageText is not null ? _pages!.Name
                : null;
            if (stray is not null)
            {
                throw CliErrors.OptionInvalid(stray, $"applies only with {ByOption} pages, not {ByOption} {SplitPlan.Spell(by)}", $"Use {ByOption} pages or omit {stray}.");
            }

            return new SplitPlan(by, null, null, null);
        }

        if (every is < 1)
        {
            throw CliErrors.OptionInvalid(EveryOption, "must be at least 1", "Pass a positive page count, such as --every 2.");
        }

        if (groupTexts is { Length: > 0 })
        {
            if (every is not null || pageText is not null)
            {
                throw CliErrors.OptionInvalid(
                    GroupsOption,
                    $"cannot be combined with {(every is not null ? EveryOption : _pages!.Name)}",
                    $"Give the parts as {GroupsOption}, or select pages with {_pages!.Name} and size the parts with {EveryOption}.");
            }

            return new SplitPlan(by, null, [.. groupTexts.Select(static text => PageRange.ParseOption(GroupsOption, text))], null);
        }

        return new SplitPlan(by, every, null, _pages!.ReadRange(parse));
    }

    private static string Describe(SplitBy division, bool groups) => division switch
    {
        // Each division is told by what it divides at, in the order of the values the label lists,
        // so the values are listed once, in the label; only the options the product takes are named.
        SplitBy.Pages => groups ? $"per page, per {EveryOption} N of them or per {GroupsOption} group" : $"per page or per {EveryOption} N of them",
        SplitBy.Bookmarks => "per top-level bookmark",
        SplitBy.Sections => "per section",
        SplitBy.Heading1 => "per Heading 1, after a leading part for any blocks before the first",
        _ => throw new ArgumentOutOfRangeException(nameof(division)),
    };
}
