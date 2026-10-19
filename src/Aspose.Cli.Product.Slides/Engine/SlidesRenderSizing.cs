using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>The scale slides are rendered at and the raster budget the rendered size must fit.</summary>
internal static class SlidesRenderSizing
{
    internal static float RenderScale(Presentation presentation, SlidesRenderRequest request)
    {
        if (request.Width is int width)
        {
            return (float)(width / presentation.SlideSize.Size.Width);
        }

        return (request.Dpi ?? RenderPixelGuard.DefaultDpi) / 72f;
    }

    internal static void EnsureRasterFits(
        ResourceBudgetLedger resourceBudgets,
        long width,
        long height,
        int? dpi) =>
        RenderPixelGuard.EnsureFits(
            resourceBudgets,
            width,
            height,
            dpi,
            "Lower --dpi or pass a smaller --width.");
}
