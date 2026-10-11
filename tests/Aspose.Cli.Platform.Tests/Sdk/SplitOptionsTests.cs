using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

/// <summary>
/// The one split grammar: <c>--by</c> with a product's divisions, and with <c>--by pages</c>
/// <c>--every</c>, <c>--groups</c> and the <c>--pages</c> selection.
/// </summary>
public sealed class SplitOptionsTests
{
    private static (SplitOptions Split, Command Command) Create(IReadOnlyList<SplitBy> divisions, bool groups = false)
    {
        var split = new SplitOptions(divisions, groups);
        var command = new Command("split");
        foreach (Option option in split.Options)
        {
            command.Options.Add(option);
        }

        return (split, command);
    }

    private static string[] Groups(SplitPlan plan, int pageCount) =>
        [.. plan.PageGroups(pageCount).Select(static group => string.Join(",", group))];

    private static SplitPlan Read(SplitOptions split, Command command, params string[] arguments) =>
        split.Read(command.Parse(arguments));

    [Fact]
    public void Options_OfferOnlyWhatTheDivisionsTake()
    {
        (SplitOptions pdf, _) = Create([SplitBy.Pages, SplitBy.Bookmarks], groups: true);
        (SplitOptions words, _) = Create([SplitBy.Sections, SplitBy.Heading1, SplitBy.Pages]);
        (SplitOptions sections, _) = Create([SplitBy.Sections]);

        Assert.Equal(["--by", "--every", "--groups", "--pages"], pdf.Options.Select(static option => option.Name));
        Assert.Equal(["--by", "--every", "--pages"], words.Options.Select(static option => option.Name));
        Assert.Equal(["--by"], sections.Options.Select(static option => option.Name));
        Assert.True(pdf.Options[0].Required);
    }

    [Fact]
    public void ByDescription_NamesOnlyTheOptionsTheProductTakes()
    {
        (SplitOptions pdf, _) = Create([SplitBy.Pages, SplitBy.Bookmarks], groups: true);
        (SplitOptions words, _) = Create([SplitBy.Sections, SplitBy.Heading1, SplitBy.Pages]);

        Assert.Contains("one part per page, per --every N of them or per --groups group", pdf.Options[0].Description, StringComparison.Ordinal);
        Assert.Contains("one part per page or per --every N of them;", words.Options[0].Description, StringComparison.Ordinal);
        Assert.DoesNotContain("--groups", words.Options[0].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_RefuseGroupsWithoutPagesAndRepeatedDivisions()
    {
        Assert.Throws<ArgumentException>(() => new SplitOptions([SplitBy.Bookmarks], groups: true));
        Assert.Throws<ArgumentException>(() => new SplitOptions([SplitBy.Pages, SplitBy.Pages]));
        Assert.Throws<ArgumentException>(() => new SplitOptions([]));
    }

    [Fact]
    public void ByPages_SplitsEveryPageAloneByDefault()
    {
        (SplitOptions split, Command command) = Create([SplitBy.Pages, SplitBy.Bookmarks], groups: true);

        SplitPlan plan = Read(split, command, "--by", "pages");

        Assert.Equal(new SplitPlan(SplitBy.Pages, null, null, null), plan);
        Assert.Equal(["1", "2", "3"], Groups(plan, 3));
    }

    [Fact]
    public void ByPages_RunsTheSelectedPagesInPartsOfEveryPages()
    {
        (SplitOptions split, Command command) = Create([SplitBy.Pages]);

        SplitPlan plan = Read(split, command, "--by", "pages", "--every", "2", "--pages", "2-6");

        Assert.Equal(2, plan.Every);
        Assert.Equal("2-6", plan.Pages!.Text);
        Assert.Equal(["2,3", "4,5", "6"], Groups(plan, 7));
        Assert.Equal(["1,2,3", "4,5"], Groups(Read(split, command, "--by", "pages", "--every", "3", "--pages", "all"), 5));
    }

    [Fact]
    public void ByPages_TakesExplicitGroupsOnePartEach()
    {
        (SplitOptions split, Command command) = Create([SplitBy.Pages, SplitBy.Bookmarks], groups: true);

        SplitPlan plan = Read(split, command, "--by", "pages", "--groups", "1-3", "5", "2,4");

        Assert.Equal(["1-3", "5", "2,4"], plan.Groups!.Select(static group => group.Text));
        Assert.Equal(["1,2,3", "5", "2,4"], Groups(plan, 5));
    }

    [Fact]
    public void PageGroups_ReportAPagePastTheCount()
    {
        (SplitOptions split, Command command) = Create([SplitBy.Pages]);

        CliException error = Assert.Throws<CliException>(
            () => Read(split, command, "--by", "pages", "--pages", "4").PageGroups(3));

        Assert.Equal(ErrorCodes.PageNotFound, error.Code);
    }

    [Fact]
    public void OtherDivisions_CarryNoPages()
    {
        (SplitOptions split, Command command) = Create([SplitBy.Sections, SplitBy.Heading1, SplitBy.Pages]);

        SplitPlan plan = Read(split, command, "--by", "heading1");

        Assert.Equal(new SplitPlan(SplitBy.Heading1, null, null, null), plan);
        Assert.Throws<InvalidOperationException>(() => plan.PageGroups(3));
    }

    [Theory]
    [InlineData("--every", "--by", "bookmarks", "--every", "2")]
    [InlineData("--groups", "--by", "bookmarks", "--groups", "1-2")]
    [InlineData("--pages", "--by", "bookmarks", "--pages", "1")]
    [InlineData("--every", "--by", "pages", "--every", "0")]
    [InlineData("--groups", "--by", "pages", "--groups", "1-2", "--every", "2")]
    [InlineData("--groups", "--by", "pages", "--groups", "1-2", "--pages", "1")]
    [InlineData("--groups", "--by", "pages", "--groups", "1-x")]
    [InlineData("--pages", "--by", "pages", "--pages", "x")]
    public void Read_RefusesAMisusedOptionAsAnOptionMistake(string option, params string[] arguments)
    {
        (SplitOptions split, Command command) = Create([SplitBy.Pages, SplitBy.Bookmarks], groups: true);

        CliException error = Assert.Throws<CliException>(() => Read(split, command, arguments));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal(option, error.Details!["option"]!.GetValue<string>());
    }

    [Fact]
    public void By_AcceptsOnlyTheProductsDivisions()
    {
        (_, Command command) = Create([SplitBy.Sections, SplitBy.Pages]);

        Assert.NotEmpty(command.Parse(["--by", "bookmarks"]).Errors);
        Assert.NotEmpty(command.Parse([]).Errors);
        Assert.Empty(command.Parse(["--by", "sections"]).Errors);
    }

    [Fact]
    public void PartNumbers_NameSinglePagePartsByPageAndGroupsByPosition()
    {
        (SplitOptions split, Command command) = Create([SplitBy.Pages, SplitBy.Bookmarks], groups: true);

        SplitPlan single = Read(split, command, "--by", "pages", "--pages", "3,5");
        SplitPlan groups = Read(split, command, "--by", "pages", "--groups", "3", "5");
        SplitPlan runs = Read(split, command, "--by", "pages", "--every", "2", "--pages", "3-6");

        Assert.Equal([3, 5], single.PartNumbers(single.PageGroups(6)));
        Assert.Equal([1, 2], groups.PartNumbers(groups.PageGroups(6)));
        Assert.Equal([1, 2], runs.PartNumbers(runs.PageGroups(6)));
    }

    [Fact]
    public void PartNumbers_NameBookmarkPartsByPosition()
    {
        var plan = new SplitPlan(SplitBy.Bookmarks, null, null, null);

        Assert.Equal([1, 2, 3], plan.PartNumbers([[1, 2], [3], [4, 5]]));
    }
}
