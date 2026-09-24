using System.CommandLine;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--max-chars</c> option of a character-budgeted read: the shared default and
/// range, with product help naming what the budget counts.
/// </summary>
public sealed class MaxCharactersOption
{
    /// <summary>The option name, for continuation commands.</summary>
    public const string Name = "--max-chars";

    /// <summary>The budget used when the option is omitted.</summary>
    internal const int DefaultCharacters = 20_000;

    private readonly Option<int> _maxCharacters;

    /// <summary>Creates the option with product help.</summary>
    public MaxCharactersOption(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _maxCharacters = new Option<int>(Name)
        {
            Description = description,
            DefaultValueFactory = _ => DefaultCharacters,
        };
        Options = [_maxCharacters];
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Returns the validated budget.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> outside 1 to <see cref="ReadContinuation.MaximumCharacters"/>.</exception>
    public int Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int characters = parse.GetValue(_maxCharacters);
        OptionGuards.EnsureInRange(
            Name, characters, 1, ReadContinuation.MaximumCharacters,
            "Use a positive bounded character budget.");
        return characters;
    }
}
