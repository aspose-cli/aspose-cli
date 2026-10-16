using System.CommandLine;
using System.Globalization;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--dpi</c> option: the shared range and default of every raster output, and the
/// one decision that a vector output, such as svg, refuses a pixel size.
/// </summary>
public sealed class DpiOption
{
    private readonly Option<int> _dpi = new Option<int>("--dpi")
    {
        Description = "Raster resolution; refused for vector output such as svg.",
        DefaultValueFactory = _ => RenderPixelGuard.DefaultDpi,
    }.InRange(RenderPixelGuard.MinimumDpi, RenderPixelGuard.MaximumDpi);

    private readonly IReadOnlyList<Option> _pixelSizes;

    /// <summary>Creates the option.</summary>
    /// <param name="pixelSizes">
    /// The command's own options that also set a raster's pixel size, such as an exact width,
    /// which a vector output refuses as it refuses <c>--dpi</c>. The command lists them itself.
    /// </param>
    public DpiOption(params Option[] pixelSizes)
    {
        ArgumentNullException.ThrowIfNull(pixelSizes);
        _pixelSizes = pixelSizes;
        Options = [_dpi];
    }

    /// <summary>The option, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Whether the caller typed <c>--dpi</c> rather than accepting the default.</summary>
    public bool IsExplicit(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        return parse.GetResult(_dpi) is { Implicit: false };
    }

    /// <summary>
    /// Returns the validated resolution of an output in <paramref name="format"/>. A vector
    /// format has no pixel size, so a typed <c>--dpi</c> or pixel-size option is refused rather
    /// than ignored; the default resolution of a vector output is unused.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> outside the shared range, or for a pixel size of a vector output.
    /// </exception>
    public int Read(ParseResult parse, FormatDescriptor format)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(format);
        if (!format.Raster
            && _pixelSizes.Prepend(_dpi).FirstOrDefault(option => parse.GetResult(option) is { Implicit: false }) is { } typed)
        {
            throw CliErrors.OptionInvalid(
                typed.Name,
                $"a {format.Id} output is vector and has no pixel size",
                $"Drop {typed.Name} to render {format.Id}, or render --to png at that size.");
        }

        return parse.GetInRange(
            _dpi,
            string.Create(CultureInfo.InvariantCulture, $"Use {RenderPixelGuard.MinimumDpi}-{RenderPixelGuard.MaximumDpi} DPI; {RenderPixelGuard.DefaultDpi} gives crisp text."));
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
