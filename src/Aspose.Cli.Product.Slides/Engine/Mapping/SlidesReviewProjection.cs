using Aspose.Slides;
using Aspose.Slides.Charts;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>Projects effective engine formatting into non-wire review facts.</summary>
internal static class SlidesReviewProjection
{
    internal static bool HasOpaqueFill(IShape shape)
    {
        if (shape is IChart or ITable)
        {
            return true;
        }

        IFillFormatEffectiveData fill = shape.FillFormat.GetEffective();
        return fill.FillType switch
        {
            FillType.Solid => fill.SolidFillColor.A >= 230,
            FillType.Pattern => true,
            _ => false,
        };
    }
}
