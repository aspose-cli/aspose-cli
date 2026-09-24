using System.CommandLine;
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

/// <summary>One validated <c>query search</c> invocation.</summary>
/// <param name="Text">The validated pattern, ready to match.</param>
/// <param name="MaxHits">The returned-hit budget.</param>
/// <param name="Scope">The selected scope, or null when the product has none.</param>
public sealed record SearchQuery(TextSearch Text, int MaxHits, string? Scope);

/// <summary>
/// The one <c>query search</c> grammar: <c>--pattern</c>, <c>--regex</c>,
/// <c>--case-sensitive</c>, <c>--max-hits</c> and an optional product-supplied
/// <c>--scope</c>. Every product validates the pattern and the hit budget the same way.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>Hits returned when <c>--max-hits</c> is omitted.</summary>
    public const int DefaultMaxHits = 100;

    /// <summary>The largest accepted <c>--max-hits</c>.</summary>
    public const int MaximumHits = 10_000;

    private readonly Option<string> _pattern;
    private readonly Option<bool> _regex;
    private readonly Option<bool> _caseSensitive;
    private readonly Option<int> _maxHits;
    private readonly Option<string>? _scope;

    /// <summary>Creates the search options, with a scope only when the product has one.</summary>
    public SearchOptions(SearchScopeGrammar? scope = null)
    {
        _pattern = new Option<string>("--pattern")
        {
            Required = true,
            Description = "Text to find, or a regular expression with --regex.",
        }.WithInput(InputKind.None);
        _regex = new Option<bool>("--regex")
        {
            Description = "Treat the pattern as a culture-invariant regular expression with a one-second budget.",
        };
        _caseSensitive = new Option<bool>("--case-sensitive")
        {
            Description = "Match letter case exactly.",
        };
        _maxHits = new Option<int>("--max-hits")
        {
            Description = $"Maximum returned hits (1-{MaximumHits}).",
            DefaultValueFactory = _ => DefaultMaxHits,
        };
        if (scope is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope.Description);
            if (!scope.Values.Contains(scope.Default, StringComparer.Ordinal))
            {
                throw new ArgumentException("The default scope must be one of the accepted values.", nameof(scope));
            }

            _scope = new Option<string>("--scope")
            {
                Description = scope.Description,
                DefaultValueFactory = _ => scope.Default,
            }.WithInput(InputKind.None);
            _scope.AcceptOnlyFromAmong([.. scope.Values]);
        }

        Options = _scope is null
            ? [_pattern, _regex, _caseSensitive, _maxHits]
            : [_pattern, _regex, _caseSensitive, _scope, _maxHits];
    }

    /// <summary>The search options in help order, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Adds the search options to one product command.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        foreach (Option option in Options)
        {
            command.Options.Add(option);
        }
    }

    /// <summary>Validates the pattern and hit budget before any document is opened.</summary>
    public SearchQuery Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int maxHits = parse.GetValue(_maxHits);
        OptionGuards.EnsureInRange(
            "--max-hits",
            maxHits,
            1,
            MaximumHits,
            "Keep the hit budget modest and narrow the search instead.");
        return new SearchQuery(
            TextSearch.Create(
                parse.GetRequiredValue(_pattern),
                parse.GetValue(_regex),
                parse.GetValue(_caseSensitive)),
            maxHits,
            _scope is null ? null : parse.GetRequiredValue(_scope));
    }
}
