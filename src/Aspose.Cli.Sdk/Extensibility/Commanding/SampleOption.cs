using System.CommandLine;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--sample</c> option of an inspect command: a bounded sample, off by default, in
/// the standard wording with the product naming what the sample holds.
/// </summary>
public sealed class SampleOption
{
    private readonly Option<bool> _sample;

    /// <summary>Creates the option.</summary>
    /// <param name="what">What the sample holds, such as <c>the size of each page</c>.</param>
    public SampleOption(string what)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        _sample = new Option<bool>("--sample") { Description = $"Include a bounded sample: {what}. Default: off." };
        Options = [_sample];
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Whether the caller asked for the sample.</summary>
    public bool Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetValue(_sample);
    }
}
