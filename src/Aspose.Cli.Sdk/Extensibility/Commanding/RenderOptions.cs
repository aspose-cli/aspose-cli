using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The one <c>--dpi</c> option: the shared range and default of every raster output.</summary>
public sealed class DpiOption
{
    private readonly Option<int> _dpi = new("--dpi")
    {
        Description = $"Raster resolution ({RenderPixelGuard.MinimumDpi}-{RenderPixelGuard.MaximumDpi}); ignored for vector formats.",
        DefaultValueFactory = _ => RenderPixelGuard.DefaultDpi,
    };

    /// <summary>Creates the option.</summary>
    public DpiOption() => Options = [_dpi];

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Whether the caller typed <c>--dpi</c> rather than accepting the default.</summary>
    public bool IsExplicit(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetResult(_dpi) is { Implicit: false };
    }

    /// <summary>Returns the validated resolution.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> outside the shared range.</exception>
    public int Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int dpi = parse.GetValue(_dpi);
        RenderPixelGuard.EnsureDpi(dpi);
        return dpi;
    }
}

/// <summary>
/// The shared part selection of a render command: the <see cref="PartRangeOption"/>
/// <c>--{part}s</c> for a product-supplied part noun. Omitting it renders every part; one run
/// renders at most <see cref="RenderSelection.MaximumParts"/>.
/// </summary>
public sealed class PartSelectionOptions
{
    private readonly PartRangeOption _range;

    /// <summary>Creates the option for a product-supplied part noun, such as <c>page</c>.</summary>
    public PartSelectionOptions(string part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        _range = new PartRangeOption(part, string.Create(CultureInfo.InvariantCulture, $"every {part}; at most {RenderSelection.MaximumParts} per run"));
        Options = _range.Options;
        // Several parts are numbered by part: the ones a closed selection names.
        NumberedParts.Declare(Options[0], parse => Read(parse) is { Last: { } last } selection ? selection.Resolve(last) : null);
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>
    /// The selected parts, or null when the option is omitted, which selects every part; the
    /// engine resolves them with <see cref="RenderSelection.Resolve"/>.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the value is not a selection.</exception>
    public PageRange? Read(ParseResult parse) => _range.ReadRange(parse);
}
