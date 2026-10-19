using System.CommandLine;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--bom</c> option of a command that writes CSV text: a UTF-8 byte order mark at the
/// start of the text, off by default, in the standard wording with the product naming the text
/// it starts and the condition it needs.
/// </summary>
public sealed class BomOption
{
    /// <summary>The option name.</summary>
    public const string Name = "--bom";

    private readonly Option<bool> _bom;

    /// <summary>Creates the option.</summary>
    /// <param name="what">The text the mark starts, such as <c>each table's CSV</c>.</param>
    /// <param name="onlyWith">The condition the option needs, such as <c>--what tables</c>, or null.</param>
    public BomOption(string what, string? onlyWith = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        _bom = new Option<bool>(Name)
        {
            Description = $"Start {what} with a UTF-8 byte order mark, so Excel reads its non-English text correctly"
                + (onlyWith is null ? string.Empty : $"; only with {onlyWith}")
                + ". Default: UTF-8 without one.",
        };
        Options = [_bom];
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Whether the caller asked for the mark.</summary>
    /// <param name="parse">The parsed command line.</param>
    /// <param name="refusal">Why this invocation writes no text the mark could start, or null when it does.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the caller asked for the mark and <paramref name="refusal"/> is given.</exception>
    public bool Read(ParseResult parse, string? refusal)
    {
        ArgumentNullException.ThrowIfNull(parse);
        bool requested = parse.GetValue(_bom);
        return requested && refusal is not null
            ? throw CliErrors.OptionInvalid(Name, refusal, $"Drop {Name}.")
            : requested;
    }
}
