using System.Globalization;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Rectangles between the engine's page space and the one results and operations use: points
/// from the top-left corner of the visible, rotated page.
/// </summary>
internal static class PdfGeometry
{
    internal static Rectangle ToPdfRect(Page page, PdfRectInput rect)
    {
        Rectangle box = page.GetPageRect(considerRotation: true);
        if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > box.Width || rect.Y + rect.Height > box.Height)
        {
            throw new OperationInvalidException(
                string.Create(CultureInfo.InvariantCulture, $"rect lies outside the visible page, which is {box.Width:0.##} x {box.Height:0.##} points"),
                "Measure rect in points from the top-left corner of the visible page; 'pdf inspect' reports each page's size.");
        }
        return FromContractRect(page, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// A rectangle the engine reports for text, such as a search match, as points from the
    /// top-left corner of the visible, rotated page.
    /// </summary>
    internal static PdfRect ToContractTextRect(Page page, Rectangle rect)
    {
        (double left, double top) = TextOrigin(page);
        return Rect(rect.LLX - left, top - rect.URY, rect.Width, rect.Height);
    }

    /// <summary>
    /// A rectangle as results report it: each value rounded to a hundredth of a point, which
    /// drops the engine's floating-point noise and keeps more precision than a page shows.
    /// </summary>
    internal static PdfRect Rect(double x, double y, double width, double height) => new()
    {
        X = Hundredth(x),
        Y = Hundredth(y),
        Width = Hundredth(width),
        Height = Hundredth(height),
    };

    private static double Hundredth(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Where the visible page's left and top edges lie in the space the engine reports text in,
    /// which on a rotated, cropped page moves with the rotation (PDF-TEXT-RECT-ROTATED-CROP).
    /// </summary>
    private static (double Left, double Top) TextOrigin(Page page)
    {
        Rectangle crop = page.CropBox;
        return page.Rotate switch
        {
            Rotation.on90 => (crop.LLY, crop.Width),
            Rotation.on180 => (0, crop.Height),
            Rotation.on270 => (0, crop.URX),
            _ => (crop.LLX, crop.URY),
        };
    }

    private static Rectangle FromContractRect(Page page, double x, double y, double width, double height)
    {
        Rectangle box = page.GetPageRect(considerRotation: true);
        return page.RotationMatrix.Reverse().Transform(new Rectangle(
            box.LLX + x, box.URY - y - height,
            box.LLX + x + width, box.URY - y));
    }

    /// <summary>A rectangle in the page's unrotated space, such as an annotation's, as points from the top-left corner of the visible, rotated page.</summary>
    internal static PdfRect ToContractRect(Page page, Rectangle rect)
    {
        Rectangle box = page.GetPageRect(considerRotation: true);
        Rectangle visible = page.RotationMatrix.Transform(rect);
        return Rect(visible.LLX - box.LLX, box.URY - visible.URY, visible.Width, visible.Height);
    }
}
