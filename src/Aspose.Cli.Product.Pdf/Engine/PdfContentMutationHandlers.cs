using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Devices;
using Aspose.Pdf.Forms;
using Aspose.Pdf.Optimization;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfArtifactSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns visible content, annotation and redaction mutations.</summary>
internal static class PdfContentMutationHandlers
{
    internal static long WatermarkText(Document document, AddWatermarkTextOp op, ISet<int> touched)
    {
        IReadOnlyList<int> pages = ResolveOptional(document, op.Pages);
        foreach (int number in pages)
        {
            var stamp = new TextStamp(op.Text)
            {
                Background = op.Layer == "under",
                Opacity = op.Opacity,
                RotateAngle = op.Rotation,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            stamp.TextState.FontSize = (float)op.Size;
            stamp.TextState.ForegroundColor = ParseColor(op.Color);
            if (op.Font is not null)
            {
                stamp.TextState.Font = FontRepository.FindFont(op.Font);
            }

            document.Pages[number].AddStamp(stamp);
            touched.Add(number);
        }

        return pages.Count;
    }

    internal static long WatermarkImage(Document document, AddWatermarkImageOp op, ISet<int> touched, InputResourceScope inputs)
    {
        EnsureFile(op.Path);
        IReadOnlyList<int> pages = ResolveOptional(document, op.Pages);
        // Read (and charge) the image once; every page stamps its own view of the bytes.
        byte[] image;
        using (var buffer = new MemoryStream())
        {
            inputs.OpenFile(op.Path).CopyTo(buffer);
            image = buffer.ToArray();
        }

        foreach (int number in pages)
        {
            var stamp = new ImageStamp(new MemoryStream(image, writable: false))
            {
                Background = op.Layer == "under",
                Opacity = op.Opacity,
                Zoom = op.Scale,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            document.Pages[number].AddStamp(stamp);
            touched.Add(number);
        }

        return pages.Count;
    }

    internal static long PageNumbers(Document document, AddPageNumbersOp op, ISet<int> touched)
    {
        IReadOnlyList<int> pages = ResolveOptional(document, op.Pages);
        int ordinal = op.Start;
        foreach (int number in pages)
        {
            string text = op.Format
                .Replace("{n}", ordinal.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{N}", document.Pages.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            AddTextStamp(document.Pages[number], text, op.Position, op.Font);
            touched.Add(number);
            ordinal++;
        }

        return pages.Count;
    }

    internal static long HeaderFooter(
        Document document,
        string text,
        string? pageRange,
        string position,
        string? font,
        ISet<int> touched)
    {
        IReadOnlyList<int> pages = ResolveOptional(document, pageRange);
        foreach (int number in pages)
        {
            AddTextStamp(document.Pages[number], text, position, font);
            touched.Add(number);
        }

        return pages.Count;
    }

    internal static void AddTextStamp(Page page, string text, string position, string? font)
    {
        var stamp = new TextStamp(text) { Background = false };
        if (font is not null)
        {
            stamp.TextState.Font = FontRepository.FindFont(font);
        }

        ApplyPosition(page, stamp, position);
        page.AddStamp(stamp);
    }

    internal static long StampImage(Document document, AddStampImageOp op, ISet<int> touched, InputResourceScope inputs)
    {
        Page page = PageAt(document, op.Page);
        EnsureFile(op.Path);
        Rectangle rectangle = ToPdfRect(page, op.Rect);
        var stamp = new ImageStamp(inputs.OpenFile(op.Path))
        {
            XIndent = rectangle.LLX,
            YIndent = rectangle.LLY,
            Width = rectangle.Width,
            Height = rectangle.Height,
        };
        page.AddStamp(stamp);
        touched.Add(op.Page);
        return 1;
    }

    internal static long AddLink(Document document, AddLinkOp op, ISet<int> touched)
    {
        Page page = PageAt(document, op.Page);
        var annotation = new LinkAnnotation(page, ToPdfRect(page, op.Rect))
        {
            Action = new GoToURIAction(op.Url),
        };
        page.Annotations.Add(annotation);
        touched.Add(op.Page);
        return 1;
    }

    internal static long RedactText(Document document, RedactTextOp op, ISet<int> touched)
    {
        IReadOnlyList<int> pages = ResolveOptional(document, op.Pages);
        PdfColor fill = ParseColor(op.FillColor);
        long count = 0;
        foreach (int number in pages)
        {
            Page page = document.Pages[number];
            TextFragmentCollection fragments = MatchText(page, op.Pattern, op.Regex, caseSensitive: true,
                static reason => new OperationInvalidException(reason));
            foreach (TextFragment fragment in fragments)
            {
                Cover(page, fragment.Rectangle, fill).Redact();
                count++;
            }
            if (fragments.Count > 0)
            {
                touched.Add(number);
            }
        }

        return count;
    }

    internal static long RedactArea(Document document, RedactAreaOp op, ISet<int> touched)
    {
        Page page = PageAt(document, op.Page);
        Cover(page, ToPdfRect(page, op.Rect), ParseColor(op.FillColor)).Redact();
        touched.Add(op.Page);
        return 1;
    }

    /// <summary>
    /// Adds the redaction annotation that covers one rectangle in the requested
    /// colour. Aspose only builds the cover from FillColor once Color is also set;
    /// with FillColor alone it draws its default black box whatever was asked for.
    /// Color itself never reaches the page, so both carry the requested colour.
    /// </summary>
    private static RedactionAnnotation Cover(Page page, Rectangle rectangle, PdfColor fill)
    {
        var annotation = new RedactionAnnotation(page, rectangle)
        {
            FillColor = fill,
            Color = fill,
        };
        page.Annotations.Add(annotation);
        return annotation;
    }
}
