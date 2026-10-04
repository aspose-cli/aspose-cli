using System.Globalization;
using System.Xml;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using SkiaSharp;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// The proportions an image is drawn with on a PDF page, read through the invocation's input
/// budget. Only the ratio of the width to the height is meaningful.
/// </summary>
internal static class PdfImageSize
{
    /// <summary>
    /// The width and height the engine draws the image with, at least 1 each: those of a raster
    /// image turned by its EXIF orientation, which the engine applies, and for an SVG without
    /// both a width and a height those of its viewBox, which the engine does not read.
    /// </summary>
    internal static (double Width, double Height) Read(InputSource inputs, string path)
    {
        if (string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase)
            && SvgViewBox(inputs, path) is { } viewBox)
        {
            return viewBox;
        }

        double width;
        double height;
        using (Stream content = inputs.OpenFile(path))
        {
            // An image the engine cannot load reads as 0 x 0 here and fails when the page is saved.
            var image = new ImageStamp(content);
            width = Math.Max(1, image.Width);
            height = Math.Max(1, image.Height);
        }

        return QuarterTurned(inputs, path) ? (height, width) : (width, height);
    }

    /// <summary>Whether the image's EXIF orientation turns it a quarter, swapping its sides.</summary>
    private static bool QuarterTurned(InputSource inputs, string path)
    {
        using Stream content = inputs.OpenFile(path);
        using SKCodec? codec = SKCodec.Create(content);
        return codec?.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
    }

    /// <summary>The viewBox size of an SVG whose root element lacks a width or a height.</summary>
    private static (double Width, double Height)? SvgViewBox(InputSource inputs, string path)
    {
        try
        {
            using Stream content = inputs.OpenFile(path);
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
            });
            if (reader.MoveToContent() != XmlNodeType.Element
                || reader.GetAttribute("width") is not null && reader.GetAttribute("height") is not null
                || reader.GetAttribute("viewBox") is not { } viewBox)
            {
                return null;
            }

            string[] values = viewBox.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            return values.Length == 4
                && double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
                && double.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
                && width > 0 && height > 0
                ? (width, height)
                : null;
        }
        catch (XmlException)
        {
            // The engine reports an SVG it cannot read when the page is saved.
            return null;
        }
    }
}
