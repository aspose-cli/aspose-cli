using System.CommandLine;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--preview</c> option of an inspect command: a bounded preview, off by default, in
/// the standard wording with the product naming what the preview holds.
/// </summary>
public sealed class PreviewOption
{
    private readonly Option<bool> _preview;

    /// <summary>Creates the option.</summary>
    /// <param name="what">What the preview holds, such as <c>the size of each page</c>.</param>
    public PreviewOption(string what)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(what);
        _preview = new Option<bool>("--preview") { Description = $"Include a bounded preview: {what}. Default: off." };
        Options = [_preview];
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Whether the caller asked for the preview.</summary>
    public bool Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetValue(_preview);
    }
}
