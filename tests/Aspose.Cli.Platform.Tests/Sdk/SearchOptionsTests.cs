using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Text;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class SearchOptionsTests
{
    private static readonly SearchScopeGrammar Scopes = new("Where to look.", ["body", "all"], "body");

    [Fact]
    public void Read_ReturnsTheValidatedQueryWithTheProductDefaultScope()
    {
        SearchQuery query = Read(new SearchOptions(Scopes), "--pattern", "Total", "--case-sensitive");

        Assert.Equal("Total", query.Text.Pattern);
        Assert.True(query.Text.CaseSensitive);
        Assert.Null(query.Text.Expression);
        Assert.Equal(SearchOptions.DefaultMaxHits, query.MaxHits);
        Assert.Equal("body", query.Scope);
    }

    [Fact]
    public void Parse_AcceptsOnlyTheProductScopesAndRequiresThePattern()
    {
        Assert.NotEmpty(Parse(new SearchOptions(Scopes), "--pattern", "x", "--scope", "notes").Errors);
        Assert.NotEmpty(Parse(new SearchOptions(), "--pattern", "x", "--scope", "body").Errors);
        Assert.NotEmpty(Parse(new SearchOptions()).Errors);
        Assert.Null(Read(new SearchOptions(), "--pattern", "x").Scope);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10001")]
    public void Read_BoundsTheHitBudgetInOnePlace(string maxHits)
    {
        CliException error = Assert.Throws<CliException>(
            () => Read(new SearchOptions(), "--pattern", "x", "--max-hits", maxHits));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Equal(SearchOptions.MaximumHits, Read(
            new SearchOptions(), "--pattern", "x", "--max-hits", "10000").MaxHits);
    }

    [Fact]
    public void Window_SkipsEarlierMatchesAndResumesAfterTheReturnedHits()
    {
        SearchQuery query = Read(new SearchOptions(Scopes), "--pattern", "a b", "--regex", "--max-hits", "2", "--skip", "1");
        SearchHits<int> hits = query.Collect<int>();

        int offered = 0;
        foreach (int match in Enumerable.Range(1, 5))
        {
            offered++;
            if (!hits.Offer(() => match))
            {
                break;
            }
        }

        Assert.Equal([2, 3], hits.Hits);
        Assert.Equal(4, offered);
        Assert.Equal(
            "aspose-cli p search in.x --pattern \"a b\" --regex --scope body --max-hits 2 --skip 3 --output json",
            SearchOptions.Continue(query, hits.Window(), new ContinuationCommand("p", "search").Argument("in.x")).Next);
        Assert.Null(SearchOptions.Continue(query, query.Collect<int>().Window(), new ContinuationCommand("p", "search")).Next);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("(", true)]
    [InlineData("a*", true)]
    [InlineData("^", true)]
    public void Read_RejectsAnEmptyPatternAnInvalidRegexAndAnEmptyMatch(string pattern, bool regex)
    {
        string[] arguments = regex ? ["--pattern", pattern, "--regex"] : ["--pattern", pattern];

        CliException error = Assert.Throws<CliException>(() => Read(new SearchOptions(), arguments));

        Assert.Equal(ErrorCodes.OptionInvalid, error.Code);
        Assert.Contains("--pattern", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextSearch_CountsOnlyNonEmptyMatches()
    {
        TextSearch boundary = TextSearch.Create(@"\b", regex: true, caseSensitive: false);
        TextSearch literal = TextSearch.Create("ab", regex: false, caseSensitive: false);

        Assert.False(boundary.IsMatch("word"));
        Assert.Empty(boundary.Find("word"));
        Assert.Equal([(0, 2), (3, 2)], literal.Find("AB-ab"));
        Assert.True(literal.IsMatch("xAbx"));
    }

    [Fact]
    public void TextSearch_MatchesCaseInsensitivelyWithoutTheCurrentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.True(TextSearch.Create("I", regex: true, caseSensitive: false).IsMatch("i"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void TextSearch_TranslatesARegexTimeout()
    {
        TextSearch catastrophic = TextSearch.Create("(a+)+$", regex: true, caseSensitive: true);

        CliException error = Assert.Throws<CliException>(
            () => catastrophic.IsMatch(new string('a', 5_000) + "!"));

        Assert.Equal(ErrorCodes.OperationTimeout, error.Code);
    }

    private static SearchQuery Read(SearchOptions options, params string[] arguments) =>
        options.Read(Parse(options, arguments));

    private static ParseResult Parse(SearchOptions options, params string[] arguments)
    {
        var command = new Command("search");
        foreach (Option option in options.Options)
        {
            command.Options.Add(option);
        }
        return command.Parse(arguments);
    }
}
