using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Rendering;

/// <summary>
/// The one raster contract: the accepted resolution range and the per-image pixel budget
/// declared by the invocation ledger (<see cref="ResourceBudgetKinds.RasterPixels"/>).
/// </summary>
public static class RenderPixelGuard
{
    /// <summary>Lowest accepted raster resolution.</summary>
    public const int MinimumDpi = 36;

    /// <summary>Highest accepted raster resolution.</summary>
    public const int MaximumDpi = 1_200;

    /// <summary>Resolution used when a command does not choose one.</summary>
    public const int DefaultDpi = 192;

    /// <summary>Rejects a raster resolution outside the shared range.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> naming <c>--dpi</c>.</exception>
    public static void EnsureDpi(int dpi)
    {
        if (dpi is < MinimumDpi or > MaximumDpi)
        {
            throw CliErrors.OptionInvalid(
                "--dpi",
                $"value must be between {MinimumDpi} and {MaximumDpi}",
                $"Use {MinimumDpi}-{MaximumDpi} DPI; {DefaultDpi} gives crisp text.");
        }
    }

    /// <summary>
    /// Rejects one raster image whose bitmap would exceed the ledger's pixel budget, before
    /// the engine allocates it.
    /// </summary>
    /// <param name="budgets">The invocation ledger that declares the budget.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="dpi">The resolution that produced the size, or null for an explicit size.</param>
    /// <param name="hint">Product guidance naming its own way to shrink the image.</param>
    /// <exception cref="CliException"><c>RENDER_TOO_LARGE</c>.</exception>
    public static void EnsureFits(
        ResourceBudgetLedger budgets,
        long width,
        long height,
        int? dpi,
        string hint = "Select a smaller render region or lower --dpi.")
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        long maxPixels = budgets.Limit(ResourceBudgetKinds.RasterPixels);
        if (width <= 0
            || height <= 0
            || width > maxPixels
            || height > maxPixels
            || width > maxPixels / height)
        {
            throw CliErrors.RenderTooLarge(width, height, dpi, maxPixels, hint);
        }
    }
}
