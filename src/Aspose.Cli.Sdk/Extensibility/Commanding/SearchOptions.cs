using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The product-owned parts of a document's search scope option.</summary>
/// <param name="Description">Help text naming what each scope covers.</param>
/// <param name="Values">Accepted scope values.</param>
/// <param name="Default">The scope used when the option is omitted.</param>
public sealed record SearchScopeGrammar(
    string Description,
    IReadOnlyList<string> Values,
    string Default);

/// <summary>
/// The one <c>query search</c> grammar: <c>--pattern</c>, <c>--regex</c>,
/// <c>--case-sensitive</c>, <c>--max-hits</c>, <c>--skip</c> and an optional product-supplied
/// <c>--scope</c>. Every product validates the pattern, the hit budget and paging the same way.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>Hits returned when <c>--max-hits</c> is omitted.</summary>
    internal const int DefaultMaxHits = 100;

    /// <summary>The largest accepted <c>--max-hits</c>.</summary>
    internal const int MaximumHits = 10_000;

    internal const string PatternOption = "--pattern";
    internal const string RegexOption = "--regex";
    internal const string CaseSensitiveOption = "--case-sensitive";
    internal const string ScopeOption = "--scope";
    internal const string MaxHitsOption = "--max-hits";
    internal const string SkipOption = "--skip";

    private readonly Option<string> _pattern;
    private readonly Option<bool> _regex;
    private readonly Option<bool> _caseSensitive;
    private readonly Option<int> _maxHits;
    private readonly Option<int> _skip;
    private readonly Option<string>? _scope;

    /// <summary>Creates the search options, with a scope only when the product has one.</summary>
    public SearchOptions(SearchScopeGrammar? scope = null)
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
            Description = $"Maximum returned hits (1-{MaximumHits}).",
            DefaultValueFactory = _ => DefaultMaxHits,
        };
        _skip = new Option<int>(SkipOption)
        {
            Description = "Matches to pass over before the returned hits; a truncated search's window.next sets it.",
            DefaultValueFactory = _ => 0,
        };
        if (scope is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope.Description);
            if (!scope.Values.Contains(scope.Default, StringComparer.Ordinal))
            {
                throw new ArgumentException("The default scope must be one of the accepted values.", nameof(scope));
            }

            _scope = new Option<string>(ScopeOption)
            {
                Description = scope.Description,
                DefaultValueFactory = _ => scope.Default,
            }.WithInput(InputKind.None);
            _scope.AcceptOnlyFromAmong([.. scope.Values]);
        }

        Options = _scope is null
            ? [_pattern, _regex, _caseSensitive, _maxHits, _skip]
            : [_pattern, _regex, _caseSensitive, _scope, _maxHits, _skip];
    }

    /// <summary>
    /// Completes the window a search returned: when hits remain, <c>next</c> is
    /// <paramref name="resume"/> (see <see cref="StandardInvocation.Continuation"/>, plus the
    /// product's own options) followed by the query's options and a <c>--skip</c> past the
    /// returned hits.
    /// </summary>
    public static ResultWindow Continue(SearchQuery query, ResultWindow window, ContinuationCommand resume)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(resume);
        if (!window.Truncated)
        {
            return window;
        }

        resume.Option(PatternOption, query.Text.Pattern)
            .Flag(RegexOption, query.Text.Expression is not null)
            .Flag(CaseSensitiveOption, query.Text.CaseSensitive);
        if (query.Scope is not null)
        {
            resume.Option(ScopeOption, query.Scope);
        }

        resume.Option(MaxHitsOption, query.MaxHits).Option(SkipOption, query.Skip + window.Returned);
        return window with { Next = resume.ToString() };
    }

    /// <summary>The search options in help order, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Validates the pattern and hit budget before any document is opened.</summary>
    public SearchQuery Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int maxHits = parse.GetValue(_maxHits);
        OptionGuards.EnsureInRange(
            MaxHitsOption,
            maxHits,
            1,
            MaximumHits,
            "Keep the hit budget modest and narrow the search instead.");
        int skip = parse.GetValue(_skip);
        OptionGuards.EnsureInRange(
            SkipOption,
            skip,
            0,
            int.MaxValue,
            "Use the --skip value from the previous result's window.next.");
        return new SearchQuery(
            TextSearch.Create(
                parse.GetRequiredValue(_pattern),
                parse.GetValue(_regex),
                parse.GetValue(_caseSensitive)),
            maxHits,
            _scope is null ? null : parse.GetRequiredValue(_scope),
            skip);
    }
}
