using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The one <c>--dpi</c> option: the shared range and default of every raster output.</summary>
public sealed class DpiOption
{
    private readonly Option<int> _dpi = new("--dpi")
    {
        Description = $"Raster resolution ({RenderPixelGuard.MinimumDpi}-{RenderPixelGuard.MaximumDpi}; "
            + $"default {RenderPixelGuard.DefaultDpi}); ignored for vector formats.",
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

/// <summary>One resolved selection of rendered parts.</summary>
/// <param name="Range">The 1-based range, or null for the default first part.</param>
/// <param name="All">Whether every part was requested.</param>
public sealed record PartSelection(PageRange? Range, bool All);

/// <summary>
/// The shared part selection of a render command: the <see cref="PartRangeOption"/>
/// <c>--{part}s</c> with <c>--all-{part}s</c> for a product-supplied part noun. Omitting both
/// renders the first part.
/// </summary>
public sealed class PartSelectionOptions
{
    private readonly PartRangeOption _range;
    private readonly Option<bool> _all;

    /// <summary>Creates the options for a product-supplied part noun, such as <c>page</c>.</summary>
    public PartSelectionOptions(string part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        _range = new PartRangeOption(part, $"{part} 1");
        _all = new Option<bool>($"--all-{part}s") { Description = $"Render every {part}." };
        Options = [.. _range.Options, _all];
    }

    /// <summary>Both options, for a product command's option list.</summary>
    public IReadOnlyList<Option> Options { get; }

    /// <summary>Returns the validated selection.</summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> when both are given; <c>PAGE_RANGE_INVALID</c> for a bad range.
    /// </exception>
    public PartSelection Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        string? range = _range.Read(parse);
        bool all = parse.GetValue(_all);
        if (range is not null && all)
        {
            throw CliErrors.OptionInvalid(
                _all.Name,
                $"cannot be combined with {_range.Name}",
                $"Choose a range with {_range.Name} or every part with {_all.Name}.");
        }

        return new PartSelection(range is null ? null : PageRange.Parse(range), all);
    }
}
