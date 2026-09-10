using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Rendering;

/// <summary>Shared allocation guard for raster renderers.</summary>
public static class RenderPixelGuard
{
    /// <summary>Default per-image ceiling (256 megapixels).</summary>
    public const long DefaultMaxPixels = 256L * 1024 * 1024;

    /// <summary>Rejects a raster resolution outside the product-owned contract.</summary>
    public static void EnsureDpi(int dpi, int minimumDpi, int maximumDpi)
    {
        if (minimumDpi <= 0 || maximumDpi < minimumDpi)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumDpi),
                "The DPI bounds must be positive and ordered.");
        }
        if (dpi < minimumDpi || dpi > maximumDpi)
        {
            throw CliErrors.OptionInvalid(
                "--dpi",
                $"value must be between {minimumDpi} and {maximumDpi}",
                $"Use {minimumDpi}-{maximumDpi} DPI.");
        }
    }

    /// <summary>Rejects an image whose pixel count exceeds the configured ceiling.</summary>
    public static void EnsureFits(long width, long height, int dpi, long maxPixels = DefaultMaxPixels)
    {
        if (maxPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPixels),
                maxPixels,
                "The pixel limit must be positive.");
        }
        if (width <= 0
            || height <= 0
            || width > maxPixels
            || height > maxPixels
            || width > maxPixels / height)
        {
            throw CliErrors.RenderTooLarge(width, height, dpi);
        }
    }
}
