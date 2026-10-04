using System.Globalization;
using Aspose.Cli.Product.Slides.Contracts;
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

    /// <summary>
    /// The area the shape's laid-out text occupies, in slide points. A placeholder is usually far
    /// taller than its text, so its frame alone cannot tell whether the text runs into a table.
    /// The paragraph rectangles are relative to the unrotated frame, so text in a rotated shape
    /// or in vertical text is not projected.
    /// </summary>
    internal static SlideRect? TextRect(IShape shape)
    {
        if (shape is not IAutoShape { TextFrame: { } frame }
            || string.IsNullOrWhiteSpace(frame.Text)
            || shape.Rotation % 360 != 0
            || frame.TextFrameFormat.GetEffective().TextVerticalType
                is not (TextVerticalType.Horizontal or TextVerticalType.NotDefined))
        {
            return null;
        }

        System.Drawing.RectangleF? bounds = null;
        foreach (IParagraph paragraph in frame.Paragraphs)
        {
            System.Drawing.RectangleF rect = paragraph.GetRect();
            if (rect.Width > 0 && rect.Height > 0)
            {
                bounds = bounds is { } union ? System.Drawing.RectangleF.Union(union, rect) : rect;
            }
        }

        return bounds is { } area
            ? new SlideRect { X = shape.X + area.X, Y = shape.Y + area.Y, Width = area.Width, Height = area.Height }
            : null;
    }

    /// <summary>
    /// Whether the shape grows to fit its text, so its stored frame may lag behind the text, or
    /// shrinks its text on overflow, which the laid-out lines do not reflect
    /// (SLIDES-AUTOFIT-RECT).
    /// </summary>
    internal static bool TextAutofits(IShape shape) =>
        shape is IAutoShape { TextFrame: { } frame }
        && frame.TextFrameFormat.GetEffective().AutofitType is TextAutofitType.Shape or TextAutofitType.Normal;

    /// <summary>
    /// The one solid color behind the text of a text shape or chart: its own opaque fill, else
    /// that of the topmost filled shape beneath its center, on the slide or in the art its layout
    /// and master show, else the slide's solid background. Null for other shapes, when a filled
    /// shape beneath covers less than half of the shape, and when what shows through is a
    /// picture, gradient, pattern, translucent fill, table, group or anything else without one
    /// known color.
    /// </summary>
    internal static System.Drawing.Color? Backdrop(ISlide slide, IShape shape)
    {
        if (shape is not (IAutoShape { TextFrame: not null } or IChart))
        {
            return null;
        }

        float x = shape.X + (shape.Width / 2);
        float y = shape.Y + (shape.Height / 2);
        foreach (IShape below in Beneath(slide, shape))
        {
            if (!ReferenceEquals(below, shape)
                && (x < below.X || x > below.X + below.Width || y < below.Y || y > below.Y + below.Height))
            {
                continue;
            }

            if (below is not (IAutoShape or IChart))
            {
                return null;
            }

            IFillFormatEffectiveData fill = below.FillFormat.GetEffective();
            if (fill.FillType != FillType.NoFill)
            {
                return ReferenceEquals(below, shape) || Covered(shape, below) >= 0.5 ? Opaque(fill) : null;
            }
        }

        return Opaque(slide.Background.GetEffective().FillFormat);
    }

    /// <summary>
    /// The shape and what is drawn beneath it, topmost first: the slide's shapes, then the
    /// shapes the layout and its master draw on the slide (their placeholders draw nothing).
    /// </summary>
    private static IEnumerable<IShape> Beneath(ISlide slide, IShape shape)
    {
        IEnumerable<IShape> beneath = slide.Shapes.Take(slide.Shapes.IndexOf(shape) + 1).Reverse();
        if (slide.ShowMasterShapes && slide.LayoutSlide is { } layout)
        {
            beneath = beneath.Concat(Art(layout));
            if (layout.ShowMasterShapes && layout.MasterSlide is { } master)
            {
                beneath = beneath.Concat(Art(master));
            }
        }

        return beneath;

        static IEnumerable<IShape> Art(IBaseSlide slide) =>
            slide.Shapes.Where(static shape => shape.Placeholder is null).Reverse();
    }

    /// <summary>The share of the shape's frame that the other shape's frame covers.</summary>
    private static double Covered(IShape shape, IShape other)
    {
        double width = Math.Min(shape.X + shape.Width, other.X + other.Width) - Math.Max(shape.X, other.X);
        double height = Math.Min(shape.Y + shape.Height, other.Y + other.Height) - Math.Max(shape.Y, other.Y);
        return Math.Max(0, width) * Math.Max(0, height) / Math.Max(1, shape.Width * shape.Height);
    }

    private static System.Drawing.Color? Opaque(IFillFormatEffectiveData fill) =>
        fill.FillType == FillType.Solid && fill.SolidFillColor.A >= 230 ? fill.SolidFillColor : null;

    /// <summary>The color a chart states for all its text; null for another shape or when the chart style decides it.</summary>
    internal static System.Drawing.Color? ChartTextColor(IShape shape) =>
        shape is IChart { TextFormat.PortionFormat.FillFormat: { FillType: FillType.Solid } fill }
            ? fill.SolidFillColor.Color
            : null;

    /// <summary>A color as <c>#RRGGBB</c>, without its transparency.</summary>
    internal static string Hex(System.Drawing.Color color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
}
