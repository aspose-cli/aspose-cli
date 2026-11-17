using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--{part}s</c> range option, such as <c>--pages</c>: the parts a
/// command processes, in the shared range syntax and the standard wording, with the command's
/// own default and precondition.
/// </summary>
public sealed class PartRangeOption
{
    private readonly Option<string?> _range;

    /// <summary>Creates the option.</summary>
    /// <param name="part">The part noun, such as <c>page</c>; the option is named after its plural.</param>
    /// <param name="defaultText">What the command processes when the option is omitted, such as <c>every page</c>.</param>
    /// <param name="onlyWith">The condition the option needs, such as <c>--by pages</c>, or null.</param>
    /// <param name="formats">The ids of the only output formats that hold a selection (<see cref="ParameterMetadata.Formats"/>), or null for every one.</param>
    public PartRangeOption(string part, string defaultText, string? onlyWith = null, IReadOnlyList<string>? formats = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultText);
        string parts = part + "s";
        _range = new Option<string?>("--" + parts)
        {
            Description = $"{char.ToUpperInvariant(parts[0])}{parts[1..]} to process: 1-based numbers and ranges such as 1-3,7,9-, or all. "
                + $"Default: {defaultText}."
                + (onlyWith is null ? string.Empty : $" Only with {onlyWith}."),
        }.WithInput(InputKind.None, formats: formats);
        Options = [_range];
    }

    /// <summary>The option name, such as <c>--pages</c>.</summary>
    public string Name => _range.Name;

    /// <summary>Whether an output in format <paramref name="format"/> holds a selection, as the option declares.</summary>
    public bool AppliesTo(string format) => _range.GetParameterMetadata().AppliesTo(format);

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>The range text as given, or null when the option is omitted.</summary>
    public string? Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetValue(_range);
    }

    /// <summary>The parsed range, or null when the option is omitted.</summary>
    /// <exception cref="Errors.CliException"><c>OPTION_INVALID</c> when the value is not a selection.</exception>
    public PageRange? ReadRange(ParseResult parse) =>
        Read(parse) is { } text ? PageRange.ParseOption(Name, text) : null;
}
