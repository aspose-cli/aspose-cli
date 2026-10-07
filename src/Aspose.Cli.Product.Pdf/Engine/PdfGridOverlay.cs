using System.Globalization;
using SkiaSharp;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Draws the labelled coordinate grid of <c>pdf render --grid</c> on a rendered page raster. The grid
/// uses the coordinates <c>redact_area</c> takes: points from the top-left corner of the visible,
/// rotated page box, which the page raster covers at <c>dpi / 72</c> pixels per point. Only the
/// image changes; the document is never modified.
/// </summary>
internal static class PdfGridOverlay
{
    internal const int MinimumSpacing = 10;
    internal const int MaximumSpacing = 500;

    private static readonly SKColor MinorLine = new(0, 90, 255, 80);
    private static readonly SKColor MajorLine = new(0, 90, 255, 150);
    private static readonly SKColor LabelText = new(0, 50, 160, 255);
    private static readonly SKColor LabelBackground = new(255, 255, 255, 200);

    /// <summary>The contract note for a grid drawn with <paramref name="spacing"/>.</summary>
    internal static PdfRenderGrid Describe(int spacing) => new()
    {
        Spacing = spacing,
        LabelSpacing = spacing * (spacing >= 100 ? 1 : spacing >= 25 ? 2 : 5),
    };

    /// <summary>Decodes <paramref name="png"/>, draws the grid and encodes it as <paramref name="format"/>.</summary>
    internal static void Draw(
        Stream png,
        PdfRenderGrid grid,
        double pageWidthPoints,
        double pageHeightPoints,
        int dpi,
        string format,
        Stream output)
    {
        using SKBitmap bitmap = SKBitmap.Decode(png)
            ?? throw new InvalidOperationException("The rendered page image could not be decoded.");
        double scale = dpi / 72d;
        float minorWidth = Math.Max(1f, (float)Math.Round(scale / 2));
        float majorWidth = Math.Max(2f, (float)Math.Round(scale));
        using (var canvas = new SKCanvas(bitmap))
        using (var minor = new SKPaint { Color = MinorLine, Style = SKPaintStyle.Fill })
        using (var major = new SKPaint { Color = MajorLine, Style = SKPaintStyle.Fill })
        using (var text = new SKPaint { Color = LabelText, IsAntialias = true })
        using (var background = new SKPaint { Color = LabelBackground, Style = SKPaintStyle.Fill })
        using (var font = new SKFont(SKTypeface.Default, Math.Max(10f, (float)Math.Round(8 * scale))))
        {
            for (int value = 0; value <= pageWidthPoints + 1e-6; value += grid.Spacing)
            {
                bool labelled = value % grid.LabelSpacing == 0;
                float width = labelled ? majorWidth : minorWidth;
                float x = Pixel(value, scale);
                canvas.DrawRect(x - MathF.Floor(width / 2), 0, width, bitmap.Height, labelled ? major : minor);
            }

            for (int value = 0; value <= pageHeightPoints + 1e-6; value += grid.Spacing)
            {
                bool labelled = value % grid.LabelSpacing == 0;
                float width = labelled ? majorWidth : minorWidth;
                float y = Pixel(value, scale);
                canvas.DrawRect(0, y - MathF.Floor(width / 2), bitmap.Width, width, labelled ? major : minor);
            }

            float pad = Math.Max(2f, (float)Math.Round(scale));
            for (int value = 0; value <= pageWidthPoints + 1e-6; value += grid.LabelSpacing)
            {
                Label(canvas, font, text, background, value, Pixel(value, scale) + pad, pad);
            }

            for (int value = grid.LabelSpacing; value <= pageHeightPoints + 1e-6; value += grid.LabelSpacing)
            {
                Label(canvas, font, text, background, value, pad, Pixel(value, scale) + pad);
            }
        }

        using SKData data = bitmap.Encode(
            format == "jpeg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png,
            format == "jpeg" ? 95 : 100)
            ?? throw new InvalidOperationException("The page image with its grid could not be encoded.");
        data.SaveTo(output);
    }

    private static float Pixel(int points, double scale) => (float)Math.Round(points * scale);

    /// <summary>Draws one point value with its top-left corner at (<paramref name="left"/>, <paramref name="top"/>).</summary>
    private static void Label(SKCanvas canvas, SKFont font, SKPaint text, SKPaint background, int value, float left, float top)
    {
        string label = value.ToString(CultureInfo.InvariantCulture);
        float width = font.MeasureText(label, text);
        SKFontMetrics metrics = font.Metrics;
        float height = metrics.Descent - metrics.Ascent;
        canvas.DrawRect(left - 1, top - 1, width + 2, height + 2, background);
        canvas.DrawText(label, left, top - metrics.Ascent, SKTextAlign.Left, font, text);
    }
}
