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

    /// <summary>Adds the option to one product command.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(_dpi);
    }

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
/// The shared page or slide selection of a render command: <c>--pages</c> with
/// <c>--all-pages</c>, or <c>--slides</c> with <c>--all-slides</c>. Omitting both
/// renders the first part.
/// </summary>
public sealed class PartSelectionOptions
{
    private readonly Option<string?> _range;
    private readonly Option<bool> _all;

    /// <summary>Creates the options for a part noun such as <c>page</c> or <c>slide</c>.</summary>
    public PartSelectionOptions(string part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(part);
        _range = new Option<string?>($"--{part}s")
        {
            Description = $"1-based {part} range, e.g. 1-3,7,9-. Default: {part} 1.",
        }.WithInput(InputKind.None);
        _all = new Option<bool>($"--all-{part}s") { Description = $"Render every {part}." };
    }

    /// <summary>Adds both options to one product command.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(_range);
        command.Options.Add(_all);
    }

    /// <summary>Returns the validated selection.</summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> when both are given; <c>PAGE_RANGE_INVALID</c> for a bad range.
    /// </exception>
    public PartSelection Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        string? range = parse.GetValue(_range);
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
