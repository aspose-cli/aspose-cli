using System.Drawing;

namespace Aspose.Cli.Product.Slides;

/// <summary>How far apart two colors are to the eye, as WCAG measures it.</summary>
internal static class SlidesContrast
{
    /// <summary>The WCAG contrast ratio of two colors, from 1 for equal luminance to 21.</summary>
    public static double Ratio(Color first, Color second)
    {
        double a = Luminance(first);
        double b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
