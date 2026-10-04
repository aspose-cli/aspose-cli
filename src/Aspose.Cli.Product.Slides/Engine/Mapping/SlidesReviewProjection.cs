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

    /// <summary>A color as <c>#RRGGBB</c>, without its transparency.</summary>
    internal static string Hex(System.Drawing.Color color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
}
