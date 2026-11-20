using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>search</c> grammar: <c>--pattern</c>, <c>--regex</c>,
/// <c>--case-sensitive</c>, <c>--max-hits</c> and <c>--skip</c>. Every product validates the
/// pattern, the hit budget and paging the same way.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>Hits returned when <c>--max-hits</c> is omitted.</summary>
    internal const int DefaultMaxHits = 100;

    /// <summary>The largest accepted <c>--max-hits</c>.</summary>
    internal const int MaximumHits = 10_000;

    /// <summary>The option that holds the text or expression to find, for an error about it.</summary>
    public const string PatternOption = "--pattern";
    internal const string RegexOption = "--regex";
    internal const string CaseSensitiveOption = "--case-sensitive";
    internal const string MaxHitsOption = "--max-hits";
    internal const string SkipOption = "--skip";

    private readonly Option<string> _pattern;
    private readonly Option<bool> _regex;
    private readonly Option<bool> _caseSensitive;
    private readonly Option<int> _maxHits;
    private readonly Option<int> _skip;

    /// <summary>Creates the search options.</summary>
    public SearchOptions()
    {
        _pattern = new Option<string>(PatternOption)
        {
            Required = true,
            Description = "Text to find, or a regular expression with --regex.",
        }.WithInput(InputKind.None);
        _regex = new Option<bool>(RegexOption)
        {
            Description = "Treat the pattern as a culture-invariant regular expression with a one-second budget.",
        };
        _caseSensitive = new Option<bool>(CaseSensitiveOption)
        {
            Description = "Match letter case exactly.",
        };
        _maxHits = new Option<int>(MaxHitsOption)
        {
            Description = "Maximum returned hits.",
            DefaultValueFactory = _ => DefaultMaxHits,
        }.InRange(1, MaximumHits);
        _skip = new Option<int>(SkipOption)
        {
            Description = "Matches to pass over before the returned hits; a truncated search's window.next sets it.",
            DefaultValueFactory = _ => 0,
        };
        Options = [_pattern, _regex, _caseSensitive, _maxHits, _skip];
    }

    /// <summary>
    /// Completes the window a search returned: when hits remain, <c>next</c> holds the
    /// <c>--skip</c> past the returned hits.
    /// </summary>
    public static ResultWindow Continue(SearchQuery query, ResultWindow window)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(window);
        return window.Truncated
            ? window with { Next = ResultWindow.Options((SkipOption, (query.Skip + window.Returned).ToString(CultureInfo.InvariantCulture))) }
            : window;
    }

    /// <summary>The search options in help order, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Validates the pattern and hit budget before any document is opened.</summary>
    public SearchQuery Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int maxHits = parse.GetInRange(_maxHits);
        int skip = parse.GetValue(_skip);
        OptionGuards.EnsureInRange(SkipOption, skip, 0, int.MaxValue);
        return new SearchQuery(
            TextSearch.Create(
                parse.GetRequiredValue(_pattern),
                parse.GetValue(_regex),
                parse.GetValue(_caseSensitive)),
            maxHits,
            skip);
    }
}
