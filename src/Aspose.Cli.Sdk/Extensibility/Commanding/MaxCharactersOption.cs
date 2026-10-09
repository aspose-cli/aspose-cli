using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--max-chars</c> option of a character-budgeted read: the shared default and
/// range in the standard wording, with the product naming what the budget counts.
/// </summary>
public sealed class MaxCharactersOption
{
    /// <summary>The option name, for continuation commands.</summary>
    public const string Name = "--max-chars";

    /// <summary>The budget used when the option is omitted.</summary>
    internal const int DefaultCharacters = 20_000;

    private readonly Option<int> _maxCharacters;

    /// <summary>Creates the option.</summary>
    /// <param name="what">What the budget counts, such as <c>the text of each returned page</c>.</param>
    public MaxCharactersOption(string what)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        _maxCharacters = new Option<int>(Name)
        {
            Description = string.Create(
                CultureInfo.InvariantCulture,
                $"Maximum characters returned, counting {what}. Range 1-{ReadContinuation.MaximumCharacters}, default {DefaultCharacters}."),
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
