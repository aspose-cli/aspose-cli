using System.Globalization;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;
using static Aspose.Cli.Product.Pdf.Engine.Editing.PdfMutationSupport;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

// Visible content, annotations and redaction.
internal sealed partial class PdfMutationHandlers
{
    public long Apply(AddWatermarkTextOp operation)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        foreach (int number in pages)
        {
            var stamp = new TextStamp(operation.Text)
            {
                Background = operation.Layer == PdfLayers.Under,
                Opacity = operation.Opacity,
                RotateAngle = operation.Rotation,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            stamp.TextState.FontSize = (float)operation.Size;
            stamp.TextState.ForegroundColor = ParseColor(operation.Color);
            if (operation.Font is not null)
            {
                stamp.TextState.Font = FontRepository.FindFont(operation.Font);
            }

            _document.Pages[number].AddStamp(stamp);
            _touched.Add(number);
        }

        return pages.Count;
    }

    public long Apply(AddWatermarkImageOp operation)
    {
        EnsureFile(operation.Path);
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        // Read (and charge) the image once; every page stamps its own view of the bytes. An SVG
        // stamp fetches its external images with no resource hook (KNOWN-ISSUES.md), so the
        // read refuses an SVG that names a network address.
        byte[] image = NetworkReferenceGuard.ReadImage(_inputs, operation.Path);

        foreach (int number in pages)
        {
            var stamp = new ImageStamp(new MemoryStream(image, writable: false))
            {
                Background = operation.Layer == PdfLayers.Under,
                Opacity = operation.Opacity,
                Zoom = operation.Scale,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _document.Pages[number].AddStamp(stamp);
            _touched.Add(number);
        }

        return pages.Count;
    }

    public long Apply(AddPageNumbersOp operation)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        int ordinal = operation.Start;
        foreach (int number in pages)
        {
            string text = operation.Format
                .Replace("{n}", ordinal.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{N}", _document.Pages.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            AddTextStamp(_document.Pages[number], text, operation.Position, operation.Font);
            _touched.Add(number);
            ordinal++;
        }

        return pages.Count;
    }

    public long Apply(AddHeaderTextOp operation) =>
        MarginText(operation.Text, operation.Pages, operation.Position, operation.Font);

    public long Apply(AddFooterTextOp operation) =>
        MarginText(operation.Text, operation.Pages, operation.Position, operation.Font);

    public long Apply(AddStampImageOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        EnsureFile(operation.Path);
        Rectangle rectangle = ToPdfRect(page, operation.Rect);
        var stamp = new ImageStamp(new MemoryStream(NetworkReferenceGuard.ReadImage(_inputs, operation.Path), writable: false))
        {
            XIndent = rectangle.LLX,
            YIndent = rectangle.LLY,
            Width = rectangle.Width,
            Height = rectangle.Height,
        };
        page.AddStamp(stamp);
        _touched.Add(operation.Page);
        return 1;
    }

    public long Apply(AddLinkOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        var annotation = new LinkAnnotation(page, ToPdfRect(page, operation.Rect))
        {
            Action = new GoToURIAction(operation.Url),
        };
        page.Annotations.Add(annotation);
        _touched.Add(operation.Page);
        return 1;
    }

    public long Apply(RedactTextOp operation)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        PdfColor fill = ParseColor(operation.FillColor);
        long count = 0;
        foreach (int number in pages)
        {
            Page page = _document.Pages[number];
            TextFragmentCollection fragments = MatchText(page, operation.Pattern, operation.Regex, caseSensitive: true,
                static reason => new OperationInvalidException(reason));
            foreach (TextFragment fragment in fragments)
            {
                Cover(page, fragment.Rectangle, fill).Redact();
                count++;
            }
            if (fragments.Count > 0)
            {
                _touched.Add(number);
            }
        }

        return count;
    }

    public long Apply(RedactAreaOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        Cover(page, ToPdfRect(page, operation.Rect), ParseColor(operation.FillColor)).Redact();
        _touched.Add(operation.Page);
        return 1;
    }

    private long MarginText(string text, string? pageRange, string position, string? font)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, pageRange);
        foreach (int number in pages)
        {
            AddTextStamp(_document.Pages[number], text, position, font);
            _touched.Add(number);
        }

        return pages.Count;
    }

    private static void AddTextStamp(Page page, string text, string position, string? font)
    {
        var stamp = new TextStamp(text) { Background = false };
        if (font is not null)
        {
            stamp.TextState.Font = FontRepository.FindFont(font);
        }

        ApplyPosition(page, stamp, position);
        page.AddStamp(stamp);
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
